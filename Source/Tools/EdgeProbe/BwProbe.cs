using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using ImageCoreService;

/// <summary>
/// Black-and-white quality on a simulated phone photo of a text page: renders an A4 page at 300 DPI, degrades it the
/// way a camera does (smaller, lens blur, uneven light, noise, JPEG), scales it back to 300 DPI like the warp, then
/// binarizes it with the old and the candidate settings. Writes 100 % crops and a phone-screen-size view of each, plus
/// the PNG size, so the settings can be compared by eye.
/// usage: probe bw &lt;outDir&gt;
/// </summary>
internal static class BwProbe
{
    public static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        const int W = 2480, H = 3508, Dpi = 300;
        GrayImage truth = RenderPage(W, H);
        GrayImage photo = Camera(truth, 0.72, 1.1, 4);
        Save(truth, Path.Combine(outDir, "0_truth"));
        Save(photo, Path.Combine(outDir, "1_photo"));

        GrayImage flat = BackgroundFlattener.Flatten(photo);
        int window = Binarizer.DefaultWindow(Dpi);
        double k = DocumentFilter.SauvolaKFor(50);

        var variants = new (string Name, Func<GrayImage> Make)[]
        {
            ("2_old_hard", () => Despeckled(Binarizer.Sauvola(flat, window, k))),
            ("3_soft12", () => Binarizer.Sauvola(flat, window, k, 0, 12)),
            ("4_sharp_soft12", () => Binarizer.Sauvola(Sharp(flat, 0.8), window, k, 0, 12)),
            ("5_sharp_soft20", () => Binarizer.Sauvola(Sharp(flat, 0.8), window, k, 0, 20)),
            ("6_sharp15_soft16", () => Binarizer.Sauvola(Sharp(flat, 1.5), window, k, 0, 16)),
            ("7_sharp_hard", () => Despeckled(Binarizer.Sauvola(Sharp(flat, 0.8), window, k))),
            ("8_sharp_soft12_q4", () => Quantize(Binarizer.Sauvola(Sharp(flat, 0.8), window, k, 0, 12), 4)),
        };
        foreach ((string name, Func<GrayImage> make) in variants)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            GrayImage img = make();
            long ms = sw.ElapsedMilliseconds;
            int png = (name.Contains("hard") ? PngWriter.EncodeBilevel(img) : PngWriter.EncodeGray8(img)).Length;
            (double err, double faint) = Compare(img, truth);
            Console.WriteLine($"{name,-20} {ms,5} ms  png {png / 1024,5} KB  mismatch {err:P2}  lost strokes {faint:P1}");
            Save(img, Path.Combine(outDir, name));
        }
    }

    private static GrayImage Sharp(GrayImage g, double amount)
    {
        var copy = new GrayImage(g.Width, g.Height, (byte[])g.Data.Clone());
        Sharpen.UnsharpInPlace(copy, Sharpen.RadiusFor(300), amount);
        return copy;
    }

    private static GrayImage Quantize(GrayImage g, int levels)
    {
        for (int i = 0; i < g.Data.Length; i++) g.Data[i] = (byte)(Math.Round(g.Data[i] * (levels - 1) / 255.0) * 255 / (levels - 1));
        return g;
    }

    private static GrayImage Despeckled(GrayImage bin)
    {
        DocumentCleanup.Despeckle(bin, DocumentCleanup.DefaultSpeckleArea(300));
        return bin;
    }

    /// <summary>Share of pixels whose black / white (at 128) differs from the truth, and share of ink lost.</summary>
    private static (double Err, double Lost) Compare(GrayImage img, GrayImage truth)
    {
        long diff = 0, ink = 0, lost = 0;
        for (int i = 0; i < truth.Data.Length; i++)
        {
            bool t = truth.Data[i] < 128, o = img.Data[i] < 128;
            if (t != o) diff++;
            if (t) { ink++; if (!o) lost++; }
        }
        return ((double)diff / truth.Data.Length, ink == 0 ? 0 : (double)lost / ink);
    }

    private static GrayImage RenderPage(int w, int h)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            string text = "Điều 4. Máy in laser, trừ loại đa màu có tốc độ in từ 60 tờ/phút (khổ A4) trở xuống hoặc có khổ in từ A3 " +
                          "trở xuống được bãi bỏ theo quy định tại điểm c khoản 2 Thông tư số 11/2024/TT-BTTTT ngày 23 tháng 9 năm 2024 " +
                          "của Bộ trưởng Bộ Thông tin và Truyền thông sửa đổi, bổ sung một số điều của Thông tư số 03/2015/TT-BTTTT.";
            float y = 150;
            foreach (float pt in new[] { 12f, 10f, 9f, 8f, 7f })
            {
                using var font = new Font("Times New Roman", pt * 300 / 72f, GraphicsUnit.Pixel);
                for (int line = 0; line < 6; line++)
                {
                    g.DrawString(text, font, Brushes.Black, new RectangleF(180, y, w - 360, font.Height * 3));
                    y += font.Height * 3.2f;
                }
                y += 40;
            }
            using var pen = new Pen(Color.Black, 3);
            g.DrawRectangle(pen, 180, y, w - 360, 300);
            g.DrawLine(pen, 180, y + 100, w - 180, y + 100);
        }
        return ToGray(bmp);
    }

    /// <summary>What a phone does to the page: shrink to <paramref name="scale"/>, blur, light falling off to one side,
    /// sensor noise, JPEG; then back up to the page size (the warp to 300 DPI).</summary>
    private static GrayImage Camera(GrayImage page, double scale, double blurSigma, double noise)
    {
        int sw = (int)(page.Width * scale), sh = (int)(page.Height * scale);
        GrayImage small = Resize(page, sw, sh, InterpolationMode.HighQualityBicubic);
        small = GaussBlur(small, blurSigma);
        var rnd = new Random(7);
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                int i = y * sw + x;
                double light = 0.93 - 0.18 * x / sw - 0.08 * y / sh;           // uneven light
                double v = 18 + (small.Data[i] - 0) * light * 0.97;              // ink not pure black, paper grayish
                v += (rnd.NextDouble() + rnd.NextDouble() + rnd.NextDouble() - 1.5) * noise * 1.4;
                small.Data[i] = (byte)Math.Clamp(v, 0, 255);
            }
        small = Jpeg(small, 88);
        return Resize(small, page.Width, page.Height, InterpolationMode.Bilinear);
    }

    private static GrayImage GaussBlur(GrayImage src, double sigma)
    {
        int r = (int)Math.Ceiling(sigma * 3);
        var kernel = Enumerable.Range(-r, 2 * r + 1).Select(i => Math.Exp(-i * i / (2 * sigma * sigma))).ToArray();
        double sum = kernel.Sum();
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        int w = src.Width, h = src.Height;
        var tmp = new double[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double a = 0;
                for (int k = -r; k <= r; k++) a += kernel[k + r] * src.Data[y * w + Math.Clamp(x + k, 0, w - 1)];
                tmp[y * w + x] = a;
            }
        });
        var dst = new GrayImage(w, h);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double a = 0;
                for (int k = -r; k <= r; k++) a += kernel[k + r] * tmp[Math.Clamp(y + k, 0, h - 1) * w + x];
                dst.Data[y * w + x] = (byte)Math.Clamp(a + 0.5, 0, 255);
            }
        });
        return dst;
    }

    private static GrayImage Jpeg(GrayImage img, long quality)
    {
        using var bmp = ToBitmap(img);
        using var ms = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        bmp.Save(ms, codec, p);
        ms.Position = 0;
        using var back = new Bitmap(ms);
        return ToGray(back);
    }

    private static GrayImage Resize(GrayImage img, int w, int h, InterpolationMode mode)
    {
        using var src = ToBitmap(img);
        using var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = mode;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var attr = new ImageAttributes();
            attr.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
        }
        return ToGray(dst);
    }

    private static GrayImage ToGray(Bitmap bmp)
    {
        var g = new GrayImage(bmp.Width, bmp.Height);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var row = new byte[bd.Stride];
        for (int y = 0; y < bmp.Height; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, bd.Stride);
            for (int x = 0; x < bmp.Width; x++) g.Data[y * bmp.Width + x] = (byte)((row[x * 3] * 29 + row[x * 3 + 1] * 150 + row[x * 3 + 2] * 77) >> 8);
        }
        bmp.UnlockBits(bd);
        return g;
    }

    private static Bitmap ToBitmap(GrayImage img)
    {
        var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        var row = new byte[bd.Stride];
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++) row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = img.Data[y * img.Width + x];
            System.Runtime.InteropServices.Marshal.Copy(row, 0, bd.Scan0 + y * bd.Stride, bd.Stride);
        }
        bmp.UnlockBits(bd);
        return bmp;
    }

    /// <summary>A 100 % crop of small text, and the whole page at phone-screen width drawn the way an ImageView scales
    /// (bilinear, no mipmaps).</summary>
    private static void Save(GrayImage img, string path)
    {
        using var full = ToBitmap(img);
        using (var crop = full.Clone(new Rectangle(180, 1700, 900, 500), full.PixelFormat)) crop.Save(path + "_crop.png");
        int sw = 1080, sh = (int)((long)img.Height * sw / img.Width);
        using var screen = new Bitmap(sw, sh, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(screen))
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(full, 0, 0, sw, sh);
        }
        using (var part = screen.Clone(new Rectangle(0, 0, sw, 900), screen.PixelFormat)) part.Save(path + "_screen.png");
    }
}
