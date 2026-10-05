using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ImageCoreService;

// usage: probe <maxEdge> <out.png> <img1> [img2 ...]  -> draws the detected outline on each image
if (args[0] == "bw") { BwProbe.Run(args[1]); return; }
if (args[0] == "sheet") { SheetProbe.Run(args[1], args[2]); return; }
int maxEdge = int.Parse(args[0]);
string outDir = args[1];
Directory.CreateDirectory(outDir);
foreach (string path in args.Skip(2))
{
    using var bmp = new Bitmap(path);
    // honour EXIF orientation like the app does
    if (bmp.PropertyIdList.Contains(0x0112))
    {
        int o = bmp.GetPropertyItem(0x0112)!.Value![0];
        var flip = o switch { 3 => RotateFlipType.Rotate180FlipNone, 6 => RotateFlipType.Rotate90FlipNone, 8 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone };
        bmp.RotateFlip(flip);
    }
    double s = Math.Min(1.0, maxEdge / (double)Math.Max(bmp.Width, bmp.Height));
    int w = (int)(bmp.Width * s), h = (int)(bmp.Height * s);
    using var small = new Bitmap(w, h, PixelFormat.Format24bppRgb);
    using (var g = Graphics.FromImage(small))
    {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        using var attr = new ImageAttributes();
        attr.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY); // else a dark 1 px rim (edge pixels blended with black)
        g.DrawImage(bmp, new Rectangle(0, 0, w, h), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attr);
    }

    var rgb = new RgbImage(w, h);
    var bd = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int p = y * bd.Stride + x * 3, d = (y * w + x) * 3;
            rgb.Data[d] = System.Runtime.InteropServices.Marshal.ReadByte(bd.Scan0, p + 2);
            rgb.Data[d + 1] = System.Runtime.InteropServices.Marshal.ReadByte(bd.Scan0, p + 1);
            rgb.Data[d + 2] = System.Runtime.InteropServices.Marshal.ReadByte(bd.Scan0, p);
        }
    small.UnlockBits(bd);

    var det = Environment.GetEnvironmentVariable("LIVE") == "1" ? DocumentEdgeDetector.Live() : new DocumentEdgeDetector { Trace = args[0] == "1600" ? null : Console.WriteLine };
    det.Detect(rgb);
    var sw = Stopwatch.StartNew();
    QuadDetection r = det.Detect(rgb);
    sw.Stop();
    Console.WriteLine($"{Path.GetFileName(path)} {w}x{h}: detected={r.Detected} conf={r.Confidence:0.00} {sw.ElapsedMilliseconds}ms  " +
        string.Join(' ', r.Quad.ToArray().Select(p => $"({p.X:0.000},{p.Y:0.000})")));

    using var canvas = new Bitmap(small);
    using (var g = Graphics.FromImage(canvas))
    {
        var pts = r.Quad.ToArray().Select(p => new PointF((float)(p.X * w), (float)(p.Y * h))).ToArray();
        using var pen = new Pen(r.Detected ? Color.Lime : Color.Orange, Math.Max(3, w / 250f));
        g.DrawPolygon(pen, pts);
        foreach (var p in pts) g.FillEllipse(Brushes.Red, p.X - 7, p.Y - 7, 14, 14);
    }
    // Refinement at this resolution (curved sides), drawn in blue.
    Quad coarsePx = r.Quad.Scale(w, h);
    PageOutlineRefiner.Trace = s => Console.WriteLine("     " + s);
    var refined = PageOutlineRefiner.Refine(rgb, coarsePx);
    using (var g = Graphics.FromImage(canvas))
    {
        using var pen = new Pen(Color.DeepSkyBlue, Math.Max(2, w / 350f));
        PageBends bends = refined.Bends ?? PageBends.Flat;
        for (int side = 0; side < 4; side++)
        {
            var pts = Enumerable.Range(0, 41).Select(i => bends.PointOnSide(refined.Outline, side, i / 40.0))
                .Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
            g.DrawLines(pen, pts);
        }
    }
    Console.WriteLine($"   refined sides={refined.SidesRefined} bends={(refined.Bends == null ? "flat" : string.Join(",", refined.Bends.ToValues().Select(v => v.ToString("0.000"))))}");
    canvas.Save(Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "_det.png"));

    // Straightened: old (straight outline, side-length size) vs new (refined curved outline, true aspect).
    (int ow, int oh) = PerspectiveWarp.A4Size(coarsePx, 1200, long.MaxValue);
    if (!PerspectiveWarp.IsA4Like(coarsePx)) (ow, oh) = PerspectiveWarp.OutputSize(coarsePx, 1200, long.MaxValue);
    Save(PerspectiveWarp.Warp(rgb, coarsePx, ow, oh), Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "_old.png"));
    double aspect = PageGeometry.OutputAspect(refined.Outline, w, h, a4: true);
    (int nw, int nh) = PageGeometry.SizeFor(refined.Outline, aspect, 1200, long.MaxValue);
    Console.WriteLine($"   old {ow}x{oh} (side ratio {PageGeometry.SideRatio(coarsePx):0.000})  new {nw}x{nh} (true aspect {PageGeometry.TrueAspect(refined.Outline, w, h):0.000})");
    Save(PerspectiveWarp.Warp(rgb, refined.Outline, nw, nh, refined.Bends), Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "_new.png"));
}

static void Save(RgbImage img, string file)
{
    using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
    var bd = bmp.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
    var row = new byte[bd.Stride];
    for (int y = 0; y < img.Height; y++)
    {
        for (int x = 0; x < img.Width; x++)
        {
            int i = (y * img.Width + x) * 3;
            row[x * 3] = img.Data[i + 2]; row[x * 3 + 1] = img.Data[i + 1]; row[x * 3 + 2] = img.Data[i];
        }
        System.Runtime.InteropServices.Marshal.Copy(row, 0, bd.Scan0 + y * bd.Stride, bd.Stride);
    }
    bmp.UnlockBits(bd);
    bmp.Save(file);
}
