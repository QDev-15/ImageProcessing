using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>Everything that shapes an export. Build from settings with <see cref="FromSettings"/>.</summary>
public sealed class ExportOptions
{
    /// <summary>false -> CCITT G4 for bitonal pages.</summary>
    public bool UseJBig2 { get; set; }
    /// <summary>false -> JPEG for gray / color pages.</summary>
    public bool UseJpeg2000 { get; set; }
    public JBig2Mode JBig2Mode { get; set; } = JBig2Mode.Symbol;
    public double JBig2Threshold { get; set; } = 0.85;
    public int JpegQuality { get; set; } = 60;
    /// <summary>0 = lossless.</summary>
    public double Jpeg2000Ratio { get; set; } = 40;
    public bool PassThroughOriginalJpeg { get; set; } = true;

    /// <summary>Pages above this DPI are resampled down when rendered for export (0 = never).</summary>
    public int TargetDpi { get; set; }

    /// <summary>When set, words already recognized for a page are reused and new ones stored.</summary>
    public OcrCache? OcrCache { get; set; }

    public ColorOutputMode ColorMode { get; set; } = ColorOutputMode.Auto;
    public BinarizationMethod Binarization { get; set; } = BinarizationMethod.Sauvola;
    public double SauvolaK { get; set; } = Binarizer.DefaultSauvolaK;
    public bool Despeckle { get; set; } = true;

    public bool PdfA { get; set; } = true;
    public bool Ocr { get; set; }
    public string OcrLanguages { get; set; } = "vie+eng";
    public PdfMetadata Metadata { get; set; } = new();

    public static ExportOptions FromSettings(AppSettings s, string? profileName = null) => new()
    {
        TargetDpi = s.LimitDpiToScanSetting ? s.GetTargetDpi(profileName) : 0,
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
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default) =>
        ExportPdf(ToRecords(pageFiles), options, destPath, progress, cancel);

    private static List<PageRecord> ToRecords(IReadOnlyList<string> files) =>
        files.Select(f => PageRecord.FromFile(f, Path.GetFileName(f))).ToList();

    public static void ExportPdf(IReadOnlyList<PageRecord> pageFiles, ExportOptions options, string destPath,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        var temps = new List<string>();
        try
        {
            string? ocrLanguages = null;
            if (options.Ocr)
            {
                if (OcrEngine.IsLanguageAvailable(options.OcrLanguages))
                    ocrLanguages = options.OcrLanguages;
                else
                    Log.Warn($"OCR skipped: language data '{options.OcrLanguages}' not found in {OcrEngine.TessDataPath}");
            }

            Prepared[] prepared = PrepareAll(pageFiles, options, forPdf: true, ocrLanguages, temps, progress, cancel);

            // JBIG2 symbol mode: one jbig2.exe run over every bitonal page so glyphs repeated
            // ACROSS pages share one dictionary (embedded once, referenced by every page).
            var symbolPages = prepared.Where(p => p.TempBitonalPath != null).ToList();
            if (symbolPages.Count > 0)
            {
                cancel.ThrowIfCancellationRequested();
                progress?.Report(new WorkProgress(pageFiles.Count, pageFiles.Count, "JBIG2: mã hoá từ điển ký tự..."));
                try
                {
                    JBig2Encoder.Result[] results = Perf.Measure("exp.jbig2sym", () => JBig2Encoder.EncodeSymbolMultiPage(
                        symbolPages.Select(p => p.TempBitonalPath!).ToList(), options.JBig2Threshold));
                    for (int i = 0; i < symbolPages.Count; i++)
                    {
                        symbolPages[i].Bytes = results[i].PageStream;
                        symbolPages[i].JBig2Globals = results[i].GlobalsStream;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // jbig2.exe is a 32-bit build: it can run out of memory on very large pages.
                    // Never fail the export for it -- code each page on its own (generic
                    // region), and any page that still fails as CCITT G4.
                    Log.Warn("JBIG2 symbol coding failed; falling back to per-page coding", ex);
                    foreach (Prepared p in symbolPages)
                    {
                        cancel.ThrowIfCancellationRequested();
                        p.JBig2Globals = null;
                        EncodeBitonalFallback(p, p.TempBitonalPath!);
                    }
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
            Perf.Measure("exp.write", () => builder.Save(destPath));
            Log.Info($"Exported PDF {destPath}: {pageFiles.Count} page(s), JBIG2={options.UseJBig2}, JP2={options.UseJpeg2000}, PDF/A={options.PdfA}, OCR={ocrLanguages != null}");
        }
        finally
        {
            DeleteTempFiles(temps);
        }
    }

    public static void ExportTiff(IReadOnlyList<string> pageFiles, ExportOptions options, string destPath,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default) =>
        ExportTiff(ToRecords(pageFiles), options, destPath, progress, cancel);

    public static void ExportTiff(IReadOnlyList<PageRecord> pageFiles, ExportOptions options, string destPath,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        var temps = new List<string>();
        try
        {
            var pages = new List<TiffPage>(pageFiles.Count);
            foreach (Prepared p in PrepareAll(pageFiles, options, forPdf: false, ocrLanguages: null, temps, progress, cancel))
            {
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

    /// <summary>
    /// Prepares every page (binarize / classify / OCR / encode) with a few pages in flight at
    /// once. Pages are independent; the two shared resources are handled explicitly:
    /// Tesseract is not thread-safe, so each worker takes its own <see cref="OcrEngine"/> from
    /// a small pool (an engine is ~1 s to create, so they are reused), and temp files go
    /// through a lock. Results stay in page order.
    /// </summary>
    private static Prepared[] PrepareAll(IReadOnlyList<PageRecord> pageFiles, ExportOptions o, bool forPdf,
        string? ocrLanguages, List<string> temps, IProgress<WorkProgress>? progress, CancellationToken cancel)
    {
        // Engines are created only when a page misses the OCR cache (each takes ~1 s to load).
        int n = pageFiles.Count;
        var result = new Prepared[n];
        // Inner loops (gray conversion, Sauvola) are already multi-threaded, and every worker
        // holds a full page in memory plus its own OCR models: a small degree is the sweet spot.
        int degree = Math.Min(n, Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
        var ocrPool = new System.Collections.Concurrent.ConcurrentBag<OcrEngine>();
        int done = 0;
        void AddTemp(string path) { lock (temps) temps.Add(path); }

        try
        {
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancel }, i =>
            {
                Func<Func<OcrEngine, IReadOnlyList<OcrWord>>, IReadOnlyList<OcrWord>>? runOcr = ocrLanguages == null ? null : work =>
                {
                    if (!ocrPool.TryTake(out OcrEngine? engine)) engine = new OcrEngine(ocrLanguages);
                    try { return work(engine); }
                    finally { ocrPool.Add(engine); }
                };
                result[i] = PreparePage(pageFiles[i], o, forPdf, runOcr, AddTemp);
                int d = Interlocked.Increment(ref done);
                progress?.Report(new WorkProgress(d, n, $"Mã hoá trang {d}/{n}"));
            });
        }
        catch (AggregateException ae) when (ae.InnerExceptions.Count > 0)
        {
            // Surface the real failure, not "One or more errors occurred".
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ae.InnerExceptions[0]).Throw();
        }
        finally
        {
            foreach (OcrEngine engine in ocrPool) engine.Dispose();
        }
        return result;
    }

    /// <summary>Generic-region JBIG2 for one page; if that fails too, CCITT G4 (managed, no
    /// external process). Used when symbol coding cannot run.</summary>
    private static void EncodeBitonalFallback(Prepared p, string bitonalPng)
    {
        try
        {
            p.Bytes = JBig2Encoder.EncodeGeneric(bitonalPng).PageStream;
            p.IsJBig2 = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warn("JBIG2 generic coding failed; using CCITT G4 for this page", ex);
            using Bitmap bitonal = ImageUtils.Load(bitonalPng);
            p.Bytes = G4Encoder.EncodeToG4(bitonal);
            p.IsJBig2 = false;
        }
    }

    /// <summary>Words for the page: from the OCR cache when present, otherwise recognized (through
    /// <paramref name="runOcr"/>, which lends an engine) and stored.</summary>
    private static IReadOnlyList<OcrWord>? WordsFor(PageRecord page, ExportOptions o,
        Func<Func<OcrEngine, IReadOnlyList<OcrWord>>, IReadOnlyList<OcrWord>>? runOcr, Func<OcrEngine, IReadOnlyList<OcrWord>> recognize)
    {
        if (runOcr == null) return null;
        string? key = o.OcrCache != null ? OcrCache.Key(page, o) : null;
        IReadOnlyList<OcrWord>? cached = key != null ? o.OcrCache!.TryGet(key) : null;
        if (cached != null) return cached;
        IReadOnlyList<OcrWord> words = Perf.Measure("exp.ocr", () => runOcr(recognize));
        if (key != null) o.OcrCache!.Put(key, words);
        return words;
    }

    /// <summary>Reads one page exactly as an export would (same render, binarization and
    /// resolution), so the words are interchangeable with those an export produces.</summary>
    public static IReadOnlyList<OcrWord> RecognizePage(PageRecord page, ExportOptions o, OcrEngine ocr)
    {
        using Bitmap src = PageRenderer.RenderFull(page, o.TargetDpi);
        (int dpiX, int dpiY) = ImageUtils.ResolveDpiXY(src);
        if (DecideKind(src, o.ColorMode, dpiX) != PageColorKind.Bitonal) return ocr.Recognize(src);
        GrayImage bin = ImageUtils.ToBinaryGray(src, o.Binarization, o.Despeckle, o.SauvolaK);
        using Bitmap bitonal = bin.ToBitmap1bpp(dpiX, dpiY);
        return ocr.Recognize(bitonal);
    }

    private static Prepared PreparePage(PageRecord page, ExportOptions o, bool forPdf,
        Func<Func<OcrEngine, IReadOnlyList<OcrWord>>, IReadOnlyList<OcrWord>>? runOcr, Action<string> addTemp)
    {
        string file = page.Source.File;
        using Bitmap src = PageRenderer.RenderFull(page, o.TargetDpi, out bool modified);
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
            GrayImage bin = Perf.Measure("exp.binarize", () => ImageUtils.ToBinaryGray(src, o.Binarization, o.Despeckle, o.SauvolaK));
            using Bitmap bitonal = bin.ToBitmap1bpp(dpiX, dpiY);
            p.Words = WordsFor(page, o, runOcr, engine => engine.Recognize(bitonal));

            if (o.UseJBig2)
            {
                p.IsJBig2 = true;
                string tmp = Path.Combine(Path.GetTempPath(), "ioc_" + Guid.NewGuid().ToString("N") + ".png");
                bitonal.Save(tmp, ImageFormat.Png);
                addTemp(tmp);
                // TIFF has no /JBIG2Globals equivalent, so each TIFF strip must be a
                // self-contained generic-region stream; PDF can use shared symbols.
                if (forPdf && o.JBig2Mode == JBig2Mode.Symbol)
                    p.TempBitonalPath = tmp;
                else
                    EncodeBitonalFallback(p, tmp); // generic JBIG2, G4 if jbig2.exe fails
            }
            else
            {
                p.Bytes = G4Encoder.EncodeToG4(bitonal);
            }
            return p;
        }

        // Gray / color.
        p.Words = WordsFor(page, o, runOcr, engine => engine.Recognize(src));

        bool isJpegFile = !modified && PageRenderer.IsRawJpeg(page);
        if (o.UseJpeg2000)
        {
            try
            {
                // Always hand opj_compress a normalized PNG (8-bit gray or 24-bit RGB): it cannot
                // read JPEG and mishandles 1bpp / palette / alpha inputs.
                string input = Path.Combine(Path.GetTempPath(), "ioc_" + Guid.NewGuid().ToString("N") + ".png");
                using (Bitmap prepared = p.Kind == PageColorKind.Gray
                    ? GrayImage.FromBitmap(src).ToBitmap8bpp(dpiX, dpiY)
                    : ImageUtils.To24bpp(src))
                {
                    addTemp(input);
                    prepared.Save(input, ImageFormat.Png);
                }
                p.Bytes = Perf.Measure("exp.jp2", () => OpenJpegEncoder.Encode(input, o.Jpeg2000Ratio > 0 ? o.Jpeg2000Ratio : null));
                p.IsJpx = true;
                p.Components = p.Kind == PageColorKind.Gray ? 1 : 3;
                return p;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Out of memory, missing / blocked exe, odd input: the page must still export,
                // so fall through to the managed JPEG encoder (same geometry, different codec).
                Log.Warn($"JPEG2000 failed for {Path.GetFileName(file)} ({src.Width}x{src.Height}); using JPEG for this page", ex);
                p.IsJpx = false;
                p.Bytes = null;
            }
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
