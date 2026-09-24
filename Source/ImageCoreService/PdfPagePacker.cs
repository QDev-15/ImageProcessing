using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace ImageCoreService;

/// <summary>Document information written to the PDF Info dictionary (and XMP for PDF/A).</summary>
public sealed class PdfMetadata
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string Creator { get; set; } = "ImageOptimizerTool";
}

/// <summary>
/// Builds a multi-page PDF, embedding each codec's bytes verbatim ("embed as-is, never
/// re-encode", same principle as the main app's PdfSharpPdfAArchiver). One instance = one
/// output document; call Add*Page per page in order, then Save.
///
/// Optional per page: an invisible OCR text layer (text render mode 3, Tesseract's
/// GlyphLessFont -- the same technique Tesseract's own PDF renderer and OCRmyPDF use), so
/// the PDF is searchable / copyable while looking exactly like the scan.
///
/// Optional per document: PDF/A-2b (ISO 19005-2, level B) -- PDF 1.7 header, XMP metadata
/// matching the Info dictionary, sRGB OutputIntent, all fonts embedded, no LZW /
/// transparency / encryption.
/// </summary>
public sealed class PdfBuilder
{
    private readonly PdfDocument _doc = new();
    private PdfDictionary? _ocrFont;
    private string? _pendingXmp;

    public int PageCount { get; private set; }

    public bool PdfA { get; set; }
    public PdfMetadata Metadata { get; set; } = new();

    public PdfBuilder()
    {
        _doc.Options.CompressContentStreams = true;
    }

    public void AddCcittG4Page(byte[] g4Bytes, int widthPx, int heightPx, int dpi, IReadOnlyList<OcrWord>? words = null) =>
        AddCcittG4Page(g4Bytes, widthPx, heightPx, dpi, dpi, words);

    public void AddCcittG4Page(byte[] g4Bytes, int widthPx, int heightPx, int dpiX, int dpiY, IReadOnlyList<OcrWord>? words = null)
    {
        PdfDictionary image = NewImage(g4Bytes, widthPx, heightPx, "/CCITTFaxDecode", 1, "/DeviceGray");
        var dp = new PdfDictionary(_doc);
        dp.Elements.SetInteger("/K", -1);
        dp.Elements.SetInteger("/Columns", widthPx);
        dp.Elements.SetInteger("/Rows", heightPx);
        dp.Elements.SetBoolean("/BlackIs1", false);
        image.Elements["/DecodeParms"] = dp;
        AddImagePage(image, widthPx, heightPx, dpiX, dpiY, words);
    }

    public void AddJBig2Page(byte[] pageBytes, byte[]? globalsBytes, int widthPx, int heightPx, int dpi, IReadOnlyList<OcrWord>? words = null) =>
        AddJBig2Page(pageBytes, globalsBytes, widthPx, heightPx, dpi, dpi, words);

    private readonly Dictionary<byte[], PdfDictionary> _jbig2Globals = new(ReferenceEqualityComparer.Instance);

    public void AddJBig2Page(byte[] pageBytes, byte[]? globalsBytes, int widthPx, int heightPx, int dpiX, int dpiY, IReadOnlyList<OcrWord>? words = null)
    {
        PdfDictionary image = NewImage(pageBytes, widthPx, heightPx, "/JBIG2Decode", 1, "/DeviceGray");
        if (globalsBytes is { Length: > 0 })
        {
            // One shared globals object per distinct array (EncodeSymbolMultiPage hands every
            // page the SAME array) -- embedding it once is the whole point of symbol mode.
            if (!_jbig2Globals.TryGetValue(globalsBytes, out PdfDictionary? globals))
            {
                globals = new PdfDictionary(_doc);
                globals.CreateStream(globalsBytes);
                _doc.Internals.AddObject(globals);
                _jbig2Globals[globalsBytes] = globals;
            }
            var dp = new PdfDictionary(_doc);
            dp.Elements["/JBIG2Globals"] = globals.Reference;
            image.Elements["/DecodeParms"] = dp;
        }
        AddImagePage(image, widthPx, heightPx, dpiX, dpiY, words);
    }

    public void AddJpegPage(byte[] jpegBytes, int widthPx, int heightPx, int dpi, IReadOnlyList<OcrWord>? words = null) =>
        AddJpegPage(jpegBytes, widthPx, heightPx, dpi, dpi, words);

    public void AddJpegPage(byte[] jpegBytes, int widthPx, int heightPx, int dpiX, int dpiY, IReadOnlyList<OcrWord>? words = null)
    {
        int components = JpegSofReader.ReadComponentCount(jpegBytes);
        string cs = components switch { 1 => "/DeviceGray", 4 => "/DeviceCMYK", _ => "/DeviceRGB" };
        PdfDictionary image = NewImage(jpegBytes, widthPx, heightPx, "/DCTDecode", 8, cs);
        if (components == 4)
        {
            // Adobe-style CMYK JPEGs are stored inverted.
            var decode = new PdfArray(_doc);
            foreach (int v in new[] { 1, 0, 1, 0, 1, 0, 1, 0 }) decode.Elements.Add(new PdfInteger(v));
            image.Elements["/Decode"] = decode;
        }
        AddImagePage(image, widthPx, heightPx, dpiX, dpiY, words);
    }

    public void AddJpxPage(byte[] jp2Bytes, int widthPx, int heightPx, int dpi, IReadOnlyList<OcrWord>? words = null) =>
        AddJpxPage(jp2Bytes, widthPx, heightPx, dpi, dpi, words);

    public void AddJpxPage(byte[] jp2Bytes, int widthPx, int heightPx, int dpiX, int dpiY, IReadOnlyList<OcrWord>? words = null)
    {
        // No /ColorSpace: for JPXDecode the colour space inside the JP2 (colr box) is used.
        PdfDictionary image = NewImage(jp2Bytes, widthPx, heightPx, "/JPXDecode", null, null);
        AddImagePage(image, widthPx, heightPx, dpiX, dpiY, words);
    }

    public void Save(string destPath)
    {
        ApplyMetadata();
        string tmp = destPath + ".partial";
        _doc.Save(tmp);
        if (_pendingXmp != null) PdfIncrementalXmp.Attach(tmp, Encoding.UTF8.GetBytes(_pendingXmp));
        File.Move(tmp, destPath, overwrite: true);
    }

    #region Page assembly

    private PdfDictionary NewImage(byte[] bytes, int widthPx, int heightPx, string filter, int? bpc, string? colorSpace)
    {
        var image = new PdfDictionary(_doc);
        image.CreateStream(bytes);
        var e = image.Elements;
        e.SetName("/Type", "/XObject");
        e.SetName("/Subtype", "/Image");
        e.SetInteger("/Width", widthPx);
        e.SetInteger("/Height", heightPx);
        e.SetName("/Filter", filter);
        if (bpc.HasValue) e.SetInteger("/BitsPerComponent", bpc.Value);
        if (colorSpace != null) e.SetName("/ColorSpace", colorSpace);
        return image;
    }

    private void AddImagePage(PdfDictionary image, int widthPx, int heightPx, int dpiX, int dpiY, IReadOnlyList<OcrWord>? words)
    {
        if (dpiX <= 0) dpiX = 200;
        if (dpiY <= 0) dpiY = dpiX;

        PdfPage page = _doc.AddPage();
        PageCount++;
        double wPt = widthPx * 72.0 / dpiX, hPt = heightPx * 72.0 / dpiY;
        page.Width = PdfSharp.Drawing.XUnit.FromPoint(wPt);
        page.Height = PdfSharp.Drawing.XUnit.FromPoint(hPt);

        _doc.Internals.AddObject(image);
        PdfDictionary xobjects = page.Resources.Elements.GetDictionary("/XObject") ?? NewResourceDict(page, "/XObject");
        xobjects.Elements["/Im0"] = image.Reference;

        var content = new StringBuilder();
        content.AppendFormat(CultureInfo.InvariantCulture, "q\n{0:0.####} 0 0 {1:0.####} 0 0 cm\n/Im0 Do\nQ\n", wPt, hPt);

        if (words is { Count: > 0 })
        {
            PdfDictionary font = GetOcrFont();
            PdfDictionary fonts = page.Resources.Elements.GetDictionary("/Font") ?? NewResourceDict(page, "/Font");
            fonts.Elements["/FOcr"] = font.Reference;
            AppendTextLayer(content, words, dpiX, dpiY, hPt);
        }

        page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(content.ToString()));
    }

    private PdfDictionary NewResourceDict(PdfPage page, string key)
    {
        var d = new PdfDictionary(_doc);
        page.Resources.Elements[key] = d;
        return d;
    }

    /// <summary>
    /// Invisible text (3 Tr). GlyphLessFont maps every CID to one empty glyph 0.5 em wide
    /// (DW 500), so each word is stretched with Tz to exactly cover its box on the scan --
    /// selection highlights and search hits land on the right place. CIDs are UTF-16 code
    /// units (Identity-H), mapped back to Unicode by the identity ToUnicode CMap, so
    /// Vietnamese (precomposed, NFC) text copies out correctly.
    /// </summary>
    private static void AppendTextLayer(StringBuilder sb, IReadOnlyList<OcrWord> words, int dpiX, int dpiY, double pageHeightPt)
    {
        sb.Append("BT\n3 Tr\n");
        foreach (OcrWord w in words)
        {
            if (w.Width <= 0 || w.Height <= 0 || w.Text.Length == 0) continue;
            double x = w.X * 72.0 / dpiX;
            double baseline = pageHeightPt - w.BaselineY * 72.0 / dpiY;
            double size = Math.Max(1.0, w.Height * 72.0 / dpiY);
            double widthPt = w.Width * 72.0 / dpiX;
            double natural = w.Text.Length * size * 0.5;
            double hscale = natural > 0 ? 100.0 * widthPt / natural : 100;

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "/FOcr {0:0.##} Tf\n{1:0.###} Tz\n1 0 0 1 {2:0.###} {3:0.###} Tm\n<", size, hscale, x, baseline);
            foreach (char c in w.Text + " ") sb.Append(((int)c).ToString("X4"));
            sb.Append("> Tj\n");
        }
        sb.Append("ET\n");
    }

    #endregion

    #region OCR font

    private PdfDictionary GetOcrFont()
    {
        if (_ocrFont != null) return _ocrFont;

        byte[] ttf = LoadGlyphLessFont();

        var fontFile = new PdfDictionary(_doc);
        fontFile.CreateStream(ttf);
        fontFile.Elements.SetInteger("/Length1", ttf.Length);
        _doc.Internals.AddObject(fontFile);

        var descriptor = new PdfDictionary(_doc);
        var d = descriptor.Elements;
        d.SetName("/Type", "/FontDescriptor");
        d.SetName("/FontName", "/GlyphLessFont");
        d.SetInteger("/Flags", 5);
        d["/FontBBox"] = IntArray(0, 0, 500, 1000);
        d.SetInteger("/ItalicAngle", 0);
        d.SetInteger("/Ascent", 1000);
        d.SetInteger("/Descent", -1);
        d.SetInteger("/CapHeight", 1000);
        d.SetInteger("/StemV", 80);
        d["/FontFile2"] = fontFile.Reference;
        _doc.Internals.AddObject(descriptor);

        // Every CID -> glyph 1 (the font's single empty glyph).
        var map = new byte[65536 * 2];
        for (int i = 0; i < 65536; i++) map[i * 2 + 1] = 1;
        var cidToGid = new PdfDictionary(_doc);
        cidToGid.CreateStream(Deflate(map));
        cidToGid.Elements.SetName("/Filter", "/FlateDecode");
        _doc.Internals.AddObject(cidToGid);

        var sysInfo = new PdfDictionary(_doc);
        sysInfo.Elements["/Registry"] = new PdfString("Adobe");
        sysInfo.Elements["/Ordering"] = new PdfString("Identity");
        sysInfo.Elements.SetInteger("/Supplement", 0);

        var cidFont = new PdfDictionary(_doc);
        var c = cidFont.Elements;
        c.SetName("/Type", "/Font");
        c.SetName("/Subtype", "/CIDFontType2");
        c.SetName("/BaseFont", "/GlyphLessFont");
        c["/CIDSystemInfo"] = sysInfo;
        c["/FontDescriptor"] = descriptor.Reference;
        c.SetInteger("/DW", 500);
        c["/CIDToGIDMap"] = cidToGid.Reference;
        _doc.Internals.AddObject(cidFont);

        const string cmap =
            "/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n" +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n" +
            "/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n" +
            "1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n" +
            "1 beginbfrange\n<0000> <FFFF> <0000>\nendbfrange\n" +
            "endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n";
        var toUnicode = new PdfDictionary(_doc);
        toUnicode.CreateStream(Encoding.ASCII.GetBytes(cmap));
        _doc.Internals.AddObject(toUnicode);

        var descendants = new PdfArray(_doc);
        descendants.Elements.Add(cidFont.Reference);

        var type0 = new PdfDictionary(_doc);
        var t = type0.Elements;
        t.SetName("/Type", "/Font");
        t.SetName("/Subtype", "/Type0");
        t.SetName("/BaseFont", "/GlyphLessFont");
        t.SetName("/Encoding", "/Identity-H");
        t["/DescendantFonts"] = descendants;
        t["/ToUnicode"] = toUnicode.Reference;
        _doc.Internals.AddObject(type0);

        _ocrFont = type0;
        return type0;
    }

    private static byte[] LoadGlyphLessFont()
    {
        using Stream s = typeof(PdfBuilder).Assembly.GetManifestResourceStream("ImageCoreService.GlyphLessFont.ttf")
            ?? throw new InvalidOperationException("Embedded GlyphLessFont.ttf is missing.");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    #endregion

    #region Metadata / PDF/A-2b

    private void ApplyMetadata()
    {
        DateTime now = DateTime.Now;
        PdfDocumentInformation info = _doc.Info;
        if (Metadata.Title.Length > 0) info.Title = Metadata.Title;
        if (Metadata.Author.Length > 0) info.Author = Metadata.Author;
        if (Metadata.Subject.Length > 0) info.Subject = Metadata.Subject;
        if (Metadata.Keywords.Length > 0) info.Keywords = Metadata.Keywords;
        info.Creator = Metadata.Creator;
        info.CreationDate = now;
        info.ModificationDate = now;

        if (!PdfA) return;

        _doc.Version = 17;
        // PDFsharp stamps its own Producer on save; mirror it (and our dates) in XMP so the
        // packet agrees with the Info dictionary (a PDF/A requirement).
        string producer = PdfSharpProducer.Value;

        // PDFsharp always (re)generates its own XMP on save (claiming PDF/A-1A when its
        // PDF/A flag is set), so ours is attached afterwards as an incremental update --
        // see Save / PdfIncrementalXmp.
        _pendingXmp = BuildXmp(Metadata, producer, now);

        byte[] icc = SrgbProfile.Value;
        var iccStream = new PdfDictionary(_doc);
        iccStream.CreateStream(icc);
        iccStream.Elements.SetInteger("/N", 3);
        _doc.Internals.AddObject(iccStream);

        var intent = new PdfDictionary(_doc);
        intent.Elements.SetName("/Type", "/OutputIntent");
        intent.Elements.SetName("/S", "/GTS_PDFA1");
        intent.Elements["/OutputConditionIdentifier"] = new PdfString("sRGB IEC61966-2.1");
        intent.Elements["/Info"] = new PdfString("sRGB IEC61966-2.1");
        intent.Elements["/DestOutputProfile"] = iccStream.Reference;
        var intents = new PdfArray(_doc);
        intents.Elements.Add(intent);
        _doc.Internals.Catalog.Elements["/OutputIntents"] = intents;
    }

    private static string BuildXmp(PdfMetadata m, string producer, DateTime now)
    {
        string date = now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + FormatOffset(now);
        static string X(string s) => SecurityElement.Escape(s) ?? "";

        var sb = new StringBuilder();
        sb.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        sb.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        sb.Append("<rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">\n");
        sb.Append("<pdfaid:part>2</pdfaid:part>\n<pdfaid:conformance>B</pdfaid:conformance>\n</rdf:Description>\n");
        sb.Append("<rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");
        sb.Append($"<xmp:CreateDate>{date}</xmp:CreateDate>\n<xmp:ModifyDate>{date}</xmp:ModifyDate>\n");
        sb.Append($"<xmp:CreatorTool>{X(m.Creator)}</xmp:CreatorTool>\n</rdf:Description>\n");
        sb.Append("<rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
        sb.Append($"<pdf:Producer>{X(producer)}</pdf:Producer>\n");
        if (m.Keywords.Length > 0) sb.Append($"<pdf:Keywords>{X(m.Keywords)}</pdf:Keywords>\n");
        sb.Append("</rdf:Description>\n");
        sb.Append("<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
        sb.Append("<dc:format>application/pdf</dc:format>\n");
        if (m.Title.Length > 0) sb.Append($"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{X(m.Title)}</rdf:li></rdf:Alt></dc:title>\n");
        if (m.Author.Length > 0) sb.Append($"<dc:creator><rdf:Seq><rdf:li>{X(m.Author)}</rdf:li></rdf:Seq></dc:creator>\n");
        if (m.Subject.Length > 0) sb.Append($"<dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">{X(m.Subject)}</rdf:li></rdf:Alt></dc:description>\n");
        sb.Append("</rdf:Description>\n</rdf:RDF>\n</x:xmpmeta>\n");
        // Padding lets other tools edit the packet in place, per the XMP spec.
        for (int i = 0; i < 20; i++) sb.Append(new string(' ', 99)).Append('\n');
        sb.Append("<?xpacket end=\"w\"?>");
        return sb.ToString();
    }

    private static string FormatOffset(DateTime local)
    {
        TimeSpan off = TimeZoneInfo.Local.GetUtcOffset(local);
        return (off < TimeSpan.Zero ? "-" : "+") + off.ToString("hh\\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>The Producer string PDFsharp stamps on save (found by saving a probe
    /// document once), so Info and XMP can be made to agree.</summary>
    private static readonly Lazy<string> PdfSharpProducer = new(() =>
    {
        try
        {
            using var probe = new PdfDocument();
            probe.AddPage();
            using var ms = new MemoryStream();
            probe.Save(ms, false);
            ms.Position = 0;
            using PdfDocument back = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            return back.Info.Producer;
        }
        catch
        {
            return "PDFsharp";
        }
    });

    /// <summary>sRGB ICC profile: PDFsharp ships the ICC's freely redistributable
    /// sRGB2014.icc as a resource; reuse it rather than vendoring another copy.</summary>
    private static readonly Lazy<byte[]> SrgbProfile = new(() =>
    {
        var asm = typeof(PdfDocument).Assembly;
        string? name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("sRGB2014.icc", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("sRGB ICC profile not found in PDFsharp resources.");
        using Stream s = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    });

    #endregion

    private PdfArray IntArray(params int[] values)
    {
        var a = new PdfArray(_doc);
        foreach (int v in values) a.Elements.Add(new PdfInteger(v));
        return a;
    }

    private static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data);
        return ms.ToArray();
    }
}
