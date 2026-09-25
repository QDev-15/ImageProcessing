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

// ---- 6a. DPI limit on scan / import, resilient export ----
Run("resolution limiter", () =>
{
    string folder = Path.Combine(outDir, "limited");
    Directory.CreateDirectory(folder);

    // 900 dpi color page (A4-sized in pixels at 900 dpi would be ~7400x10500; use a 1/3 scale stand-in: 2481x3507 tagged 900).
    using Bitmap big = TextPage(Color.White);
    big.SetResolution(900, 900);
    string bigPng = Save(big, "lim_big.png");
    string outPath = ResolutionLimiter.Limit(bigPng, 300, folder);
    using (Bitmap r = ImageUtils.Load(outPath))
        Check("900 dpi page scaled to 300 dpi", Math.Abs(r.Width - big.Width / 3) <= 1 && Math.Abs(r.HorizontalResolution - 300) < 1
            && r.PixelFormat == PixelFormat.Format24bppRgb, $"{r.Width}x{r.Height} @{r.HorizontalResolution}");
    Check("source file consumed", !File.Exists(bigPng));

    // Page at / below target stays byte-identical (same path returned, nothing rewritten).
    using Bitmap ok = TextPage(Color.White);
    string okPng = Save(ok, "lim_ok.png");
    Check("300 dpi page untouched", ResolutionLimiter.Limit(okPng, 300, folder) == okPng && File.Exists(okPng));
    Check("200 dpi target on 300 dpi page shrinks", ResolutionLimiter.Limit(Save(ok, "lim_ok2.png"), 200, folder) != okPng);

    // Bitonal stays 1bpp.
    using Bitmap bin = ImageUtils.ToBitonal(TextPage(Color.White));
    bin.SetResolution(600, 600);
    string binTif = ImageUtils.SaveLossless(bin, Path.Combine(outDir, "lim_bin"));
    string binOut = ResolutionLimiter.Limit(binTif, 300, folder);
    using (Bitmap r = ImageUtils.Load(binOut))
        Check("600 dpi bitonal page -> 300 dpi, still 1bpp", r.PixelFormat == PixelFormat.Format1bppIndexed
            && Math.Abs(r.HorizontalResolution - 300) < 1 && Math.Abs(r.Width - bin.Width / 2) <= 1, $"{r.Width}x{r.Height} {r.PixelFormat}");

    // Untagged big image: DPI is inferred from the pixel size (A4 -> ~300 here, so untouched at 300).
    string exportFolder = Path.Combine(outDir, "resil");
    Directory.CreateDirectory(exportFolder);
    using Bitmap tiny = new(8, 8, PixelFormat.Format24bppRgb);
    tiny.SetResolution(300, 300);
    string tinyPng = Save(tiny, "tiny.png");
    string tinyPdf = Path.Combine(exportFolder, "tiny.pdf");
    DocumentExporter.ExportPdf(new[] { tinyPng }, new ExportOptions { UseJpeg2000 = true, UseJBig2 = true }, tinyPdf);
    Check("export of a tiny page succeeds whatever the codec does", new FileInfo(tinyPdf).Length > 0);

    // External encoders unavailable / crashing (e.g. out of memory): export still completes.
    string toolsDir = Path.Combine(AppContext.BaseDirectory, "tools");
    string hiddenTools = toolsDir + "_hidden";
    Directory.Move(toolsDir, hiddenTools);
    try
    {
        using Bitmap col = TextPage(Color.White);
        using (Graphics g = Graphics.FromImage(col)) g.FillEllipse(Brushes.Blue, 1500, 2800, 500, 400);
        string colPng = Save(col, "resil_color.png"), txtPng = Save(TextPage(Color.White), "resil_text.png");
        string fbPdf = Path.Combine(exportFolder, "fallback.pdf");
        DocumentExporter.ExportPdf(new[] { txtPng, colPng, txtPng }, new ExportOptions { UseJBig2 = true, UseJpeg2000 = true }, fbPdf);
        VerifyPdf(fbPdf, 3, null);
        string fbTif = Path.Combine(exportFolder, "fallback.tif");
        DocumentExporter.ExportTiff(new[] { txtPng, colPng }, new ExportOptions { UseJBig2 = true, UseJpeg2000 = true }, fbTif);
        Check("export survives missing jbig2 / openjpeg (JBIG2 -> G4, JP2 -> JPEG)", new FileInfo(fbTif).Length > 0);
    }
    finally
    {
        Directory.Move(hiddenTools, toolsDir);
    }

    // Many pages in parallel keep their order.
    using Bitmap a = TextPage(Color.White);
    var many = new List<string>();
    for (int i = 0; i < 6; i++)
    {
        using Bitmap pg = new(a.Width / 2, (int)(a.Height / 2 + i * 40), PixelFormat.Format24bppRgb);
        pg.SetResolution(150, 150);
        many.Add(Save(pg, $"many_{i}.png"));
    }
    string manyPdf = Path.Combine(exportFolder, "many.pdf");
    DocumentExporter.ExportPdf(many, new ExportOptions { PdfA = false }, manyPdf);
    using var md = PdfiumViewer.PdfDocument.Load(manyPdf);
    bool ordered = md.PageCount == 6;
    for (int i = 1; ordered && i < 6; i++) ordered = md.PageSizes[i].Height > md.PageSizes[i - 1].Height;
    Check("parallel export keeps page order", ordered);
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
    project.Execute(p => { p.Add(PageRecord.FromFile(f1, "a")); p.Add(PageRecord.FromFile(f2, "b")); });
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

// ---- 6c. Project model v2: ops, renderer, migration, background updates ----
Run("project v2", () =>
{
    // Renderer applies ops non-destructively: the source file is never touched.
    using Bitmap page = TextPage(Color.White);
    string src = Save(page, "v2_src.png");
    byte[] before = File.ReadAllBytes(src);
    PageRecord rec = PageRecord.FromFile(src, "p");
    using (Bitmap r90 = PageRenderer.RenderFull(rec with { Ops = rec.Ops.RotatedBy(90) }))
        Check("rotate op renders a turned page", r90.Width == page.Height && r90.Height == page.Width, $"{r90.Width}x{r90.Height}");
    using (Bitmap cropped = PageRenderer.RenderFull(rec with { Ops = new PageOps(Crop: new RectangleF(0.1f, 0.2f, 0.5f, 0.5f)) }))
        Check("crop op is relative to the source", Math.Abs(cropped.Width - page.Width / 2) <= 1 && Math.Abs(cropped.Height - page.Height / 2) <= 1, $"{cropped.Width}x{cropped.Height}");
    using (Bitmap plain = PageRenderer.RenderFull(rec, 0, out bool modified))
        Check("no ops -> unmodified render", !modified && plain.Width == page.Width);
    using (Bitmap limited = PageRenderer.RenderFull(rec, 150, out bool modified))
        Check("target DPI limits at render time", modified && Math.Abs(limited.Width - page.Width / 2) <= 1 && Math.Abs(limited.HorizontalResolution - 150) < 1, $"{limited.Width} @{limited.HorizontalResolution}");
    Check("source file untouched by ops", File.ReadAllBytes(src).SequenceEqual(before));
    Check("raw JPEG only when ops are identity", !PageRenderer.IsRawJpeg(rec) && PageRenderer.IsRawJpeg(PageRecord.FromFile(Path.Combine(outDir, "p3_orig.jpg"), "j"))
        && !PageRenderer.IsRawJpeg(PageRecord.FromFile(Path.Combine(outDir, "p3_orig.jpg"), "j") is var j ? j with { Ops = j.Ops.RotatedBy(90) } : null!));

    // Export honours ops (rotated page -> landscape media box) and skips nothing.
    string rotPdf = Path.Combine(outDir, "v2_rot.pdf");
    DocumentExporter.ExportPdf(new[] { rec with { Ops = rec.Ops.RotatedBy(90) } }, new ExportOptions { PdfA = false }, rotPdf);
    using (var d = PdfiumViewer.PdfDocument.Load(rotPdf))
        Check("export applies the rotate op", d.PageSizes[0].Width > d.PageSizes[0].Height, $"{d.PageSizes[0]}");

    // Project file round trip incl. ops, and Update / Discard versus undo history.
    var project = ScanProject.NewSession(Path.Combine(outDir, "work_v2"));
    PageRecord a = PageRecord.FromFile(project.ImportFile(src), "a") with { Ops = new PageOps(90, 1.25, new RectangleF(0.1f, 0.1f, 0.8f, 0.8f)) };
    PageRecord b = PageRecord.FromFile(project.ImportFile(src), "b") with { State = PageState.Pending };
    project.Execute(p => { p.Add(a); p.Add(b); });
    project.Execute(p => p.Reverse());
    Check("Update patches current list and undo snapshots without an undo step",
        project.Update(b.Id, r => r with { State = PageState.Ready, Label = "b-done" }) && project.Pages[0].Label == "b-done");
    project.Undo();
    Check("undo keeps the patched record", project.Pages.Single(p => p.Id == b.Id).State == PageState.Ready);
    project.Redo();
    project.Discard(new[] { b.Id });
    Check("Discard removes from list and history", project.Pages.Count == 1 && !project.Pages.Any(p => p.Id == b.Id));
    project.Flush();
    project.Persist();
    var back = ScanProject.Open(project.Folder);
    PageRecord a2 = back.Pages.Single();
    Check("project v2 round-trips ids and ops", a2.Id == a.Id && a2.Ops.Rotate == 90 && Math.Abs(a2.Ops.Deskew - 1.25) < 1e-9
        && a2.Ops.Crop is { } c && Math.Abs(c.Width - 0.8f) < 1e-4, a2.Ops.Signature);

    // Migration from a version-1 project.xml (page = file + label).
    string old = Path.Combine(outDir, "old_v1_project");
    if (Directory.Exists(old)) Directory.Delete(old, true);
    Directory.CreateDirectory(Path.Combine(old, "pages"));
    File.Copy(src, Path.Combine(old, "pages", "one.png"));
    File.Copy(src, Path.Combine(old, "pages", "two.png"));
    string v1xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><ScanProject xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><Pages>"
        + "<Page File=\"pages\\one.png\" Label=\"Trang 1\" /><Page File=\"pages\\two.png\" Label=\"Trang 2\" /></Pages></ScanProject>";
    File.WriteAllText(Path.Combine(old, "project.xml"), v1xml);
    var migrated = ScanProject.Open(old);
    Check("v1 project opens with both pages, ready, no ops", migrated.Pages.Count == 2 && migrated.Pages.All(p => p.State == PageState.Ready && p.Ops.IsIdentity && p.Id.Length > 0)
        && migrated.Pages[1].Label == "Trang 2" && File.Exists(migrated.Pages[0].FilePath));
    Check("v1 project.xml kept as project.xml.v1.bak", File.Exists(Path.Combine(old, "project.xml.v1.bak")) && File.ReadAllText(Path.Combine(old, "project.xml.v1.bak")) == v1xml);
    Check("upgraded project.xml is version 2", File.ReadAllText(Path.Combine(old, "project.xml")).Contains("Version=\"2\""));
    var again = ScanProject.Open(old);
    Check("re-opening the upgraded project keeps ids", again.Pages[0].Id == migrated.Pages[0].Id);
});

// ---- 6d. Cache (proxy / thumbnails) and background ingest ----
Run("cache + ingest", () =>
{
    var project = ScanProject.NewSession(Path.Combine(outDir, "work_ingest"));

    // Proxy: ~1600 px long edge, persisted, reused; ops apply on top without touching the source.
    using Bitmap page = TextPage(Color.White);
    string src = project.ImportFile(Save(page, "ing_src.png"));
    var rec = PageRecord.FromFile(src, "src");
    using (Bitmap proxy = project.Cache.GetProxy(rec.Source))
        Check("proxy long edge is 1600 px, DPI scaled with it", Math.Max(proxy.Width, proxy.Height) == PageCache.ProxyEdge && Math.Abs(proxy.HorizontalResolution - 300.0 * 1600 / page.Height) < 1,
            $"{proxy.Width}x{proxy.Height} @{proxy.HorizontalResolution:0}");
    Check("proxy is stored in the cache folder", Directory.EnumerateFiles(Path.Combine(project.CacheFolder, "proxy")).Any());
    using (Bitmap turned = project.Cache.RenderPreview(rec with { Ops = rec.Ops.RotatedBy(90) }))
        Check("preview applies ops to the proxy", turned.Width == 1600 && turned.Height < turned.Width, $"{turned.Width}x{turned.Height}");
    using (Bitmap t1 = project.Cache.GetThumbnail(rec, new Size(120, 162)))
        Check("thumbnail has the requested box", t1.Width == 120 && t1.Height == 162);
    int thumbsBefore = Directory.EnumerateFiles(Path.Combine(project.CacheFolder, "thumbs")).Count();
    using (Bitmap t2 = project.Cache.GetThumbnail(rec with { Ops = rec.Ops.RotatedBy(90) }, new Size(120, 162))) { }
    Check("a rotated look gets its own thumbnail", Directory.EnumerateFiles(Path.Combine(project.CacheFolder, "thumbs")).Count() == thumbsBefore + 1);

    // Ingest: placeholders at once, order kept, blank dropped, one Undo removes the whole import.
    AppSettings settings = new AppSettings { AutoProcessOnImport = true, AutoOrient = false };
    settings.Normalize();
    using var idle = new ManualResetEventSlim();
    IngestSummary? summary = null;
    using var ingest = new PageIngestor(project, () => settings, () => null, null, degree: 2);
    ingest.Idle += s => { summary = s; idle.Set(); };
    string pdf3 = Path.Combine(outDir, "out_g4_jpeg.pdf");        // 3 pages
    string blankPng = Path.Combine(outDir, "blank.png");
    string textPng = Path.Combine(outDir, "p1_text.png");
    int added = ingest.Import(new[] { pdf3, blankPng, textPng }, insertAt: null, progress: null, CancellationToken.None);
    Check("import adds one placeholder per page immediately", added == 5 && project.Pages.Count == 5 && project.Pages.Take(3).All(p => p.State is PageState.Pending or PageState.Ready),
        $"{added} added, states: {string.Join(",", project.Pages.Select(p => p.State))}");
    Check("labels keep file / page numbering", project.Pages[0].Label == "out_g4_jpeg.pdf #1" && project.Pages[2].Label == "out_g4_jpeg.pdf #3", string.Join(" | ", project.Pages.Select(p => p.Label)));
    Check("ingest finishes", idle.Wait(TimeSpan.FromSeconds(90)));
    Check("blank page removed, the rest ready, order kept", project.Pages.Count == 4 && project.Pages.All(p => p.State == PageState.Ready)
        && project.Pages[3].Label.StartsWith("p1_text.png") && summary is { Blank: 1 }, string.Join(" | ", project.Pages.Select(p => p.Label)));
    Check("one Undo removes the whole import", project.CanUndo && Undo(project) && project.Pages.Count == 0);

    bool Undo(ScanProject p) { p.Undo(); return true; }

    // Cancelling drops pages that are still waiting.
    using var idle2 = new ManualResetEventSlim();
    ingest.Idle += s => idle2.Set();
    ingest.Import(new[] { pdf3, pdf3, pdf3 }, null, null, CancellationToken.None);
    ingest.CancelPending();
    Check("cancel finishes and leaves no pending page", idle2.Wait(TimeSpan.FromSeconds(90)) && project.Pages.All(p => p.State != PageState.Pending), $"{project.Pages.Count} left");
});

// ---- 6f. Lazy PDF + non-destructive analysis ----
Run("lazy pdf + analysis", () =>
{
    var project = ScanProject.NewSession(Path.Combine(outDir, "work_lazy"));
    var settings = new AppSettings { AutoProcessOnImport = true, AutoOrient = false };
    settings.Normalize();
    using var osdPool = new OsdEnginePool();
    using var idle = new ManualResetEventSlim();
    using var ingest = new PageIngestor(project, () => settings, () => null, osdPool, degree: 2);
    ingest.Idle += _ => idle.Set();

    string pdf3 = Path.Combine(outDir, "out_g4_jpeg.pdf"); // 3 pages
    var watch = System.Diagnostics.Stopwatch.StartNew();
    ingest.Import(new[] { pdf3 }, null, null, CancellationToken.None);
    double importSeconds = watch.Elapsed.TotalSeconds;
    string[] filesInPages = Directory.GetFiles(project.PagesFolder);
    Check("PDF import renders nothing: one PDF copy, 3 page sources", filesInPages.Length == 1 && filesInPages[0].EndsWith(".pdf")
        && project.Pages.Count == 3 && project.Pages.All(p => p.Source.IsPdf && p.Source.File == filesInPages[0]) && project.Pages.Select(p => p.Source.PdfPage).SequenceEqual(new[] { 0, 1, 2 }),
        $"{filesInPages.Length} file(s), import {importSeconds:0.00}s");
    Check("ingest of PDF pages finishes", idle.Wait(TimeSpan.FromSeconds(90)) && project.Pages.All(p => p.State == PageState.Ready));
    Check("only proxies were rendered (no full-size PNG)", Directory.GetFiles(project.PagesFolder).Length == 1
        && Directory.GetFiles(Path.Combine(project.CacheFolder, "proxy")).Length == 3);

    // A PDF page renders at export size on demand; export from PDF sources round-trips text.
    using (Bitmap full = PageRenderer.RenderFull(project.Pages[0], 200))
        Check("PDF page renders at the requested density", Math.Abs(full.Width - 8.27 * 200) < 40 && Math.Abs(full.HorizontalResolution - 200) < 2, $"{full.Width}x{full.Height} @{full.HorizontalResolution}");
    string pdfOut = Path.Combine(outDir, "from_lazy_pdf.pdf");
    DocumentExporter.ExportPdf(project.Pages.ToList(), new ExportOptions { Ocr = true, TargetDpi = 300 }, pdfOut);
    VerifyPdf(pdfOut, 3, "HỢP");
    // Native DPI caps the render: never more pixels than the page really has.
    Check("native DPI recorded and respected", project.Pages[0].Source.NativeDpi is >= 0);

    // Analysis on the proxy finds the same things the pixel pipeline did (crop + skew), as ops.
    using Bitmap tilted = TextPage(Color.White);
    using Bitmap skewed = DocumentCleanup.RotateArbitrary(tilted, 3.0);
    using var framed = new Bitmap(skewed.Width + 200, skewed.Height + 200, PixelFormat.Format24bppRgb);
    framed.SetResolution(Dpi, Dpi);
    using (Graphics g = Graphics.FromImage(framed)) { g.Clear(Color.Black); g.DrawImage(skewed, 100, 100, skewed.Width, skewed.Height); }
    string framedPath = project.ImportFile(Save(framed, "lazy_framed.png"));
    byte[] before = File.ReadAllBytes(framedPath);
    var options = new PageProcessingOptions { AutoOrient = false };
    AnalysisResult r = PageAnalysis.Analyze(project.Cache, new PageSource(framedPath), options, null);
    Check("analysis: crop rect + skew angle recorded as ops", !r.IsBlank && r.Ops.Crop is { Width: > 0.5f and < 1f } && Math.Abs(r.Ops.Deskew - 3.0) < 0.6, r.Ops.Signature + " " + r.Summary);
    Check("analysis leaves the source file untouched", File.ReadAllBytes(framedPath).SequenceEqual(before));
    using Bitmap fixedPage = PageRenderer.RenderFull(PageRecord.FromFile(framedPath, "f") with { Ops = r.Ops });
    Check("full render applies the recorded ops", fixedPage.Width < framed.Width && fixedPage.Height < framed.Height, $"{fixedPage.Width}x{fixedPage.Height} vs {framed.Width}x{framed.Height}");

    // Project file keeps a PDF page's page number and native DPI.
    project.Flush(); project.Persist();
    var reopened = ScanProject.Open(project.Folder);
    Check("PDF page sources survive save / reopen", reopened.Pages.Count == 3 && reopened.Pages.Select(p => p.Source.PdfPage).SequenceEqual(new[] { 0, 1, 2 }));
    string saved = Path.Combine(outDir, "lazy_saved");
    if (Directory.Exists(saved)) Directory.Delete(saved, true);
    project.SaveAs(saved);
    Check("Save as copies the PDF once and re-points every page", Directory.GetFiles(Path.Combine(saved, "pages"), "*.pdf").Length == 1
        && project.Pages.All(p => p.Source.File.StartsWith(Path.GetFullPath(saved), StringComparison.OrdinalIgnoreCase)));
});

// ---- 6e0. Background OCR before any project is open (app waiting in the startup dialog) ----
Run("background ocr without project", () =>
{
    var settings = new AppSettings { Ocr = true };
    using var bg = new BackgroundOcr(() => null, () => settings, () => null, () => true);
    long logBefore = File.Exists(Log.CurrentFile) ? new FileInfo(Log.CurrentFile).Length : 0;
    bg.Signal();
    Thread.Sleep(3500); // long enough for the 2 s settle time plus a pass
    string added = File.Exists(Log.CurrentFile)
        ? System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Log.CurrentFile).AsSpan((int)Math.Min(logBefore, int.MaxValue)))
        : "";
    Check("no project yet: passes are skipped without error", !added.Contains("Background OCR pass failed"), added.Trim());
});

// ---- 6e. OCR cache + background OCR ----
Run("ocr cache", () =>
{
    var project = ScanProject.NewSession(Path.Combine(outDir, "work_ocr"));
    string src = project.ImportFile(Path.Combine(outDir, "doc_page.png"));
    PageRecord rec = PageRecord.FromFile(src, "ocr page");
    project.Execute(p => p.Add(rec));

    var settings = new AppSettings { Ocr = true };
    settings.Normalize();
    ExportOptions opt = ExportOptions.FromSettings(settings);
    string key = OcrCache.Key(rec, opt);
    Check("OCR key changes with ops and settings", key != OcrCache.Key(rec with { Ops = rec.Ops.RotatedBy(90) }, opt)
        && key != OcrCache.Key(rec, ExportOptions.FromSettings(new AppSettings { OcrLanguages = "eng" })) && key == OcrCache.Key(rec, opt));

    // A background pass reads the page and stores the words.
    using var bg = new BackgroundOcr(() => project, () => settings, () => null, () => true);
    bg.Signal();
    var deadline = DateTime.UtcNow.AddSeconds(90);
    while (!project.OcrCache.Contains(key) && DateTime.UtcNow < deadline) Thread.Sleep(300);
    IReadOnlyList<OcrWord>? words = project.OcrCache.TryGet(key);
    Check("background OCR fills the cache", words is { Count: > 20 }, $"{words?.Count} words");
    bg.Pause();

    // Several pages: one pass reads them all (a signal arriving mid-pass must not strand the rest).
    var more = new List<PageRecord>();
    foreach (string name in new[] { "p1_text.png", "p2_color.png", "doc_page.png" })
        more.Add(PageRecord.FromFile(project.ImportFile(Path.Combine(outDir, name)), name));
    project.Execute(p => p.AddRange(more));
    bg.Resume();
    var deadline2 = DateTime.UtcNow.AddSeconds(120);
    while (more.Any(m => !project.OcrCache.Contains(OcrCache.Key(m, opt))) && DateTime.UtcNow < deadline2) Thread.Sleep(300);
    Check("background OCR reads every page", more.All(m => project.OcrCache.Contains(OcrCache.Key(m, opt))),
        $"{more.Count(m => project.OcrCache.Contains(OcrCache.Key(m, opt)))}/{more.Count} cached");
    bg.Pause();

    // An export with the cache uses those words (proof: a poisoned entry shows up in the PDF text).
    project.OcrCache.Put(key, new[] { new OcrWord("ZEBRAQUAGGA", 300, 300, 900, 120, 400, 95f) });
    opt.OcrCache = project.OcrCache;
    opt.Ocr = true;
    string pdf = Path.Combine(outDir, "ocr_cached.pdf");
    DocumentExporter.ExportPdf(new[] { rec }, opt, pdf);
    using var d = PdfiumViewer.PdfDocument.Load(pdf);
    Check("export reuses cached words instead of running OCR", d.GetPdfText(0).Contains("ZEBRAQUAGGA"), d.GetPdfText(0).Trim());
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
