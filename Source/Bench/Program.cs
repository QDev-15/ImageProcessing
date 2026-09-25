using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using ImageCoreService;

// Repeatable benchmark of the document pipeline on synthetic A4 pages at 300 dpi.
//   dotnet run -c Release --project Source/Bench -- pages=40 label=baseline
// Prints a table and appends it to Bench/results/<label>.txt (next to the sources).
Console.OutputEncoding = Encoding.UTF8;
int pages = 40;
string label = "run";
foreach (string a in args)
{
    if (a.StartsWith("pages=")) pages = int.Parse(a[6..]);
    else if (a.StartsWith("label=")) label = a[6..];
}

string root = Path.Combine(Path.GetTempPath(), "ioc_bench");
if (Directory.Exists(root)) Directory.Delete(root, true);
Directory.CreateDirectory(root);
AppPaths.DataFolder = Path.Combine(root, "appdata");
var report = new StringBuilder();
void Say(string s) { Console.WriteLine(s); report.AppendLine(s); }

Say($"=== {label}  {DateTime.Now:yyyy-MM-dd HH:mm}  pages={pages}  cores={Environment.ProcessorCount} ===");

// ---------- synthetic data ----------
Bitmap MakePage(int seed, bool color)
{
    const int dpi = 300;
    int w = (int)(8.27 * dpi), h = (int)(11.69 * dpi);
    var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
    bmp.SetResolution(dpi, dpi);
    var rnd = new Random(seed);
    using Graphics g = Graphics.FromImage(bmp);
    g.Clear(Color.FromArgb(244, 240, 228));
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    using var font = new Font("Segoe UI", 34);
    string[] words = "hợp đồng mua bán hàng hoá số lượng giá trị thanh toán bên giao nhận thời hạn chất lượng cam kết điều khoản".Split(' ');
    for (int line = 0; line < 42; line++)
    {
        var sb = new StringBuilder();
        for (int k = 0; k < 9; k++) sb.Append(words[rnd.Next(words.Length)]).Append(' ');
        g.DrawString(sb.ToString(), font, Brushes.Black, 200, 260 + line * 72);
    }
    if (color)
    {
        using var red = new SolidBrush(Color.FromArgb(200, 30, 30));
        g.FillEllipse(red, 1500, 2800, 500, 400);
        using var blue = new SolidBrush(Color.FromArgb(40, 70, 200));
        g.FillRectangle(blue, 300, 3000, 700, 300);
    }
    return bmp;
}

var sw = Stopwatch.StartNew();
string dataDir = Path.Combine(root, "data");
Directory.CreateDirectory(dataDir);
var distinct = new List<string>();
for (int i = 0; i < 6; i++)
{
    using Bitmap b = MakePage(i, color: false);
    string p = Path.Combine(dataDir, $"text{i}.png");
    b.Save(p, ImageFormat.Png);
    distinct.Add(p);
}
string pdf = Path.Combine(dataDir, "doc.pdf");
DocumentExporter.ExportPdf(Enumerable.Range(0, pages).Select(i => distinct[i % distinct.Count]).ToList(), new ExportOptions { PdfA = false }, pdf);
var jpgs = new List<string>();
for (int i = 0; i < pages; i++)
{
    using Bitmap b = MakePage(100 + (i % 6), color: true);
    string p = Path.Combine(dataDir, $"scan{i:000}.jpg");
    b.Save(p, ImageFormat.Jpeg);
    jpgs.Add(p);
}
Say($"data generated in {sw.Elapsed.TotalSeconds:0.0}s  (PDF {new FileInfo(pdf).Length / 1024} KB, {pages} JPEG {new FileInfo(jpgs[0]).Length / 1024} KB each)");
Say("");

var results = new List<(string Name, double Seconds, string Note)>();
double Time(string name, Action body, Func<string>? noteFn = null)
{
    Perf.Reset();
    var t = Stopwatch.StartNew();
    body();
    double s = t.Elapsed.TotalSeconds;
    string note = noteFn?.Invoke() ?? "";
    results.Add((name, s, note));
    Say($"{name,-42} {s,8:0.00} s   {note}");
    foreach (var kv in Perf.Snapshot().OrderByDescending(k => k.Value.Ms))
        Say($"    {kv.Key,-24} {kv.Value.Ms / 1000.0,8:0.00} s  x{kv.Value.Count}");
    return s;
}

// ---------- the pipeline as the app runs it today ----------
using var osd = new OcrEngine(loadOcr: false);
using var osdPool = new OsdEnginePool();
var options = new PageProcessingOptions();
string pagesFolder = Path.Combine(root, "pages");
Directory.CreateDirectory(pagesFolder);
var imported = new List<string>();

Time($"import PDF ({pages} pages): render+save", () =>
{
    var files = PageImporter.Import(pdf, pagesFolder);
    imported.AddRange(files);
}, () => "PageImporter.Import");

var processed = new List<string>();
Time($"import PDF: limit + auto-process", () =>
{
    foreach (string f in imported)
    {
        string limited = ResolutionLimiter.Limit(f, 300, pagesFolder);
        PageProcessResult r = PageProcessor.Process(limited, options, osd, pagesFolder);
        processed.Add(r.OutputPath);
    }
}, () => "ResolutionLimiter + PageProcessor");

var jpgImported = new List<string>();
Time($"import {pages} JPEG: copy + limit + auto-process", () =>
{
    foreach (string j in jpgs)
    {
        string copy = PageImporter.Import(j, pagesFolder)[0];
        string limited = ResolutionLimiter.Limit(copy, 300, pagesFolder);
        PageProcessResult r = PageProcessor.Process(limited, options, osd, pagesFolder);
        jpgImported.Add(r.OutputPath);
    }
});

// ---------- the project pipeline (PageIngestor + PageCache): what the app does since M2 ----------
{
    var project = ScanProject.NewSession(Path.Combine(root, "work"));
    var settings = new AppSettings();
    settings.Normalize();
    int degree = Math.Max(1, Environment.ProcessorCount / 4);
    using var ingest = new PageIngestor(project, () => settings, () => null, osdPool, degree);
    using var idle = new ManualResetEventSlim();
    ingest.Idle += _ => idle.Set();

    void Ingest(string title, IReadOnlyList<string> files)
    {
        Perf.Reset();
        idle.Reset();
        var t = Stopwatch.StartNew();
        double firstReady = -1;
        Action<PageRecord> onUpdate = r => { if (firstReady < 0 && r.State == PageState.Ready) firstReady = t.Elapsed.TotalSeconds; };
        project.PageUpdated += onUpdate;
        ingest.Import(files, null, null, CancellationToken.None);
        double added = t.Elapsed.TotalSeconds;
        idle.Wait();
        double total = t.Elapsed.TotalSeconds;
        project.PageUpdated -= onUpdate;
        results.Add((title, total, ""));
        Say($"{title,-42} {total,8:0.00} s   list complete after {added:0.0}s, first page ready after {firstReady:0.0}s (workers={degree})");
        foreach (var kv in Perf.Snapshot().OrderByDescending(k => k.Value.Ms))
            Say($"    {kv.Key,-24} {kv.Value.Ms / 1000.0,8:0.00} s  x{kv.Value.Count}");
    }

    Ingest($"[project] import PDF ({pages} pages)", new[] { pdf });
    Ingest($"[project] import {pages} JPEG", jpgs);

    var pageList = project.Pages.ToList();
    var box = new Size(120, 162);
    Time($"[project] thumbnails cold ({pageList.Count} pages)", () =>
    {
        foreach (PageRecord p in pageList) { using Bitmap b = project.Cache.GetThumbnail(p, box); }
    });
    Time($"[project] thumbnails warm ({pageList.Count} pages)", () =>
    {
        foreach (PageRecord p in pageList) { using Bitmap b = project.Cache.GetThumbnail(p, box); }
    });
    Time("[project] preview proxy, cold (10 pages)", () =>
    {
        foreach (PageRecord p in pageList.Take(10)) { using Bitmap b = project.Cache.RenderPreview(p); }
    });
    Time("[project] preview proxy, warm (10 pages)", () =>
    {
        foreach (PageRecord p in pageList.Take(10)) { using Bitmap b = project.Cache.RenderPreview(p); }
    });
    Time("[project] preview full-size (10 pages)", () =>
    {
        foreach (PageRecord p in pageList.Take(10)) { using Bitmap b = PageRenderer.RenderFull(p); }
    });
    Time("[project] rotate 10 pages (op + new thumbnails)", () =>
    {
        foreach (PageRecord p in pageList.Take(10)) { using Bitmap b = project.Cache.GetThumbnail(p with { Ops = p.Ops.RotatedBy(90) }, box); }
    });
}

var all = processed.Concat(jpgImported).ToList();
Time($"thumbnails ({all.Count} pages, decode + scale)", () =>
{
    foreach (string f in all)
    {
        using Bitmap src = ImageUtils.Load(f);
        using Bitmap t = ImageUtils.MakeThumbnail(src, 120, 162);
    }
});

Time("preview decode (10 pages)", () =>
{
    foreach (string f in all.Take(10)) { using Bitmap src = ImageUtils.Load(f); }
});

int exportN = Math.Min(all.Count, 40);
string outPdf = Path.Combine(root, "out_plain.pdf");
Time($"export PDF {exportN} pages, G4+JPEG, no OCR", () =>
    DocumentExporter.ExportPdf(all.Take(exportN).ToList(), new ExportOptions { PdfA = true }, outPdf),
    () => $"{new FileInfo(outPdf).Length / 1024} KB");

string outJb = Path.Combine(root, "out_jb2.pdf");
Time($"export PDF {exportN} pages, JBIG2+JP2, no OCR", () =>
    DocumentExporter.ExportPdf(all.Take(exportN).ToList(), new ExportOptions { PdfA = true, UseJBig2 = true, UseJpeg2000 = true }, outJb),
    () => $"{new FileInfo(outJb).Length / 1024} KB");

int ocrN = Math.Min(all.Count, 12);
string outOcr = Path.Combine(root, "out_ocr.pdf");
Time($"export PDF {ocrN} pages, JBIG2+JP2 + OCR", () =>
    DocumentExporter.ExportPdf(all.Take(ocrN).ToList(), new ExportOptions { PdfA = true, UseJBig2 = true, UseJpeg2000 = true, Ocr = true }, outOcr),
    () => $"{new FileInfo(outOcr).Length / 1024} KB");

Say("");
string resultsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "results"));
if (!Directory.Exists(Path.GetDirectoryName(resultsDir)!)) resultsDir = Path.Combine(root, "results");
Directory.CreateDirectory(resultsDir);
File.AppendAllText(Path.Combine(resultsDir, label + ".txt"), report.ToString() + Environment.NewLine);
Console.WriteLine("Saved to " + Path.Combine(resultsDir, label + ".txt"));
try { Directory.Delete(root, true); } catch { }
