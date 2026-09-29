using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using ImageCoreService;

/// <summary>
/// Contact sheet of a document copied from the phone (doc.json + {pageId}_proxy.jpg + {pageId}_cropped_N.jpg): for each
/// page the photo with its saved outline (green = detected, orange = fallback, blue = by hand) next to the straightened
/// page as saved, and next to the outline the detector finds now (red) and the refined one (blue dashed).
/// usage: probe sheet &lt;docDir&gt; &lt;out.png&gt;
/// </summary>
internal static class SheetProbe
{
    public static void Run(string dir, string output)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "doc.json")));
        var pages = doc.RootElement.GetProperty("pages").EnumerateArray().ToList();
        int cell = int.TryParse(Environment.GetEnvironmentVariable("CELL"), out int cs) ? cs : 360;
        using var sheet = new Bitmap(cell * 5, cell * pages.Count, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(40, 40, 40));
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        var detector = new DocumentEdgeDetector();
        for (int i = 0; i < pages.Count; i++)
        {
            JsonElement p = pages[i];
            string id = p.GetProperty("id").GetString()!;
            string proxy = Path.Combine(dir, id + "_proxy.jpg");
            if (!File.Exists(proxy)) continue;
            using var photo = new Bitmap(proxy);
            double s = Math.Min((double)cell / photo.Width, (double)cell / photo.Height);
            int w = (int)(photo.Width * s), h = (int)(photo.Height * s);
            int y0 = i * cell;

            // 1: saved outline
            g.DrawImage(photo, 0, y0, w, h);
            double[] q = p.GetProperty("cropQuad").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            bool manual = p.GetProperty("cropManual").GetBoolean(), detected = p.GetProperty("cropDetected").GetBoolean();
            using (var pen = new Pen(manual ? Color.DeepSkyBlue : detected ? Color.Lime : Color.Orange, 3))
                g.DrawPolygon(pen, Points(q, 0, y0, w, h));
            g.DrawString($"{i + 1} conf {p.GetProperty("cropConfidence").GetDouble():0.00}", SystemFonts.DefaultFont, Brushes.Yellow, 4, y0 + 4);

            // 2: detector now on this proxy (red) + refined at 1600 (blue)
            // As the app does now (CropDetectionService.ForDetector): the whole proxy, shrunk here with RgbImage.Resize.
            RgbImage big = ToRgb(photo, 1600);
            double ds = Math.Min(1.0, 480.0 / Math.Max(big.Width, big.Height));
            RgbImage rgb = ds >= 1 ? big : big.Resize((int)Math.Round(big.Width * ds), (int)Math.Round(big.Height * ds));
            QuadDetection now = detector.Detect(rgb);
            g.DrawImage(photo, cell, y0, w, h);
            using (var pen = new Pen(now.Detected ? Color.Red : Color.Orange, 3))
                g.DrawPolygon(pen, Points(now.Quad.ToValues(), cell, y0, w, h));
            if (now.Detected)
            {

                bool trace = Environment.GetEnvironmentVariable("TRACE") == "1";
                if (trace)
                {
                    Console.WriteLine($"page {i + 1} {big.Width}x{big.Height} coarse {string.Join(" ", now.Quad.Scale(big.Width, big.Height).ToValues().Select(v => v.ToString("0")))}");
                    PageOutlineRefiner.Trace = m => Console.WriteLine("  " + m);
                }
                var refined = PageOutlineRefiner.Refine(big, now.Quad.Scale(big.Width, big.Height));
                PageOutlineRefiner.Trace = null;
                if (trace) Console.WriteLine($"  refined {string.Join(" ", refined.Outline.ToValues().Select(v => v.ToString("0")))}");
                double[] rv = refined.Outline.Scale(1.0 / big.Width, 1.0 / big.Height).ToValues();
                using var pen = new Pen(Color.DeepSkyBlue, 2) { DashStyle = DashStyle.Dash };
                g.DrawPolygon(pen, Points(rv, cell, y0, w, h));
            }
            g.DrawString($"now {now.Confidence:0.00}", SystemFonts.DefaultFont, Brushes.Yellow, cell + 4, y0 + 4);
            if (now.Detected)
            {
                var rf = PageOutlineRefiner.Refine(big, now.Quad.Scale(big.Width, big.Height));
                (int ow, int oh) = PerspectiveWarp.A4Size(rf.Outline, 1600, 4_000_000);
                RgbImage flat = PerspectiveWarp.Warp(big, rf.Outline, ow, oh, rf.Bends);
                if (Environment.GetEnvironmentVariable("TRACE") == "1") ContentAligner.Trace = m => Console.WriteLine("  " + m);
                LineTilt? tilt = ContentAligner.Measure(flat);
                ContentAligner.Trace = null;
                RgbImage level = ContentAligner.Align(flat, tilt);
                DrawRgb(g, flat, 3 * cell, y0, cell);
                DrawRgb(g, level, 4 * cell, y0, cell);
                if (Environment.GetEnvironmentVariable("PAGE_OUT") is { } pageOut)
                {
                    SaveRgb(flat, Path.Combine(pageOut, $"p{i + 1}_flat.png"));
                    SaveRgb(level, Path.Combine(pageOut, $"p{i + 1}_level.png"));
                }
                string tt = tilt is { } t ? $"lean {t.At(0.1):0.00}..{t.At(0.9):0.00}" : "level";
                g.DrawString(tt, SystemFonts.DefaultFont, Brushes.Red, 4 * cell + 4, y0 + 4);
                Console.WriteLine($"page {i + 1}: {tt}");
            }

            // 3: saved render
            string? render = Directory.EnumerateFiles(dir, id + "_cropped_*.jpg").FirstOrDefault();
            if (render != null)
            {
                using var r = new Bitmap(render);
                double rs = Math.Min((double)cell / r.Width, (double)cell / r.Height);
                g.DrawImage(r, 2 * cell, y0, (int)(r.Width * rs), (int)(r.Height * rs));
            }
        }
        sheet.Save(output);
    }

    private static void SaveRgb(RgbImage img, string path)
    {
        using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp)) DrawRgb(g, img, 0, 0, Math.Max(img.Width, img.Height));
        bmp.Save(path);
    }

    private static void DrawRgb(Graphics g, RgbImage img, int x0, int y0, int cell)
    {
        using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        var row = new byte[bd.Stride];
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                int s = (y * img.Width + x) * 3;
                row[x * 3] = img.Data[s + 2]; row[x * 3 + 1] = img.Data[s + 1]; row[x * 3 + 2] = img.Data[s];
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, bd.Scan0 + y * bd.Stride, bd.Stride);
        }
        bmp.UnlockBits(bd);
        double rs = Math.Min((double)cell / img.Width, (double)cell / img.Height);
        g.DrawImage(bmp, x0, y0, (int)(img.Width * rs), (int)(img.Height * rs));
    }

    private static PointF[] Points(double[] q, int x0, int y0, int w, int h) =>
        Enumerable.Range(0, 4).Select(k => new PointF(x0 + (float)(q[2 * k] * w), y0 + (float)(q[2 * k + 1] * h))).ToArray();

    /// <summary>ANDROIDLIKE=1: scale the way the app does on the phone (power-of-two decode, then plain bilinear).</summary>
    private static readonly bool AndroidLike = Environment.GetEnvironmentVariable("ANDROIDLIKE") == "1";

    public static RgbImage ToRgb(Bitmap bmp, int longEdge)
    {
        double s = Math.Min(1.0, (double)longEdge / Math.Max(bmp.Width, bmp.Height));
        int w = (int)(bmp.Width * s), h = (int)(bmp.Height * s);
        Bitmap source = bmp;
        if (AndroidLike)
        {
            int sample = 1;
            while (Math.Max(bmp.Width, bmp.Height) / (sample * 2) >= longEdge) sample *= 2;
            if (sample > 1)
            {
                source = new Bitmap(bmp.Width / sample, bmp.Height / sample, PixelFormat.Format24bppRgb);
                using var gs = Graphics.FromImage(source);
                gs.InterpolationMode = InterpolationMode.HighQualityBilinear; // the JPEG decoder averages the blocks
                using var a = new ImageAttributes();
                a.SetWrapMode(WrapMode.TileFlipXY);
                gs.DrawImage(bmp, new Rectangle(0, 0, source.Width, source.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, a);
            }
        }
        using var small = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(small))
        {
            // TileFlipXY: without it GDI+ blends the edge pixels with black, and the dark 1 px rim reads as a sheet edge.
            g.InterpolationMode = AndroidLike ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear;
            bmp = source;
            using var attr = new ImageAttributes();
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(bmp, new Rectangle(0, 0, w, h), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attr);
        }
        var rgb = new RgbImage(w, h);
        var bd = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var row = new byte[bd.Stride];
        for (int y = 0; y < h; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, bd.Stride);
            for (int x = 0; x < w; x++)
            {
                int d = (y * w + x) * 3;
                rgb.Data[d] = row[x * 3 + 2]; rgb.Data[d + 1] = row[x * 3 + 1]; rgb.Data[d + 2] = row[x * 3];
            }
        }
        small.UnlockBits(bd);
        return rgb;
    }
}
