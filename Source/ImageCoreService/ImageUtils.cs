using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageCoreService;

public static class ImageUtils
{
    /// <summary>
    /// Matches the main app's ArchivePageFactory.MaxColorExportDpi: color/gray pages
    /// are never embedded above this DPI on export, regardless of scan resolution.
    /// Bitonal pages are deliberately NOT capped (same as production) -- no comparable
    /// size win there, and downsampling scanned text risks legibility for no benefit.
    /// </summary>
    public const int MaxColorExportDpi = 200;

    /// <summary>
    /// Downsamples <paramref name="src"/> (bicubic) so neither axis exceeds
    /// <paramref name="maxDpi"/>, preserving aspect ratio -- same approach as
    /// ArchivePageFactory.ResampleIfAboveDpi in the main app (minus its denoise step,
    /// not requested here). Returns <paramref name="src"/> itself, unchanged, when
    /// already within the cap -- callers must not dispose the result unless
    /// `!ReferenceEquals(result, src)`.
    /// </summary>
    public static Bitmap CapDpi(Bitmap src, int maxDpi = MaxColorExportDpi)
    {
        double dpiX = src.HorizontalResolution > 0 ? src.HorizontalResolution : 200;
        double dpiY = src.VerticalResolution > 0 ? src.VerticalResolution : 200;
        if (dpiX <= maxDpi && dpiY <= maxDpi)
            return src;

        double scale = Math.Min(maxDpi / dpiX, maxDpi / dpiY);
        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));

        var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        dst.SetResolution((float)(dpiX * scale), (float)(dpiY * scale));
        using (Graphics g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, w, h);
        }
        return dst;
    }

    /// <summary>
    /// Fixed-threshold (128) luminance-to-1bpp conversion. GDI+'s default 1bpp palette
    /// is index 0 = black, index 1 = white, so the bit is set (1) for light/paper
    /// pixels and left clear (0) for ink -- matching the fix already applied in the
    /// main app's TiffUtils.To1bpp after an inverted-color bug there (see Goal.md
    /// "Bug thu 2" 2026-09-17). Good enough for a test bench; the main app uses Otsu
    /// (CvUtils.ToBitonal) for a content-adaptive threshold instead of a fixed one.
    /// </summary>
    public static Bitmap ToBitonal(Bitmap src)
    {
        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format1bppIndexed);
        dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);

        using Bitmap rgb = src.PixelFormat == PixelFormat.Format24bppRgb ? src : To24bpp(src);

        BitmapData srcData = rgb.LockBits(new Rectangle(0, 0, rgb.Width, rgb.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        BitmapData dstData = dst.LockBits(new Rectangle(0, 0, dst.Width, dst.Height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
        try
        {
            unsafe
            {
                for (int y = 0; y < rgb.Height; y++)
                {
                    byte* srcRow = (byte*)srcData.Scan0 + y * srcData.Stride;
                    byte* dstRow = (byte*)dstData.Scan0 + y * dstData.Stride;
                    for (int x = 0; x < rgb.Width; x++)
                    {
                        byte b = srcRow[x * 3 + 0], g = srcRow[x * 3 + 1], r = srcRow[x * 3 + 2];
                        int luminance = (r * 299 + g * 587 + b * 114) / 1000;
                        if (luminance >= 128)
                            dstRow[x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                }
            }
        }
        finally
        {
            rgb.UnlockBits(srcData);
            dst.UnlockBits(dstData);
        }
        return dst;
    }

    public static Bitmap To24bpp(Bitmap src)
    {
        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);
        using Graphics g = Graphics.FromImage(dst);
        g.Clear(Color.White);
        g.DrawImage(src, 0, 0, src.Width, src.Height);
        return dst;
    }

    public static int ResolveDpi(Bitmap bmp)
    {
        int dpi = (int)Math.Round(bmp.HorizontalResolution);
        return dpi > 0 ? dpi : 200;
    }
}
