using System.Globalization;
using System.Text;
using PdfSharp.Pdf;

namespace ImageCoreService;

/// <summary>
/// Builds a multi-page PDF, embedding each candidate codec's bytes verbatim (same
/// "embed as-is, never re-encode" principle as the main app's PdfSharpPdfAArchiver)
/// so what you see in the PDF is exactly the tested encoder's real output, not
/// something PdfSharp re-touched. One instance = one output document; call
/// AddCcittG4Page/AddJBig2Page/AddJpxPage per page in order, then Save.
/// </summary>
public sealed class PdfBuilder
{
    private readonly PdfDocument _doc = new();

    // Tracked separately, not via _doc.PageCount: PdfSharp's PdfDocument throws
    // InvalidOperationException on PageCount once Save() has been called ("the
    // document was already saved and cannot be modified anymore"), so callers that
    // want the count after Save (e.g. for a status message) need it cached here.
    public int PageCount { get; private set; }

    public void AddCcittG4Page(byte[] g4Bytes, int widthPx, int heightPx, int dpi)
    {
        PdfPage page = AddPage(widthPx, heightPx, dpi);
        var image = new PdfDictionary(_doc);
        image.CreateStream(g4Bytes);
        var e = image.Elements;
        e.SetName("/Type", "/XObject");
        e.SetName("/Subtype", "/Image");
        e.SetInteger("/Width", widthPx);
        e.SetInteger("/Height", heightPx);
        e.SetName("/Filter", "/CCITTFaxDecode");
        e.SetInteger("/BitsPerComponent", 1);
        e.SetName("/ColorSpace", "/DeviceGray");
        var dp = new PdfDictionary(_doc);
        dp.Elements.SetInteger("/K", -1);
        dp.Elements.SetInteger("/Columns", widthPx);
        dp.Elements.SetInteger("/Rows", heightPx);
        dp.Elements.SetBoolean("/BlackIs1", false);
        e["/DecodeParms"] = dp;

        Finish(page, image);
    }

    public void AddJBig2Page(byte[] pageBytes, byte[]? globalsBytes, int widthPx, int heightPx, int dpi)
    {
        PdfPage page = AddPage(widthPx, heightPx, dpi);
        var image = new PdfDictionary(_doc);
        image.CreateStream(pageBytes);
        var e = image.Elements;
        e.SetName("/Type", "/XObject");
        e.SetName("/Subtype", "/Image");
        e.SetInteger("/Width", widthPx);
        e.SetInteger("/Height", heightPx);
        e.SetName("/Filter", "/JBIG2Decode");
        e.SetInteger("/BitsPerComponent", 1);
        e.SetName("/ColorSpace", "/DeviceGray");

        if (globalsBytes is { Length: > 0 })
        {
            var globals = new PdfDictionary(_doc);
            globals.CreateStream(globalsBytes);
            _doc.Internals.AddObject(globals);
            var dp = new PdfDictionary(_doc);
            dp.Elements["/JBIG2Globals"] = globals.Reference;
            e["/DecodeParms"] = dp;
        }

        Finish(page, image);
    }

    public void AddJpegPage(byte[] jpegBytes, int widthPx, int heightPx, int dpi)
    {
        PdfPage page = AddPage(widthPx, heightPx, dpi);
        var image = new PdfDictionary(_doc);
        image.CreateStream(jpegBytes);
        var e = image.Elements;
        e.SetName("/Type", "/XObject");
        e.SetName("/Subtype", "/Image");
        e.SetInteger("/Width", widthPx);
        e.SetInteger("/Height", heightPx);
        e.SetName("/Filter", "/DCTDecode");
        e.SetInteger("/BitsPerComponent", 8);
        e.SetName("/ColorSpace", "/DeviceRGB");

        Finish(page, image);
    }

    public void AddJpxPage(byte[] jp2Bytes, int widthPx, int heightPx, int dpi)
    {
        PdfPage page = AddPage(widthPx, heightPx, dpi);
        var image = new PdfDictionary(_doc);
        image.CreateStream(jp2Bytes);
        var e = image.Elements;
        e.SetName("/Type", "/XObject");
        e.SetName("/Subtype", "/Image");
        e.SetInteger("/Width", widthPx);
        e.SetInteger("/Height", heightPx);
        e.SetName("/Filter", "/JPXDecode");
        e.SetInteger("/BitsPerComponent", 8);

        Finish(page, image);
    }

    public void Save(string destPath) => _doc.Save(destPath);

    private PdfPage AddPage(int widthPx, int heightPx, int dpi)
    {
        PdfPage page = _doc.AddPage();
        PageCount++;
        page.Width = PdfSharp.Drawing.XUnit.FromPoint(widthPx / (double)dpi * 72.0);
        page.Height = PdfSharp.Drawing.XUnit.FromPoint(heightPx / (double)dpi * 72.0);
        return page;
    }

    private void Finish(PdfPage page, PdfDictionary image)
    {
        _doc.Internals.AddObject(image);
        PdfDictionary xobjects = page.Resources.Elements.GetDictionary("/XObject") ?? NewDict(page);
        xobjects.Elements["/Im0"] = image.Reference;

        string content = string.Format(CultureInfo.InvariantCulture,
            "q\n{0:0.####} 0 0 {1:0.####} 0 0 cm\n/Im0 Do\nQ\n",
            page.Width.Point, page.Height.Point);
        page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(content));
    }

    private PdfDictionary NewDict(PdfPage page)
    {
        var xobjects = new PdfDictionary(_doc);
        page.Resources.Elements["/XObject"] = xobjects;
        return xobjects;
    }
}
