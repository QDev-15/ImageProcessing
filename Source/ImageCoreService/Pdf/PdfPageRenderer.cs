using System.Drawing;
using PdfiumDocument = PdfiumViewer.PdfDocument;

namespace ImageCoreService;

/// <summary>
/// Renders single pages of a PDF on demand (pdfium). A PDF page is a project page source in
/// its own right -- nothing is rendered at import -- so this is called for a low-resolution
/// proxy when the page is first looked at, and for the full-quality image when exporting.
/// pdfium serializes its own native calls, so concurrent callers are safe; each call opens
/// the file afresh (no long-lived handle: the project folder can be moved or deleted).
/// </summary>
public static class PdfPageRenderer
{
    /// <summary>No page is rendered above this many pixels (an oversized / corrupted MediaBox
    /// would otherwise ask for hundreds of MP); 36 MP is A4 at ~600 dpi.</summary>
    public const double MaxRenderPixels = 36_000_000;

    /// <summary>Default rasterization density when no target DPI is set.</summary>
    public const int DefaultDpi = 300;

    public static int PageCount(string pdfPath)
    {
        using FileStream fs = File.OpenRead(pdfPath);
        using PdfiumDocument doc = PdfiumDocument.Load(fs);
        return doc.PageCount;
    }

    /// <summary>Highest useful density per page (the dominant embedded image's own DPI), null
    /// entries when a page has no dominant raster image or the PDF cannot be inspected.</summary>
    public static double?[] NativeDpis(string pdfPath) => PdfSplitter.GetNativeImageDpis(pdfPath);

    /// <summary>Page size in points (1/72 inch).</summary>
    public static SizeF PageSize(string pdfPath, int page)
    {
        using FileStream fs = File.OpenRead(pdfPath);
        using PdfiumDocument doc = PdfiumDocument.Load(fs);
        return doc.PageSizes[page];
    }

    /// <summary>The page at <paramref name="dpi"/>, never beyond <see cref="MaxRenderPixels"/>.
    /// The bitmap's DPI tag is derived from its pixel size against A4 / Letter / Legal, not from
    /// the file's MediaBox (which may be corrupted -- see <see cref="ImageUtils.EstimateDpiFromPixels"/>).</summary>
    public static Bitmap Render(string pdfPath, int page, int dpi)
    {
        using FileStream fs = File.OpenRead(pdfPath);
        using PdfiumDocument doc = PdfiumDocument.Load(fs);
        SizeF pt = doc.PageSizes[page];
        double pixels = pt.Width / 72.0 * dpi * (pt.Height / 72.0 * dpi);
        if (pixels > MaxRenderPixels)
            dpi = Math.Max(36, (int)(dpi * Math.Sqrt(MaxRenderPixels / pixels)));
        return RenderCore(doc, page, dpi);
    }

    /// <summary>The page scaled to fit <paramref name="maxEdge"/> pixels on its long side.</summary>
    public static Bitmap RenderFit(string pdfPath, int page, int maxEdge)
    {
        using FileStream fs = File.OpenRead(pdfPath);
        using PdfiumDocument doc = PdfiumDocument.Load(fs);
        SizeF pt = doc.PageSizes[page];
        double longInches = Math.Max(pt.Width, pt.Height) / 72.0;
        int dpi = Math.Max(20, (int)Math.Round(maxEdge / Math.Max(longInches, 0.1)));
        return RenderCore(doc, page, dpi);
    }

    private static Bitmap RenderCore(PdfiumDocument doc, int page, int dpi)
    {
        using IDisposable perf = Perf.Scope("pdf.render");
        using Image img = doc.Render(page, dpi, dpi, PdfiumViewer.PdfRenderFlags.CorrectFromDpi | PdfiumViewer.PdfRenderFlags.Annotations);
        var bmp = new Bitmap(img);
        int sane = ImageUtils.EstimateDpiFromPixels(bmp.Width, bmp.Height);
        bmp.SetResolution(sane, sane);
        return bmp;
    }
}
