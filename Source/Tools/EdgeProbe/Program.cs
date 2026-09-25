using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ImageCoreService;

// usage: probe <maxEdge> <out.png> <img1> [img2 ...]  -> draws the detected outline on each image
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
    using (var g = Graphics.FromImage(small)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear; g.DrawImage(bmp, 0, 0, w, h); }

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

    var det = new DocumentEdgeDetector { Trace = args[0] == "1600" ? null : Console.WriteLine };
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
    canvas.Save(Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + "_det.png"));
}
