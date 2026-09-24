using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// Page codec for export. CCITT G4 / JBIG2 are bitonal (B&amp;W) codecs; JPEG /
/// JPEG2000 are color codecs -- mirrors the main app's own codec split.
/// </summary>
public enum ExportCodec { CcittG4, JBig2, Jpeg, Jpeg2000 }

/// <summary>
/// Multi-page PDF/TIFF export of a list of page image files, using any of the four
/// codecs. UI-free, so any front end (ImageOptimizerTool's form, a service, a CLI)
/// gets the exact same pipeline.
/// </summary>
public static class DocumentExporter
{
    public const double DefaultJpeg2000Ratio = 20.0;
    public const int DefaultJpegQuality = 85;

    public static bool IsBitonal(ExportCodec codec) => codec is ExportCodec.CcittG4 or ExportCodec.JBig2;

    public static void ExportPdf(IReadOnlyList<string> pageFiles, ExportCodec codec, string destPath,
        double jpeg2000Ratio = DefaultJpeg2000Ratio, int jpegQuality = DefaultJpegQuality)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        // Cap to 200dpi first, for B&W too (explicitly requested, unlike the main app
        // where bitonal is left untouched). External tools (jbig2.exe, opj_compress.exe)
        // read whichever file path we hand them, so a downsampled page needs to be
        // materialized to a temp file first.
        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(pageFiles);
        try
        {
            var builder = new PdfBuilder();
            if (codec == ExportCodec.JBig2)
            {
                // One jbig2.exe invocation over every page: repeated glyphs are
                // recognized and shared ACROSS pages via a single globals dictionary,
                // not just within each page -- the real advantage of batching a whole
                // document together, vs. G4 where every page stands alone regardless.
                JBig2Encoder.Result[] results = JBig2Encoder.EncodeSymbolMultiPage(sourcePaths);
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    builder.AddJBig2Page(results[i].PageStream, results[i].GlobalsStream, width, height, dpi);
                }
            }
            else
            {
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    switch (codec)
                    {
                        case ExportCodec.CcittG4:
                        {
                            using var src = new Bitmap(sourcePaths[i]);
                            using Bitmap bitonal = ImageUtils.ToBitonal(src);
                            builder.AddCcittG4Page(G4Encoder.EncodeToG4(bitonal), width, height, dpi);
                            break;
                        }
                        case ExportCodec.Jpeg2000:
                            builder.AddJpxPage(OpenJpegEncoder.Encode(sourcePaths[i], jpeg2000Ratio), width, height, dpi);
                            break;
                        case ExportCodec.Jpeg:
                        {
                            using var src = new Bitmap(sourcePaths[i]);
                            using Bitmap rgb = ImageUtils.To24bpp(src);
                            builder.AddJpegPage(JpegEncoderSimple.Encode(rgb, jpegQuality), width, height, dpi);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(codec), codec, null);
                    }
                }
            }
            builder.Save(destPath);
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    public static void ExportTiff(IReadOnlyList<string> pageFiles, ExportCodec codec, string destPath,
        double jpeg2000Ratio = DefaultJpeg2000Ratio, int jpegQuality = DefaultJpegQuality)
    {
        if (pageFiles.Count == 0) throw new ArgumentException("No pages to export.", nameof(pageFiles));

        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(pageFiles);
        try
        {
            switch (codec)
            {
                case ExportCodec.JBig2:
                {
                    // Per-page generic-region coding (NOT the symbol/shared-globals mode
                    // used for PDF): TIFF has no equivalent of PDF's /JBIG2Globals indirect
                    // reference, so each strip must be a fully self-contained stream.
                    var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                    foreach (string path in sourcePaths)
                    {
                        JBig2Encoder.Result r = JBig2Encoder.EncodeGeneric(path);
                        using var src = new Bitmap(path);
                        pages.Add((r.PageStream, src.Width, src.Height, ImageUtils.ResolveDpi(src)));
                    }
                    TiffPagePacker.SaveJbig2(pages, destPath);
                    break;
                }
                case ExportCodec.CcittG4:
                {
                    var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                    for (int i = 0; i < sourcePaths.Count; i++)
                    {
                        (int width, int height, int dpi) = cappedInfo[i];
                        using var src = new Bitmap(sourcePaths[i]);
                        using Bitmap bitonal = ImageUtils.ToBitonal(src);
                        pages.Add((G4Encoder.EncodeToG4(bitonal), width, height, dpi));
                    }
                    TiffPagePacker.SaveCcittG4(pages, destPath);
                    break;
                }
                case ExportCodec.Jpeg2000:
                {
                    var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                    for (int i = 0; i < sourcePaths.Count; i++)
                    {
                        (int width, int height, int dpi) = cappedInfo[i];
                        pages.Add((OpenJpegEncoder.Encode(sourcePaths[i], jpeg2000Ratio), width, height, dpi));
                    }
                    TiffPagePacker.SaveJpeg2000(pages, destPath);
                    break;
                }
                case ExportCodec.Jpeg:
                {
                    var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi, int HSampling, int VSampling)>(sourcePaths.Count);
                    for (int i = 0; i < sourcePaths.Count; i++)
                    {
                        (int width, int height, int dpi) = cappedInfo[i];
                        using var src = new Bitmap(sourcePaths[i]);
                        using Bitmap rgb = ImageUtils.To24bpp(src);
                        byte[] jpeg = JpegEncoderSimple.Encode(rgb, jpegQuality);
                        (int h, int v) = JpegSofReader.ReadComponent0Sampling(jpeg);
                        pages.Add((jpeg, width, height, dpi, h, v));
                    }
                    TiffPagePacker.SaveJpeg(pages, destPath);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(codec), codec, null);
            }
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    /// <summary>
    /// For each input file, applies ImageUtils.CapDpi and, if that actually
    /// downsampled the page, saves the result to a new temp PNG (returned in
    /// sourcePaths in place of the original) so external tools that only take a file
    /// path (jbig2.exe) see the capped pixels. Returns per-page (width, height, dpi)
    /// for PDF page sizing, and the list of temp files the caller must delete.
    /// </summary>
    public static (List<string> sourcePaths, List<(int, int, int)> info, List<string> tempFiles) PrepareCappedSources(IReadOnlyList<string> files)
    {
        var sourcePaths = new List<string>(files.Count);
        var info = new List<(int, int, int)>(files.Count);
        var tempFiles = new List<string>();

        foreach (string file in files)
        {
            using var original = new Bitmap(file);
            Bitmap capped = ImageUtils.CapDpi(original);
            try
            {
                if (ReferenceEquals(capped, original))
                {
                    sourcePaths.Add(file);
                }
                else
                {
                    string tempPng = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
                    capped.Save(tempPng, ImageFormat.Png);
                    sourcePaths.Add(tempPng);
                    tempFiles.Add(tempPng);
                }
                info.Add((capped.Width, capped.Height, ImageUtils.ResolveDpi(capped)));
            }
            finally
            {
                if (!ReferenceEquals(capped, original)) capped.Dispose();
            }
        }
        return (sourcePaths, info, tempFiles);
    }

    private static void DeleteTempFiles(IEnumerable<string> tempFiles)
    {
        foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
    }
}
