using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

public sealed class PageProcessingOptions
{
    public bool Deskew { get; set; } = true;
    public bool CropBorders { get; set; } = true;
    public bool AutoOrient { get; set; } = true;
    public bool DetectBlank { get; set; } = true;
    public double BlankInkPercent { get; set; } = PageAnalyzer.DefaultBlankInkPercent;

    public static PageProcessingOptions FromSettings(AppSettings s) => new()
    {
        Deskew = s.Deskew,
        CropBorders = s.CropBlackBorders,
        AutoOrient = s.AutoOrient,
        DetectBlank = s.RemoveBlankPages,
        BlankInkPercent = s.BlankPageInkPercent,
    };
}

/// <summary>Outcome for one page. <see cref="OutputPath"/> is the input path itself when
/// nothing had to change (no re-encode at all).</summary>
public sealed record PageProcessResult(string OutputPath, bool IsBlank, bool Changed, string Summary);

/// <summary>
/// Automatic per-page cleanup run right after scan / import: blank detection, black-border
/// crop, deskew, upright orientation. Every changed page is written LOSSLESSLY (PNG, or
/// TIFF G4 for 1bpp) as a new file -- the original is never modified, which is what makes
/// undo trivial and keeps the single lossy encode for export time.
/// </summary>
public static class PageProcessor
{
    public static PageProcessResult Process(string inputPath, PageProcessingOptions o, OcrEngine? osd, string outputFolder)
    {
        using Bitmap src = ImageUtils.Load(inputPath);
        (int dpiX, int dpiY) = ImageUtils.ResolveDpiXY(src);
        bool wasGrayish = src.PixelFormat is PixelFormat.Format1bppIndexed or PixelFormat.Format8bppIndexed;
        GrayImage gray = GrayImage.FromBitmap(src);

        if (o.DetectBlank && Perf.Measure("proc.blank", () => PageAnalyzer.IsBlank(gray, dpiX, o.BlankInkPercent)))
            return new PageProcessResult(inputPath, true, false, "trang trắng");

        var notes = new List<string>();
        Bitmap current = src;
        try
        {
            if (o.CropBorders)
            {
                Rectangle r = Perf.Measure("proc.cropdetect", () => DocumentCleanup.DetectContentBounds(gray, dpiX));
                if (r.Width < src.Width || r.Height < src.Height)
                {
                    Replace(ref current, DocumentCleanup.Crop(current, r), src);
                    gray = GrayImage.FromBitmap(current);
                    notes.Add("cắt viền");
                }
            }

            if (o.Deskew)
            {
                double angle = Perf.Measure("proc.skewdetect", () => DocumentCleanup.DetectSkew(gray, dpiX));
                if (angle != 0)
                {
                    Replace(ref current, DocumentCleanup.RotateArbitrary(current, -angle), src);
                    notes.Add($"chỉnh nghiêng {angle:0.0}°");
                }
            }

            if (o.AutoOrient && osd != null)
            {
                int turn = Perf.Measure("proc.osd", () => osd.DetectUprightRotation(current));
                if (turn != 0)
                {
                    Replace(ref current, DocumentCleanup.RotateRight(current, turn), src);
                    notes.Add($"xoay {turn}°");
                }
            }

            if (ReferenceEquals(current, src))
                return new PageProcessResult(inputPath, false, false, "");

            // Keep bitonal / gray sources compact: the rotation / crop produced 24bpp, but
            // there is no color to preserve.
            if (wasGrayish && current.PixelFormat != PixelFormat.Format8bppIndexed)
            {
                (int nx, int ny) = ImageUtils.ResolveDpiXY(current);
                Replace(ref current, GrayImage.FromBitmap(current).ToBitmap8bpp(nx, ny), src);
            }
            if (current.HorizontalResolution < 2) current.SetResolution(dpiX, dpiY);

            Directory.CreateDirectory(outputFolder);
            string outPath = Perf.Measure("proc.save", () => ImageUtils.SaveLossless(current, Path.Combine(outputFolder, Guid.NewGuid().ToString("N"))));
            return new PageProcessResult(outPath, false, true, string.Join(", ", notes));
        }
        finally
        {
            if (!ReferenceEquals(current, src)) current.Dispose();
        }
    }

    /// <summary>Lossless 90/180/270 rotation of a page file into a new file.</summary>
    public static string RotateFile(string inputPath, int degreesClockwise, string outputFolder)
    {
        using Bitmap src = ImageUtils.Load(inputPath);
        (int dx, int dy) = ImageUtils.ResolveDpiXY(src);
        src.SetResolution(dx, dy);
        using Bitmap rotated = DocumentCleanup.RotateRight(src, degreesClockwise);
        Directory.CreateDirectory(outputFolder);
        return ImageUtils.SaveLossless(rotated, Path.Combine(outputFolder, Guid.NewGuid().ToString("N")));
    }

    private static void Replace(ref Bitmap current, Bitmap next, Bitmap original)
    {
        if (!ReferenceEquals(current, original)) current.Dispose();
        current = next;
    }
}
