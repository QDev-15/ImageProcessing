using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// Scan / import step: a page whose DPI is higher than the scan setting is resampled down
/// to exactly that DPI. Lower or equal DPI is never touched (no upscaling). This is the
/// single biggest lever on speed and memory for everything downstream -- binarization,
/// OCR, JPEG2000 / JBIG2 encoding and the 32-bit external tools all scale with pixel count
/// (an 85 MP page becomes ~9 MP at 300 dpi).
/// </summary>
public static class ResolutionLimiter
{
    /// <summary>DPI within 2% of the target counts as already matching (avoids resampling
    /// 301 dpi tags for nothing).</summary>
    private const double Tolerance = 1.02;

    /// <summary>Coverage below which a resampled 1bpp pixel becomes paper; slightly below 50%
    /// so thin strokes survive the shrink.</summary>
    private const int BitonalThreshold = 176;

    /// <summary>
    /// Returns <paramref name="inputPath"/> when the page is already within
    /// <paramref name="targetDpi"/>; otherwise writes the reduced page (lossless: PNG, or
    /// TIFF G4 for 1bpp) into <paramref name="outputFolder"/>, deletes the input (the caller
    /// owns it: an import copy or a scanner temp file) and returns the new path.
    /// </summary>
    public static string Limit(string inputPath, int targetDpi, string outputFolder)
    {
        if (targetDpi < 50) return inputPath;

        using var perf = Perf.Scope("limit");
        using Bitmap src = ImageUtils.Load(inputPath);
        using Bitmap? reduced = ResampleToDpi(src, targetDpi);
        if (reduced == null) return inputPath;

        Directory.CreateDirectory(outputFolder);
        string outPath = ImageUtils.SaveLossless(reduced, Path.Combine(outputFolder, Guid.NewGuid().ToString("N")));
        try { File.Delete(inputPath); } catch { /* best effort */ }
        return outPath;
    }

    /// <summary>
    /// The page resampled down to <paramref name="targetDpi"/>, or null when it is already at
    /// or below it (within tolerance). Keeps the storage class: bitonal stays 1bpp, gray
    /// stays 8bpp, everything else 24bpp; the result carries the new DPI.
    /// </summary>
    public static Bitmap? ResampleToDpi(Bitmap src, int targetDpi)
    {
        if (targetDpi < 50) return null;
        (int dx, int dy) = ImageUtils.ResolveDpiXY(src);
        double sx = dx > targetDpi * Tolerance ? (double)targetDpi / dx : 1.0;
        double sy = dy > targetDpi * Tolerance ? (double)targetDpi / dy : 1.0;
        if (sx >= 1.0 && sy >= 1.0) return null;

        int nw = Math.Max(1, (int)Math.Round(src.Width * sx));
        int nh = Math.Max(1, (int)Math.Round(src.Height * sy));
        float ndx = (float)(dx * sx), ndy = (float)(dy * sy);

        using Bitmap scaled = new(nw, nh, PixelFormat.Format24bppRgb);
        scaled.SetResolution(ndx, ndy);
        using (Graphics g = Graphics.FromImage(scaled))
        {
            g.Clear(Color.White); // flattens any alpha onto paper
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var attrs = new ImageAttributes();
            attrs.SetWrapMode(WrapMode.TileFlipXY); // no dark / light fringe at the page edge
            g.DrawImage(src, new Rectangle(0, 0, nw, nh), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        Log.Info($"Resolution limited: {src.Width}x{src.Height} @{dx}x{dy} dpi -> {nw}x{nh} @{targetDpi} dpi");

        if (src.PixelFormat == PixelFormat.Format1bppIndexed)
            return Binarizer.Threshold(GdiGray.FromBitmap(scaled), BitonalThreshold).ToBitmap1bpp(ndx, ndy);
        if (IsGrayStorage(src))
            return GdiGray.FromBitmap(scaled).ToBitmap8bpp(ndx, ndy);
        return (Bitmap)scaled.Clone();
    }

    private static bool IsGrayStorage(Bitmap bmp)
    {
        if (bmp.PixelFormat != PixelFormat.Format8bppIndexed) return false;
        foreach (Color c in bmp.Palette.Entries)
            if (c.R != c.G || c.G != c.B) return false;
        return true;
    }
}
