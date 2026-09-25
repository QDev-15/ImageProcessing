using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>Everything that shapes an export. Build from settings with <see cref="FromSettings"/>.</summary>
public sealed class ExportOptions
{
    /// <summary>false -> CCITT G4 for bitonal pages.</summary>
    public bool UseJBig2 { get; set; } = true;
    /// <summary>false -> JPEG for gray / color pages.</summary>
    public bool UseJpeg2000 { get; set; } = true;
    public JBig2Mode JBig2Mode { get; set; } = JBig2Mode.Symbol;
    public double JBig2Threshold { get; set; } = 0.85;
    public int JpegQuality { get; set; } = 60;
    /// <summary>0 = lossless.</summary>
    public double Jpeg2000Ratio { get; set; } = 40;
    public bool PassThroughOriginalJpeg { get; set; } = false;

    public ColorOutputMode ColorMode { get; set; } = ColorOutputMode.Auto;
    public BinarizationMethod Binarization { get; set; } = BinarizationMethod.Sauvola;
    public double SauvolaK { get; set; } = Binarizer.DefaultSauvolaK;
    public bool Despeckle { get; set; } = true;

    public bool PdfA { get; set; } = true;
    public bool Ocr { get; set; }
    public string OcrLanguages { get; set; } = "vie+eng";
    public PdfMetadata Metadata { get; set; } = new();

    public static ExportOptions FromSettings(AppSettings s) => new()
    {
        UseJBig2 = s.UseJBig2,
        UseJpeg2000 = s.UseJpeg2000,
        JBig2Mode = s.JBig2Mode,
        JBig2Threshold = s.JBig2Threshold,
        JpegQuality = s.JpegQuality,
        Jpeg2000Ratio = s.Jpeg2000Ratio,
        PassThroughOriginalJpeg = s.PassThroughOriginalJpeg,
        ColorMode = s.ColorMode,
        Binarization = s.Binarization,
        SauvolaK = s.SauvolaK,
        Despeckle = s.Despeckle,
        PdfA = s.PdfA,
        Ocr = s.Ocr,
        OcrLanguages = s.OcrLanguages,
        Metadata = new PdfMetadata
        {
            Title = s.MetaTitle,
            Author = s.MetaAuthor,
            Subject = s.MetaSubject,
            Keywords = s.MetaKeywords,
        },
    };
}

/// <summary>Progress report: page <see cref="Current"/> of <see cref="Total"/> (1-based).</summary>
public readonly record struct WorkProgress(int Current, int Total, string Message);

/// <summary>
/// Multi-page PDF / TIFF export of a list of page image files. UI-free, so any front end
/// (the WinForms app, a service, a CLI) gets the same pipeline.
///
/// Per page: keep the native resolution (no resampling -- the DPI only sets the physical
/// page size), decide bitonal / gray / color (auto-detected or forced), then encode once:
///   bitonal     -> CCITT G4, or JBIG2 when enabled (adaptive Sauvola/Otsu binarization + despeckle)
///   gray/color  -> JPEG, or JPEG2000 (OpenJPEG) when enabled; an original JPEG file is
///                  embedded byte-for-byte when possible (no second lossy generation).
/// </summary>
public static class DocumentExporter
{
    private sealed class Prepared
    {
        public PageColorKind Kind;
        public int Width, Height, DpiX, DpiY;
        public byte[]? Bytes;
        public bool IsJpx, IsJBig2;
        public byte[]? JBig2Globals;
        public string? TempBitonalPath; // for batched JBIG2 symbol coding
        public int HSampling = 2, VSampling = 2, Components = 3;
        public IReadOnlyList<OcrWord>? Words;
    }

    public static void ExportPdf(IReadOnlyList<string> pageFiles, ExportOptions options, string destPath,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        var temps = new List<string>();
        OcrEngine? ocr = null;
        try
        {
            if (options.Ocr)
            {
                if (OcrEngine.IsLanguageAvailable(options.OcrLanguages))
                    ocr = new OcrEngine(options.OcrLanguages);
                else
                    Log.Warn($"OCR skipped: language data '{options.OcrLanguages}' not found in {OcrEngine.TessDataPath}");
            }

            var prepared = new List<Prepared>(pageFiles.Count);
            for (int i = 0; i < pageFiles.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                progress?.Report(new WorkProgress(i + 1, pageFiles.Count, $"Mã hoá trang {i + 1}/{pageFiles.Count}"));
                prepared.Add(PreparePage(pageFiles[i], options, forPdf: true, ocr, temps));
            }

            // JBIG2 symbol mode: one jbig2.exe run over every bitonal page so glyphs repeated
            // ACROSS pages share one dictionary (embedded once, referenced by every page).
            var symbolPages = prepared.Where(p => p.TempBitonalPath != null).ToList();
            if (symbolPages.Count > 0)
            {
                cancel.ThrowIfCancellationRequested();
                progress?.Report(new WorkProgress(pageFiles.Count, pageFiles.Count, "JBIG2: mã hoá từ điển ký tự..."));
                JBig2Encoder.Result[] results = JBig2Encoder.EncodeSymbolMultiPage(
                    symbolPages.Select(p => p.TempBitonalPath!).ToList(), options.JBig2Threshold);
                for (int i = 0; i < symbolPages.Count; i++)
                {
                    symbolPages[i].Bytes = results[i].PageStream;
                    symbolPages[i].JBig2Globals = results[i].GlobalsStream;
                }
            }

            cancel.ThrowIfCancellationRequested();
            progress?.Report(new WorkProgress(pageFiles.Count, pageFiles.Count, "Ghi file PDF..."));
            var builder = new PdfBuilder { PdfA = options.PdfA, Metadata = options.Metadata };
            if (string.IsNullOrEmpty(builder.Metadata.Title))
                builder.Metadata = CloneWithTitle(options.Metadata, Path.GetFileNameWithoutExtension(destPath));

            foreach (Prepared p in prepared)
            {
                if (p.IsJBig2)
                    builder.AddJBig2Page(p.Bytes!, p.JBig2Globals, p.Width, p.Height, p.DpiX, p.DpiY, p.Words);
                else if (p.Kind == PageColorKind.Bitonal)
                    builder.AddCcittG4Page(p.Bytes!, p.Width, p.Height, p.DpiX, p.DpiY, p.Words);
                else if (p.IsJpx)
                    builder.AddJpxPage(p.Bytes!, p.Width, p.Height, p.DpiX, p.DpiY, p.Words);
                else
                    builder.AddJpegPage(p.Bytes!, p.Width, p.Height, p.DpiX, p.DpiY, p.Words);
            }
            builder.Save(destPath);
            Log.Info($"Exported PDF {destPath}: {pageFiles.Count} page(s), JBIG2={options.UseJBig2}, JP2={options.UseJpeg2000}, PDF/A={options.PdfA}, OCR={ocr != null}");
        }
        finally
        {
            ocr?.Dispose();
            DeleteTempFiles(temps);
        }
    }

    public static void ExportTiff(IReadOnlyList<string> pageFiles, ExportOptions options, string destPath,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        var temps = new List<string>();
        try
        {
            var pages = new List<TiffPage>(pageFiles.Count);
            for (int i = 0; i < pageFiles.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                progress?.Report(new WorkProgress(i + 1, pageFiles.Count, $"Mã hoá trang {i + 1}/{pageFiles.Count}"));
                Prepared p = PreparePage(pageFiles[i], options, forPdf: false, null, temps);
                TiffPageCodec codec = p.Kind == PageColorKind.Bitonal
                    ? (p.IsJBig2 ? TiffPageCodec.JBig2 : TiffPageCodec.CcittG4)
                    : (p.IsJpx ? TiffPageCodec.Jpeg2000 : TiffPageCodec.Jpeg);
                pages.Add(new TiffPage(codec, p.Bytes!, p.Width, p.Height, p.DpiX, p.DpiY, p.HSampling, p.VSampling, p.Components));
            }
            progress?.Report(new WorkProgress(pageFiles.Count, pageFiles.Count, "Ghi file TIFF..."));
            TiffPagePacker.Save(pages, destPath);
            Log.Info($"Exported TIFF {destPath}: {pageFiles.Count} page(s)");
        }
        finally
        {
            DeleteTempFiles(temps);
        }
    }

    /// <summary>Kind the exporter will use for this page (forced mode or auto-detection).</summary>
    public static PageColorKind DecideKind(Bitmap bmp, ColorOutputMode mode, int dpi) => mode switch
    {
        ColorOutputMode.BlackAndWhite => PageColorKind.Bitonal,
        ColorOutputMode.Gray => PageColorKind.Gray,
        ColorOutputMode.Color => PageColorKind.Color,
        _ => PageAnalyzer.Classify(bmp, dpi),
    };

    private static Prepared PreparePage(string file, ExportOptions o, bool forPdf, OcrEngine? ocr, List<string> temps)
    {
        using Bitmap src = ImageUtils.Load(file);
        (int dpiX, int dpiY) = ImageUtils.ResolveDpiXY(src);
        var p = new Prepared
        {
            Width = src.Width,
            Height = src.Height,
            DpiX = dpiX,
            DpiY = dpiY,
            Kind = DecideKind(src, o.ColorMode, dpiX),
        };

        if (p.Kind == PageColorKind.Bitonal)
        {
            GrayImage bin = ImageUtils.ToBinaryGray(src, o.Binarization, o.Despeckle, o.SauvolaK);
            using Bitmap bitonal = bin.ToBitmap1bpp(dpiX, dpiY);
            if (ocr != null) p.Words = ocr.Recognize(bitonal);

            if (o.UseJBig2)
            {
                p.IsJBig2 = true;
                string tmp = Path.Combine(Path.GetTempPath(), "ioc_" + Guid.NewGuid().ToString("N") + ".png");
                bitonal.Save(tmp, ImageFormat.Png);
                temps.Add(tmp);
                // TIFF has no /JBIG2Globals equivalent, so each TIFF strip must be a
                // self-contained generic-region stream; PDF can use shared symbols.
                if (forPdf && o.JBig2Mode == JBig2Mode.Symbol)
                    p.TempBitonalPath = tmp;
                else
                    p.Bytes = JBig2Encoder.EncodeGeneric(tmp).PageStream;
            }
            else
            {
                p.Bytes = G4Encoder.EncodeToG4(bitonal);
            }
            return p;
        }

        // Gray / color.
        if (ocr != null) p.Words = ocr.Recognize(src);

        bool isJpegFile = IsJpegFile(file);
        if (o.UseJpeg2000)
        {
            p.IsJpx = true;
            // Always hand opj_compress a normalized PNG (8-bit gray or 24-bit RGB): it cannot
            // read JPEG and mishandles 1bpp / palette / alpha inputs.
            string input;
            {
                input = Path.Combine(Path.GetTempPath(), "ioc_" + Guid.NewGuid().ToString("N") + ".png");
                using Bitmap prepared = p.Kind == PageColorKind.Gray
                    ? GrayImage.FromBitmap(src).ToBitmap8bpp(dpiX, dpiY)
                    : ImageUtils.To24bpp(src);
                prepared.Save(input, ImageFormat.Png);
                temps.Add(input);
            }
            p.Bytes = OpenJpegEncoder.Encode(input, o.Jpeg2000Ratio > 0 ? o.Jpeg2000Ratio : null);
            p.Components = p.Kind == PageColorKind.Gray ? 1 : 3;
            return p;
        }

        if (isJpegFile && o.PassThroughOriginalJpeg)
        {
            // Original JPEG (camera / scanner output / imported file never edited -- every
            // edit in this app writes a lossless PNG instead): embed it untouched.
            p.Bytes = File.ReadAllBytes(file);
        }
        else if (p.Kind == PageColorKind.Gray)
        {
            using Bitmap gray = GrayImage.FromBitmap(src).ToBitmap8bpp(dpiX, dpiY);
            using Bitmap rgb = ImageUtils.To24bpp(gray);
            p.Bytes = JpegEncoderSimple.Encode(rgb, o.JpegQuality);
        }
        else
        {
            using Bitmap rgb = ImageUtils.To24bpp(src);
            p.Bytes = JpegEncoderSimple.Encode(rgb, o.JpegQuality);
        }
        p.Components = JpegSofReader.ReadComponentCount(p.Bytes);
        (p.HSampling, p.VSampling) = JpegSofReader.ReadComponent0Sampling(p.Bytes);
        return p;
    }

    private static bool IsJpegFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".jpe";
    }

    private static PdfMetadata CloneWithTitle(PdfMetadata m, string title) => new()
    {
        Title = title,
        Author = m.Author,
        Subject = m.Subject,
        Keywords = m.Keywords,
        Creator = m.Creator,
    };

    private static void DeleteTempFiles(IEnumerable<string> tempFiles)
    {
        foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
    }
}
