using System.Drawing;
using System.Drawing.Imaging;
using PdfiumDocument = PdfiumViewer.PdfDocument;
using PdfSharpDocument = PdfSharp.Pdf.PdfDocument;

namespace ImageCoreService;

/// <summary>
/// Splits a PDF into one image file per page, the same way the main app imports a
/// PDF (IMIP.OpenImaging.Internal.PdfUtils.RenderPages / OpenImageProcessing.SplitPDF)
/// -- render via pdfium, capped to the source's own native detail so a genuinely
/// low-DPI PDF (e.g. GdPicture's own 150dpi export) isn't needlessly upsampled to a
/// fixed request, then tag the saved file with a DPI our own code derives (see
/// EstimateSaneDpi), not whatever pdfium's render happens to imply.
/// </summary>
public static class PdfSplitter
{
    public static List<string> SplitToImages(string pdfPath, string destFolder, int dpi = 300)
    {
        Directory.CreateDirectory(destFolder);
        var result = new List<string>();

        // Real bug, found from a live comparison against a genuine GdPicture 150dpi
        // export: without this cap, a PDF whose native content is already low-DPI
        // still got rendered "at 300dpi" (of its own correct, un-corrupted MediaBox),
        // producing an UPSCALED bitmap with no real extra detail -- purely wasted
        // pixels that the 200dpi export cap only partially undoes (200 > true 150dpi
        // native), so the export ends up bigger than it needs to be for zero quality
        // gain. Mirrors PdfUtils.RenderPages's own native-DPI cap in the main app.
        double?[] nativeDpiPerPage = TryGetNativeImageDpiPerPage(pdfPath);

        using PdfiumDocument doc = PdfiumDocument.Load(pdfPath);
        for (int i = 0; i < doc.PageCount; i++)
        {
            int effectiveDpi = dpi;
            if (nativeDpiPerPage != null && i < nativeDpiPerPage.Length && nativeDpiPerPage[i].HasValue)
            {
                int nativeDpi = (int)Math.Round(nativeDpiPerPage[i].Value);
                if (nativeDpi > 0 && nativeDpi < effectiveDpi)
                    effectiveDpi = nativeDpi;
            }

            using Image img = doc.Render(i, effectiveDpi, effectiveDpi,
                PdfiumViewer.PdfRenderFlags.CorrectFromDpi | PdfiumViewer.PdfRenderFlags.Annotations);
            using var bmp = new Bitmap(img);

            // Tagging the render output with the DPI we asked pdfium to render at
            // (the old approach) just reproduces whatever physical page size the
            // source PDF's own /MediaBox declares -- DPI resampling can only change
            // pixel density, never physical size, when render-dpi == tag-dpi. If that
            // MediaBox is itself corrupted (a real ~2490x3462px scan whose page was
            // computed via a wrong 96dpi assumption instead of the true ~300dpi --
            // see Goal.md 2026-09-24, source file NoneGD2.pdf) the split image would
            // silently inherit and propagate the same wrong physical size forever,
            // no matter what DPI cap runs downstream.
            //
            // Instead, derive DPI from the RENDERED PIXEL DIMENSIONS against whichever
            // standard paper size (A4/Letter/Legal) best matches this page's aspect
            // ratio -- independent of the source's own (possibly wrong) MediaBox
            // entirely. Verified against NoneGD2.pdf's actual numbers: a 2490x3462px
            // page has aspect ratio 0.719, closest to A4's 0.707, giving an estimated
            // DPI of ~301 -- which matches the true original scan resolution (~300dpi)
            // that the corrupted MediaBox lost. For a page whose MediaBox already IS a
            // standard size, this reconstructs the same DPI that was actually
            // requested, so correctly-sized PDFs are unaffected.
            int saneDpi = ImageUtils.EstimateDpiFromPixels(bmp.Width, bmp.Height);
            bmp.SetResolution(saneDpi, saneDpi);

            string path = Path.Combine(destFolder, $"{Path.GetFileNameWithoutExtension(pdfPath)}_p{i + 1:000}.png");
            bmp.Save(path, ImageFormat.Png);
            result.Add(path);
        }
        return result;
    }

    /// <summary>
    /// For each page, finds the largest embedded raster image XObject and derives its
    /// native DPI (pixel size vs. the page's physical size in points) so the render
    /// step above never upsamples beyond what the source actually has. Same logic and
    /// same corrupted-/MediaBox correction as the main app's
    /// IMIP.OpenImaging.Internal.PdfUtils.TryGetNativeImageDpiPerPage/GetDominantImageDpi
    /// (kept as a separate copy here since ImageOptimizerTool is deliberately not
    /// wired into the main app's assemblies). Returns null (whole array) if the PDF
    /// can't be parsed this way; a null entry for a given page means "no dominant
    /// raster image found, don't cap that page's render DPI".
    /// </summary>
    private static double?[] TryGetNativeImageDpiPerPage(string pdfPath)
    {
        try
        {
            using PdfSharpDocument doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            var result = new double?[doc.PageCount];
            for (int i = 0; i < doc.PageCount; i++)
                result[i] = GetDominantImageDpi(doc.Pages[i]);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static double? GetDominantImageDpi(PdfSharp.Pdf.PdfPage page)
    {
        PdfSharp.Pdf.PdfDictionary resources = page.Elements.GetDictionary("/Resources");
        PdfSharp.Pdf.PdfDictionary xobjects = resources?.Elements.GetDictionary("/XObject");
        if (xobjects == null)
            return null;

        int bestWidth = 0, bestHeight = 0;
        foreach (string key in xobjects.Elements.Keys)
        {
            PdfSharp.Pdf.PdfDictionary image = xobjects.Elements.GetDictionary(key);
            if (image == null || image.Elements.GetName("/Subtype") != "/Image")
                continue;

            int w = image.Elements.GetInteger("/Width");
            int h = image.Elements.GetInteger("/Height");
            if ((long)w * h > (long)bestWidth * bestHeight)
            {
                bestWidth = w;
                bestHeight = h;
            }
        }

        if (bestWidth <= 0 || bestHeight <= 0)
            return null;

        double pageWidthPt = page.Width.Point;
        double pageHeightPt = page.Height.Point;
        if (pageWidthPt <= 0 || pageHeightPt <= 0)
            return null;

        double pageWidthIn = pageWidthPt / 72.0;
        double pageHeightIn = pageHeightPt / 72.0;

        // Sanity bound: no real scanned/printed document page is bigger than roughly
        // A3 (~17in) on its long edge -- a /MediaBox beyond that is corrupted (see
        // NoneGD2.pdf); fall back to the aspect-ratio reconstruction instead of
        // trusting it, same as the main app's PdfUtils.GetDominantImageDpi.
        const double maxPlausiblePageInches = 20.0;
        if (Math.Max(pageWidthIn, pageHeightIn) > maxPlausiblePageInches)
            return ImageUtils.EstimateDpiFromPixels(bestWidth, bestHeight);

        // Conservative: use the axis with LESS native detail, so we never upsample
        // either dimension beyond what the source actually has.
        double dpiX = bestWidth * 72.0 / pageWidthPt;
        double dpiY = bestHeight * 72.0 / pageHeightPt;
        return Math.Min(dpiX, dpiY);
    }

}
