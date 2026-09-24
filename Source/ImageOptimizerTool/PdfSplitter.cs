using System.Drawing;
using System.Drawing.Imaging;
using PdfiumDocument = PdfiumViewer.PdfDocument;
using PdfSharpDocument = PdfSharp.Pdf.PdfDocument;

namespace ImageOptimizerTool;

/// <summary>
/// Splits a PDF into one image file per page, the same way the main app imports a
/// PDF (IMIP.OpenImaging.Internal.PdfUtils.RenderPages / OpenImageProcessing.SplitPDF)
/// -- render via pdfium, capped to the source's own native detail so a genuinely
/// low-DPI PDF (e.g. GdPicture's own 150dpi export) isn't needlessly upsampled to a
/// fixed request, then tag the saved file with a DPI our own code derives (see
/// EstimateSaneDpi), not whatever pdfium's render happens to imply.
/// </summary>
internal static class PdfSplitter
{
    /// <summary>
    /// Common paper sizes in inches (portrait), used to reconstruct a sane DPI for a
    /// split page independently of the source PDF's own declared MediaBox. Deliberately
    /// excludes other ISO A-series sizes (A3, A5, ...): the ENTIRE ISO 216 series shares
    /// the exact same aspect ratio (1:root2 =~ 0.707) by design, so aspect-ratio matching
    /// cannot distinguish "A4" from "A5" or "A3" at all -- including them just lets tiny
    /// pixel-rounding noise flip the match between pages of the SAME document (observed:
    /// page 1 of NoneGD2.pdf matched A4, pages 2-3 matched A5, giving wildly inconsistent
    /// per-page DPI -- 941 vs 1327 -- for what should obviously be one uniform scan job).
    /// A4 stands in for the whole ISO series; Letter/Legal have genuinely distinct ratios.
    /// </summary>
    private static readonly (double WidthIn, double HeightIn)[] StandardPageSizesIn =
    {
        (8.27, 11.69), // A4 (and every other ISO A-series size, aspect-ratio-wise)
        (8.5, 11.0),   // US Letter
        (8.5, 14.0),   // US Legal
    };

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
            int saneDpi = EstimateSaneDpi(bmp.Width, bmp.Height);
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
            return EstimateSaneDpi(bestWidth, bestHeight);

        // Conservative: use the axis with LESS native detail, so we never upsample
        // either dimension beyond what the source actually has.
        double dpiX = bestWidth * 72.0 / pageWidthPt;
        double dpiY = bestHeight * 72.0 / pageHeightPt;
        return Math.Min(dpiX, dpiY);
    }

    private static int EstimateSaneDpi(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return 200;

        double aspect = (double)pixelWidth / pixelHeight;
        double bestDiff = double.MaxValue;
        double bestWidthIn = 8.27; // fallback: A4 portrait width

        foreach ((double w, double h) in StandardPageSizesIn)
        {
            // Check both orientations of each standard size.
            foreach ((double widthIn, double heightIn) in new[] { (w, h), (h, w) })
            {
                double diff = Math.Abs(widthIn / heightIn - aspect);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestWidthIn = widthIn;
                }
            }
        }

        int dpi = (int)Math.Round(pixelWidth / bestWidthIn);
        return dpi > 0 ? dpi : 200;
    }
}
