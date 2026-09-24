using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using ImageCoreService;

// Headless end-to-end checks of ImageCoreService on synthetic pages.
// dotnet run --project Source/SmokeTests [-- outDir]
Console.OutputEncoding = System.Text.Encoding.UTF8;
string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "ioc_smoke");
Directory.CreateDirectory(outDir);
AppPaths.DataFolder = Path.Combine(outDir, "appdata");
int failures = 0;

void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -- " + detail : "")}");
    if (!ok) failures++;
}

void Run(string name, Action body)
{
    try { body(); }
    catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
}

const int Dpi = 300;
string[] lines =
{
    "CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM",
    "Độc lập - Tự do - Hạnh phúc",
    "HỢP ĐỒNG MUA BÁN HÀNG HOÁ",
    "Số hợp đồng: 2026/09/25-001",
    "Bên A cam kết giao hàng đúng thời hạn và chất lượng.",
    "Tổng giá trị hợp đồng: 1.250.000.000 đồng.",
    "The quick brown fox jumps over the lazy dog 0123456789.",
};

Bitmap TextPage(Color paper, bool gradient = false)
{
    int w = (int)(8.27 * Dpi), h = (int)(11.69 * Dpi);
    var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
    bmp.SetResolution(Dpi, Dpi);
    using Graphics g = Graphics.FromImage(bmp);
    if (gradient)
    {
        // Uneven lighting: bright left, dim yellowish right -- defeats any global threshold.
        using var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(250, 248, 240), Color.FromArgb(120, 110, 80), 0f);
        g.FillRectangle(br, 0, 0, w, h);
    }
    else g.Clear(paper);
    g.TextRenderingHint = TextRenderingHint.AntiAlias;
    using var font = new Font("Arial", 14, FontStyle.Regular, GraphicsUnit.Point);
    for (int rep = 0; rep < 4; rep++)
        for (int i = 0; i < lines.Length; i++)
            g.DrawString(lines[i], font, Brushes.Black, 250, 300 + (rep * lines.Length + i) * 110);
    return bmp;
}

string Save(Bitmap bmp, string name)
{
    string p = Path.Combine(outDir, name);
    bmp.Save(p, ImageFormat.Png);
    return p;
}

// ---- 1. Binarization on uneven lighting ----
Run("binarize", () =>
{
    using Bitmap page = TextPage(Color.White, gradient: true);
    GrayImage gray = GrayImage.FromBitmap(page);
    // Region with no text on the dim right side.
    var blank = new Rectangle(page.Width - 300, page.Height - 400, 200, 200);
    int InkIn(GrayImage b) { int n = 0; for (int y = blank.Top; y < blank.Bottom; y++) for (int x = blank.Left; x < blank.Right; x++) if (b[x, y] == 0) n++; return n; }
    int fixed128 = InkIn(Binarizer.Threshold(gray, 128));
    GrayImage sauvola = Binarizer.Binarize(gray, BinarizationMethod.Sauvola, Dpi);
    int sv = InkIn(sauvola);
    Check("Sauvola keeps dim paper white (fixed 128 does not)", sv < 50 && fixed128 > 10000, $"ink px sauvola={sv}, fixed128={fixed128}");
    using Bitmap b1 = sauvola.ToBitmap1bpp(Dpi, Dpi);
    Save(b1, "sauvola.png");
});

// ---- 2. Deskew ----
Run("deskew", () =>
{
    using Bitmap page = TextPage(Color.White);
    using Bitmap skewed = DocumentCleanup.RotateArbitrary(page, 3.0);
    double a = DocumentCleanup.DetectSkew(GrayImage.FromBitmap(skewed), Dpi);
    Check("DetectSkew finds +3 deg", Math.Abs(a - 3.0) < 0.3, $"angle={a:0.00}");
    using Bitmap fixedImg = DocumentCleanup.RotateArbitrary(skewed, -a);
    double after = DocumentCleanup.DetectSkew(GrayImage.FromBitmap(fixedImg), Dpi);
    Check("after correction ~0", Math.Abs(after) < 0.3, $"angle={after:0.00}");
    double none = DocumentCleanup.DetectSkew(GrayImage.FromBitmap(page), Dpi);
    Check("straight page -> 0", Math.Abs(none) < 0.2, $"angle={none:0.00}");
});

// ---- 3. Border crop ----
Run("crop", () =>
{
    using Bitmap page = TextPage(Color.White);
    using var framed = new Bitmap(page.Width + 200, page.Height + 160, PixelFormat.Format24bppRgb);
    framed.SetResolution(Dpi, Dpi);
    using (Graphics g = Graphics.FromImage(framed)) { g.Clear(Color.FromArgb(15, 15, 15)); g.DrawImage(page, 100, 80, page.Width, page.Height); }
    Rectangle r = DocumentCleanup.DetectContentBounds(GrayImage.FromBitmap(framed), Dpi);
    bool ok = Math.Abs(r.Left - 100) <= 8 && Math.Abs(r.Top - 80) <= 8 && Math.Abs(r.Right - (100 + page.Width)) <= 8 && Math.Abs(r.Bottom - (80 + page.Height)) <= 8;
    Check("DetectContentBounds finds the sheet", ok, r.ToString());
    Rectangle none = DocumentCleanup.DetectContentBounds(GrayImage.FromBitmap(page), Dpi);
    Check("no border -> full image", none == new Rectangle(0, 0, page.Width, page.Height), none.ToString());
});

// ---- 4. Blank + classification ----
Run("analyze", () =>
{
    using var blank = new Bitmap(2480, 3508, PixelFormat.Format24bppRgb);
    blank.SetResolution(Dpi, Dpi);
    var rnd = new Random(1);
    using (Graphics g = Graphics.FromImage(blank))
    {
        g.Clear(Color.FromArgb(245, 243, 236));
        for (int i = 0; i < 300; i++) g.FillRectangle(Brushes.DimGray, rnd.Next(2480), rnd.Next(3508), 2, 2); // dust
    }
    Check("blank page with dust is blank", PageAnalyzer.IsBlank(GrayImage.FromBitmap(blank), Dpi));
    using Bitmap text = TextPage(Color.White);
    Check("text page is not blank", !PageAnalyzer.IsBlank(GrayImage.FromBitmap(text), Dpi));
    using var oneLine = new Bitmap(2480, 3508, PixelFormat.Format24bppRgb);
    oneLine.SetResolution(Dpi, Dpi);
    using (Graphics g = Graphics.FromImage(oneLine)) { g.Clear(Color.White); using var f = new Font("Arial", 11); g.DrawString("Ghi chú: đã ký", f, Brushes.Black, 900, 1700); }
    Check("page with one short line is not blank", !PageAnalyzer.IsBlank(GrayImage.FromBitmap(oneLine), Dpi),
        $"ink%: line={PageAnalyzer.InkPercent(GrayImage.FromBitmap(oneLine), Dpi):0.0000}, dust={PageAnalyzer.InkPercent(GrayImage.FromBitmap(blank), Dpi):0.0000}");

    Check("text page -> Bitonal", PageAnalyzer.Classify(text, Dpi) == PageColorKind.Bitonal, PageAnalyzer.Classify(text, Dpi).ToString());
    using var color = (Bitmap)text.Clone();
    using (Graphics g = Graphics.FromImage(color)) g.FillEllipse(Brushes.Red, 1500, 2800, 500, 400); // red stamp
    Check("page with red stamp -> Color", PageAnalyzer.Classify(color, Dpi) == PageColorKind.Color, PageAnalyzer.Classify(color, Dpi).ToString());
    using var photo = (Bitmap)text.Clone();
    using (Graphics g = Graphics.FromImage(photo))
    using (var br = new LinearGradientBrush(new Rectangle(300, 2000, 1800, 1200), Color.Black, Color.White, 45f))
        g.FillRectangle(br, 300, 2000, 1800, 1200);
    Check("page with gray photo -> Gray", PageAnalyzer.Classify(photo, Dpi) == PageColorKind.Gray, PageAnalyzer.Classify(photo, Dpi).ToString());
});

// ---- 5. OCR + OSD ----
Run("ocr", () =>
{
    using Bitmap page = TextPage(Color.White);
    using var ocr = new OcrEngine("vie+eng");
    var words = ocr.Recognize(page);
    string all = string.Join(" ", words.Select(w => w.Text));
    Check("OCR reads Vietnamese", all.Contains("HỢP") && all.Contains("đồng"), $"{words.Count} words; sample: {all[..Math.Min(80, all.Length)]}");

    using Bitmap rot = DocumentCleanup.RotateRight(page, 90);
    int fix = ocr.DetectUprightRotation(rot);
    using Bitmap corrected = DocumentCleanup.RotateRight(rot, fix);
    int again = ocr.DetectUprightRotation(corrected);
    Check("OSD corrects a 90 deg page", fix == 270 && again == 0, $"fix={fix}, after={again}");
    using Bitmap upside = DocumentCleanup.RotateRight(page, 180);
    Check("OSD corrects an upside-down page", ocr.DetectUprightRotation(upside) == 180);
});

// ---- 6. Export ----
Run("export", () =>
{
    using Bitmap text = TextPage(Color.FromArgb(240, 235, 220));
    string textPng = Save(text, "p1_text.png");
    using Bitmap color = TextPage(Color.White);
    using (Graphics g = Graphics.FromImage(color)) g.FillEllipse(Brushes.Blue, 1500, 2800, 500, 400);
    string colorPng = Save(color, "p2_color.png");
    string colorJpg = Path.Combine(outDir, "p3_orig.jpg");
    color.Save(colorJpg, ImageFormat.Jpeg);
    var pages = new[] { textPng, colorPng, colorJpg };

    var opt = new ExportOptions { Ocr = true, PdfA = true, Metadata = new PdfMetadata { Author = "Smoke Test", Title = "Hợp đồng" } };
    string pdf1 = Path.Combine(outDir, "out_g4_jpeg.pdf");
    DocumentExporter.ExportPdf(pages, opt, pdf1);
    VerifyPdf(pdf1, 3, "HỢP");

    // Byte-for-byte passthrough of the original JPEG.
    byte[] jpg = File.ReadAllBytes(colorJpg);
    byte[] pdfBytes = File.ReadAllBytes(pdf1);
    Check("original JPEG embedded unchanged", IndexOf(pdfBytes, jpg) >= 0);

    var opt2 = new ExportOptions { UseJBig2 = true, UseJpeg2000 = true, Ocr = false, PdfA = true };
    string pdf2 = Path.Combine(outDir, "out_jbig2_jp2.pdf");
    DocumentExporter.ExportPdf(pages, opt2, pdf2);
    VerifyPdf(pdf2, 3, null);
    string g4Only = Path.Combine(outDir, "text_g4.pdf"), jb2Only = Path.Combine(outDir, "text_jbig2.pdf");
    DocumentExporter.ExportPdf(new[] { textPng, textPng }, new ExportOptions(), g4Only);
    DocumentExporter.ExportPdf(new[] { textPng, textPng }, new ExportOptions { UseJBig2 = true }, jb2Only);
    Check("JBIG2 text pages smaller than G4", new FileInfo(jb2Only).Length < new FileInfo(g4Only).Length,
        $"{new FileInfo(jb2Only).Length / 1024} KB vs {new FileInfo(g4Only).Length / 1024} KB");

    string meta = Path.Combine(outDir, "out_meta.pdf");
    DocumentExporter.ExportPdf(new[] { textPng }, new ExportOptions { Ocr = true, Metadata = new PdfMetadata { Title = "Hợp đồng số 1", Author = "Nguyễn Văn A", Subject = "Mua bán & <test>", Keywords = "hợp đồng, 2026" } }, meta);
    VerifyPdf(meta, 1, "HỢP");

    string tif = Path.Combine(outDir, "out.tif");
    DocumentExporter.ExportTiff(pages, new ExportOptions(), tif);
    using var t = BitMiracle.LibTiff.Classic.Tiff.Open(tif, "r");
    Check("TIFF has 3 pages", t != null && t.NumberOfDirectories() == 3, $"{t?.NumberOfDirectories()}");
    using Bitmap first = new Bitmap(tif);
    Check("TIFF page 1 readable by GDI+ at native DPI", Math.Abs(first.HorizontalResolution - Dpi) < 1, $"{first.HorizontalResolution}");
});

// ---- 6b. Page processor, splitter, naming, project ----
Run("processor", () =>
{
    using Bitmap page = TextPage(Color.White);
    using Bitmap skewed = DocumentCleanup.RotateArbitrary(page, 2.5);
    using var framed = new Bitmap(skewed.Width + 160, skewed.Height + 160, PixelFormat.Format24bppRgb);
    framed.SetResolution(Dpi, Dpi);
    using (Graphics g = Graphics.FromImage(framed)) { g.Clear(Color.Black); g.DrawImage(skewed, 80, 80, skewed.Width, skewed.Height); }
    string input = Save(framed, "raw_framed_skewed.png");
    using var osd = new OcrEngine(loadOcr: false);
    PageProcessResult r = PageProcessor.Process(input, new PageProcessingOptions(), osd, Path.Combine(outDir, "processed"));
    Check("processor crops + deskews", r.Changed && r.Summary.Contains("cắt viền") && r.Summary.Contains("nghiêng"), r.Summary);
    using (Bitmap outBmp = ImageUtils.Load(r.OutputPath))
        Check("processed page keeps 300 dpi, border gone", Math.Abs(outBmp.HorizontalResolution - Dpi) < 1 && outBmp.Width <= page.Width + 10, $"{outBmp.Width}x{outBmp.Height} ");

    using var blank = new Bitmap(2480, 3508, PixelFormat.Format24bppRgb);
    blank.SetResolution(Dpi, Dpi);
    using (Graphics g = Graphics.FromImage(blank)) g.Clear(Color.White);
    string blankPath = Save(blank, "blank.png");
    Check("processor flags blank page", PageProcessor.Process(blankPath, new PageProcessingOptions(), null, outDir).IsBlank);

    // Barcode separator sheet.
    var writer = new ZXing.BarcodeWriterPixelData { Format = ZXing.BarcodeFormat.CODE_128, Options = new ZXing.Common.EncodingOptions { Width = 1200, Height = 300, Margin = 20 } };
    ZXing.Rendering.PixelData px = writer.Write("SEP-INVOICE-42");
    using var sep = new Bitmap(2480, 3508, PixelFormat.Format24bppRgb);
    sep.SetResolution(Dpi, Dpi);
    using (var code = new Bitmap(px.Width, px.Height, PixelFormat.Format32bppRgb))
    {
        var bd = code.LockBits(new Rectangle(0, 0, px.Width, px.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        System.Runtime.InteropServices.Marshal.Copy(px.Pixels, 0, bd.Scan0, px.Pixels.Length);
        code.UnlockBits(bd);
        using Graphics g = Graphics.FromImage(sep); g.Clear(Color.White); g.DrawImage(code, 600, 1500, px.Width, px.Height);
    }
    string sepPath = Save(sep, "separator.png");
    string textPath = Save(page, "doc_page.png");
    var groups = DocumentSplitter.Split(new[] { textPath, sepPath, textPath, textPath }, DocumentSplitMode.Barcode, "SEP-", true);
    Check("barcode split -> 2 docs (1 + 2 pages)", groups.Count == 2 && groups[0].Pages.Count == 1 && groups[1].Pages.Count == 2 && groups[1].Barcode == "SEP-INVOICE-42",
        string.Join(" | ", groups.Select(g => $"{g.Pages.Count}p {g.Barcode}")));
    var byBlank = DocumentSplitter.Split(new[] { textPath, blankPath, textPath }, DocumentSplitMode.BlankPage, "", true);
    Check("blank-page split -> 2 docs", byBlank.Count == 2 && byBlank.All(g => g.Pages.Count == 1));

    string name = FileNamer.Build("HD_{yyyy}-{MM}-{dd}_{counter}_{barcode}", new DateTime(2026, 9, 25, 6, 0, 0), 7, "A/B");
    Check("file name pattern", name == "HD_2026-09-25_007_A_B", name);

    var project = ScanProject.NewSession(Path.Combine(outDir, "work"));
    string f1 = project.ImportFile(textPath), f2 = project.ImportFile(blankPath);
    project.Execute(p => { p.Add(new PageRecord(f1, "a")); p.Add(new PageRecord(f2, "b")); });
    project.Execute(p => p.Reverse());
    project.Undo();
    Check("undo restores order", project.Pages[0].Label == "a" && project.CanRedo);
    project.Redo();
    string saved = Path.Combine(outDir, "saved_project");
    if (Directory.Exists(saved)) Directory.Delete(saved, true);
    project.SaveAs(saved);
    var reopened = ScanProject.Open(saved);
    Check("project save / reopen", reopened.Pages.Count == 2 && reopened.Pages[0].Label == "b" && File.Exists(reopened.Pages[0].FilePath));
});

// ---- 7. Settings ----
Run("settings", () =>
{
    AppSettings s = SettingsStore.Load();
    s.UseJBig2 = true;
    s.JpegQuality = 77;
    SettingsStore.Save(s);
    AppSettings back = SettingsStore.Load();
    Check("settings XML round-trip", back.UseJBig2 && back.JpegQuality == 77 && back.ScanProfiles.Count > 0);
});

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
Console.WriteLine("Output: " + outDir);
return failures == 0 ? 0 : 1;

void VerifyPdf(string path, int pages, string? mustContain)
{
    using var doc = PdfiumViewer.PdfDocument.Load(path);
    Check($"{Path.GetFileName(path)} opens in pdfium with {pages} pages", doc.PageCount == pages, $"{doc.PageCount}");
    using (Image img = doc.Render(0, 50, 50, PdfiumViewer.PdfRenderFlags.CorrectFromDpi)) { }
    if (mustContain != null)
    {
        string txt = doc.GetPdfText(0);
        Check($"{Path.GetFileName(path)} text layer searchable", txt.Contains(mustContain), txt.Length > 60 ? txt[..60] : txt);
    }
    string raw = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
    Check($"{Path.GetFileName(path)} has PDF/A-2b XMP + OutputIntent",
        raw.Contains("<pdfaid:part>2</pdfaid:part>") && raw.Contains("/GTS_PDFA1") && raw.StartsWith("%PDF-1.7"));
}

static int IndexOf(byte[] hay, byte[] needle)
{
    for (int i = 0; i <= hay.Length - needle.Length; i++)
    {
        if (hay[i] != needle[0]) continue;
        if (hay.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
    }
    return -1;
}
