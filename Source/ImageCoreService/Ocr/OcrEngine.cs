using System.Drawing;
using System.Drawing.Imaging;
using Tesseract;

namespace ImageCoreService;

/// <summary>One recognized word, in image pixel coordinates (origin top-left).</summary>
public sealed record OcrWord(string Text, int X, int Y, int Width, int Height, int BaselineY, float Confidence);

/// <summary>
/// Tesseract 5 (Apache-2.0) wrapper: word-level OCR for the PDF text layer and OSD page
/// orientation detection. Language data comes from tessdata\ next to the exe
/// (tessdata_fast vie + eng, tessdata osd). An instance holds loaded models (~1 s to
/// create), so reuse one per batch; NOT thread-safe.
/// </summary>
public sealed class OcrEngine : IDisposable
{
    public static string TessDataPath => Path.Combine(AppContext.BaseDirectory, "tessdata");

    private readonly TesseractEngine? _ocr;
    private readonly string _languages;
    private TesseractEngine? _osd;

    public OcrEngine(string languages = "vie+eng", bool loadOcr = true)
    {
        _languages = languages;
        if (loadOcr)
            _ocr = new TesseractEngine(TessDataPath, languages, EngineMode.LstmOnly);
    }

    public static bool IsLanguageAvailable(string languages) =>
        languages.Split('+', StringSplitOptions.RemoveEmptyEntries)
                 .All(l => File.Exists(Path.Combine(TessDataPath, l + ".traineddata")));

    public IReadOnlyList<OcrWord> Recognize(Bitmap page)
    {
        if (_ocr == null) throw new InvalidOperationException("OCR engine was created without OCR models.");

        using Pix pix = ToPix(page);
        using Tesseract.Page result = _ocr.Process(pix, PageSegMode.Auto);
        var words = new List<OcrWord>();
        using ResultIterator it = result.GetIterator();
        it.Begin();
        do
        {
            if (!it.TryGetBoundingBox(PageIteratorLevel.Word, out Rect box)) continue;
            string? text = it.GetText(PageIteratorLevel.Word)?.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            float conf = it.GetConfidence(PageIteratorLevel.Word);
            int baseline = box.Y2;
            if (it.TryGetBaseline(PageIteratorLevel.Word, out Rect bl))
                baseline = (bl.Y1 + bl.Y2) / 2;
            words.Add(new OcrWord(text.Normalize(System.Text.NormalizationForm.FormC), box.X1, box.Y1, box.Width, box.Height, baseline, conf));
        }
        while (it.Next(PageIteratorLevel.Word));
        return words;
    }

    /// <summary>
    /// Clockwise rotation (0/90/180/270) that makes the page upright, or 0 when OSD is not
    /// confident (few characters, pictures, blank page) or osd.traineddata is missing.
    /// </summary>
    public int DetectUprightRotation(Bitmap page, float minConfidence = 2.0f)
    {
        if (!File.Exists(Path.Combine(TessDataPath, "osd.traineddata"))) return 0;
        try
        {
            _osd ??= new TesseractEngine(TessDataPath, "osd", EngineMode.TesseractOnly);
            // OSD only needs to see text-line shapes: ~150 dpi is plenty and several times
            // faster than full scan resolution.
            using Bitmap small = DownscaleForOsd(page);
            using Pix pix = ToPix(small);
            using Tesseract.Page result = _osd.Process(pix, PageSegMode.OsdOnly);
            result.DetectBestOrientation(out int orientationDeg, out float confidence);
            if (confidence < minConfidence) return 0;
            // Tesseract reports how far the page is rotated clockwise (verified in SmokeTests:
            // a page turned 90 deg clockwise reports 90); undo it with the opposite turn.
            return (360 - ((orientationDeg % 360) + 360) % 360) % 360;
        }
        catch (Exception ex)
        {
            Log.Warn("OSD orientation detection failed; leaving page as is.", ex);
            return 0;
        }
    }

    private static Bitmap DownscaleForOsd(Bitmap page)
    {
        int dpi = ImageUtils.ResolveDpi(page);
        int factor = Math.Max(1, dpi / 150);
        GrayImage gray = GdiGray.FromBitmap(page);
        if (factor > 1) gray = gray.Downscale(factor);
        return gray.ToBitmap8bpp(dpi / (float)factor, dpi / (float)factor);
    }

    private static Pix ToPix(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        // 1bpp stays 1bpp (TIFF G4 is compact); everything else PNG. Both keep DPI, which
        // Tesseract uses for its size heuristics.
        if (bmp.PixelFormat == PixelFormat.Format1bppIndexed)
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Tiff);
        else
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Pix.LoadFromMemory(ms.ToArray());
    }

    public void Dispose()
    {
        _ocr?.Dispose();
        _osd?.Dispose();
    }

    public override string ToString() => $"OcrEngine({_languages})";
}
