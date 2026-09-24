using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

public static class ImageUtils
{
    /// <summary>
    /// Common paper sizes in inches (portrait), used to reconstruct a sane DPI from pixel
    /// dimensions alone. Deliberately only A4 for the whole ISO 216 series: every ISO A size
    /// shares the same 1:sqrt(2) aspect ratio, so aspect-ratio matching cannot tell A4 from
    /// A3/A5 -- including them lets pixel-rounding noise flip the match between pages of the
    /// same document (observed on NoneGD2.pdf: 941 vs 1327 dpi for one uniform scan job).
    /// </summary>
    private static readonly (double WidthIn, double HeightIn)[] StandardPageSizesIn =
    {
        (8.27, 11.69), // A4 (and every other ISO A-series size, aspect-ratio-wise)
        (8.5, 11.0),   // US Letter
        (8.5, 14.0),   // US Legal
    };

    /// <summary>
    /// Loads an image WITHOUT keeping the file locked (so pages can be rotated / replaced /
    /// deleted while shown), preserving the original pixel format and DPI tags.
    /// </summary>
    public static Bitmap Load(string path)
    {
        // GDI+ needs the stream alive for the bitmap's lifetime; a MemoryStream owned by
        // nobody else is fine (collected together with the bitmap).
        var ms = new MemoryStream(File.ReadAllBytes(path));
        return new Bitmap(ms);
    }

    /// <summary>
    /// The DPI to use for a page. Keeps the file's own resolution tag (never resamples);
    /// when the file has no usable tag -- GDI+ reports 0, or its 96 dpi default for files
    /// without one (PNG without pHYs, many JPEGs) on an image far too large to be a 96 dpi
    /// page -- derives it from the pixel size against A4 / Letter / Legal.
    /// </summary>
    public static (int X, int Y) ResolveDpiXY(Bitmap bmp)
    {
        int dx = (int)Math.Round(bmp.HorizontalResolution);
        int dy = (int)Math.Round(bmp.VerticalResolution);
        if (IsMissingDpi(dx, bmp.Width, bmp.Height) || IsMissingDpi(dy, bmp.Width, bmp.Height))
        {
            int est = EstimateDpiFromPixels(bmp.Width, bmp.Height);
            return (est, est);
        }
        return (dx, dy);
    }

    public static int ResolveDpi(Bitmap bmp) => ResolveDpiXY(bmp).X;

    private static bool IsMissingDpi(int dpi, int widthPx, int heightPx)
    {
        if (dpi <= 1) return true;
        // 96 / 72 are what GDI+ and most tools write when nothing better is known. A real
        // 96 dpi page is at most ~1650 px on its long side (Legal); anything bigger with
        // such a tag is a scan whose tag got lost or defaulted.
        if ((dpi == 96 || dpi == 72) && Math.Max(widthPx, heightPx) > 14 * dpi + 50) return true;
        return false;
    }

    /// <summary>DPI such that the image fills the best-matching standard paper size
    /// (A4 / Letter / Legal, either orientation).</summary>
    public static int EstimateDpiFromPixels(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return 200;

        double aspect = (double)pixelWidth / pixelHeight;
        double bestDiff = double.MaxValue;
        double bestWidthIn = 8.27;
        foreach ((double w, double h) in StandardPageSizesIn)
        {
            foreach ((double widthIn, double heightIn) in new[] { (w, h), (h, w) })
            {
                double diff = Math.Abs(widthIn / heightIn - aspect);
                if (diff < bestDiff) { bestDiff = diff; bestWidthIn = widthIn; }
            }
        }
        int dpi = (int)Math.Round(pixelWidth / bestWidthIn);
        return dpi > 0 ? dpi : 200;
    }

    /// <summary>
    /// Adaptive gray -> 1bpp conversion (see <see cref="Binarizer"/>), with optional
    /// despeckle. Replaces the old fixed-threshold-128 conversion. Keeps DPI.
    /// </summary>
    public static Bitmap ToBitonal(Bitmap src, BinarizationMethod method = BinarizationMethod.Sauvola,
        bool despeckle = false, double sauvolaK = Binarizer.DefaultSauvolaK)
    {
        (int dpiX, int dpiY) = ResolveDpiXY(src);
        GrayImage bin = ToBinaryGray(src, method, despeckle, sauvolaK);
        return bin.ToBitmap1bpp(dpiX, dpiY);
    }

    /// <summary>Same as <see cref="ToBitonal"/> but returns the binary buffer (0 = ink).</summary>
    public static GrayImage ToBinaryGray(Bitmap src, BinarizationMethod method, bool despeckle, double sauvolaK = Binarizer.DefaultSauvolaK)
    {
        int dpi = ResolveDpi(src);
        GrayImage gray = GrayImage.FromBitmap(src);
        GrayImage bin = src.PixelFormat == PixelFormat.Format1bppIndexed
            ? Binarizer.Threshold(gray, 127) // already binary: keep exactly
            : Binarizer.Binarize(gray, method, dpi, sauvolaK);
        if (despeckle) DocumentCleanup.Despeckle(bin, DocumentCleanup.DefaultSpeckleArea(dpi));
        return bin;
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

    /// <summary>Lossless save choosing the right container: 1bpp -> TIFF CCITT G4,
    /// anything else -> PNG. Used for every intermediate page file.</summary>
    public static string SaveLossless(Bitmap bmp, string pathWithoutExtension)
    {
        if (bmp.PixelFormat == PixelFormat.Format1bppIndexed)
        {
            string path = pathWithoutExtension + ".tif";
            ImageCodecInfo enc = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Tiff.Guid);
            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4);
            bmp.Save(path, enc, ep);
            return path;
        }
        else
        {
            string path = pathWithoutExtension + ".png";
            bmp.Save(path, ImageFormat.Png);
            return path;
        }
    }

    /// <summary>Small preview image for thumbnails (keeps aspect ratio, white letterbox).</summary>
    public static Bitmap MakeThumbnail(Bitmap src, int boxW, int boxH)
    {
        double scale = Math.Min((double)boxW / src.Width, (double)boxH / src.Height);
        int w = Math.Max(1, (int)(src.Width * scale)), h = Math.Max(1, (int)(src.Height * scale));
        var dst = new Bitmap(boxW, boxH, PixelFormat.Format24bppRgb);
        using Graphics g = Graphics.FromImage(dst);
        g.Clear(Color.White);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        g.DrawImage(src, (boxW - w) / 2, (boxH - h) / 2, w, h);
        g.DrawRectangle(Pens.Silver, 0, 0, boxW - 1, boxH - 1);
        return dst;
    }
}
