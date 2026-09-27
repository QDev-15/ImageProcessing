using System.Diagnostics;
using DocScanner.Core;
using ImageCore.Shared.Tests;
using ImageCoreService;

// Median-of-N timings of each managed stage of the mobile pipeline.
if (args.Length == 2 && args[0] == "detect-dump") { DetectDump.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "scene-bmp") { SceneFile.Write(args[1]); return; }
int runs = args.Length > 0 ? int.Parse(args[0]) : 5;
Console.WriteLine($"MobileBench  {DateTime.Now:yyyy-MM-dd HH:mm}  cores={Environment.ProcessorCount}  runs={runs}");

var opts = new SceneBuilder.Options((70, 60, 50), (240, 236, 225), Stripes: true, Shadow: 0.2, Noise: 6);
Quad Sheet(int w, int h) => new(new PointD(w * 0.22, h * 0.08), new PointD(w * 0.78, h * 0.12), new PointD(w * 0.74, h * 0.93), new PointD(w * 0.18, h * 0.88));

var sw = Stopwatch.StartNew();
RgbImage proxy = SceneBuilder.Render(1600, 1200, Sheet(1600, 1200), opts);
RgbImage photo = SceneBuilder.Render(4000, 3000, Sheet(4000, 3000), opts);
Console.WriteLine($"(scene generation {sw.ElapsedMilliseconds} ms, not timed)");

double Time(string name, Action a)
{
    a(); // warm-up (JIT)
    var t = new List<double>();
    for (int i = 0; i < runs; i++)
    {
        var s = Stopwatch.StartNew();
        a();
        t.Add(s.Elapsed.TotalMilliseconds);
    }
    t.Sort();
    double med = t[t.Count / 2];
    Console.WriteLine($"{name,-44} {med,8:0.0} ms   (min {t[0]:0.0}, max {t[^1]:0.0})");
    return med;
}

var detector = new DocumentEdgeDetector();
RgbImage analysis = proxy.Resize(480, 360);
Time("proxy 1600 -> 480 resize", () => proxy.Resize(480, 360));
Time("edge detection (480 px)", () => detector.Detect(analysis));

Quad q = Sheet(4000, 3000);
(int aw, int ah) = PerspectiveWarp.A4Size(q, CropPlanner.MaxLongEdge, CropPlanner.MaxPixels);
RgbImage flat = PerspectiveWarp.Warp(photo, q, aw, ah);
Time($"warp 12 MP -> A4 {aw}x{ah}", () => PerspectiveWarp.Warp(photo, q, aw, ah));

GrayImage gray = flat.ToGray();
Time("ToGray (A4)", () => flat.ToGray());
Time("filter Gray (flatten)", () => DocumentFilter.Apply(flat, new FilterOptions(PageColorMode.Gray), 300));
Time("  BackgroundFlattener.Flatten(gray)", () => BackgroundFlattener.Flatten(gray));
Time("filter BlackWhite (flatten+sauvola+despeckle)", () => DocumentFilter.Apply(flat, new FilterOptions(PageColorMode.BlackWhite), 300));
Time("  Binarizer.Sauvola", () => Binarizer.Sauvola(gray, Binarizer.DefaultWindow(300), 0.34));
GrayImage bin = Binarizer.Sauvola(gray, Binarizer.DefaultWindow(300), 0.34);
Time("  Despeckle", () => DocumentCleanup.Despeckle(new GrayImage(bin.Width, bin.Height, (byte[])bin.Data.Clone()), DocumentCleanup.DefaultSpeckleArea(300)));
Time("PngWriter.EncodeBilevel (A4)", () => PngWriter.EncodeBilevel(bin));
Time("RgbImage.FromGray (A4, gray JPEG path)", () => RgbImage.FromGray(gray));
Time("thumbnail color: Resize A4 -> 512", () => flat.Resize(362, 512));
Time("thumbnail gray: Downscale+FromGray+Resize", () => RgbImage.FromGray(gray.Downscale(6)).Resize(362, 512));

// The result screen's live preview (CropPlanner.PreviewLongEdge): what a tap on Gray / B&W or a darkness step costs.
(int pw, int ph) = PerspectiveWarp.A4Size(q, CropPlanner.PreviewLongEdge, (long)CropPlanner.PreviewLongEdge * CropPlanner.PreviewLongEdge);
RgbImage preview = PerspectiveWarp.Warp(photo, q, pw, ph);
int previewDpi = CropRenderService.PageDpi(pw, ph);
Time($"preview warp 12 MP -> {pw}x{ph}", () => PerspectiveWarp.Warp(photo, q, pw, ph));
Time("preview filter Gray", () => DocumentFilter.Apply(preview, new FilterOptions(PageColorMode.Gray), previewDpi));
Time("preview filter BlackWhite", () => DocumentFilter.Apply(preview, new FilterOptions(PageColorMode.BlackWhite), previewDpi));
Time("tone LUT on A4 color page (save)", () => new ToneAdjust(20, 15).Apply(new RgbImage(flat.Width, flat.Height, flat.Data)));

// Staged preview (LookPreview): first black-and-white look vs. a darkness / brightness step (threshold only) vs. a turn.
var staged = new LookPreview(preview);
var bwLook = new FilterOptions(PageColorMode.BlackWhite);
Time("staged: first B&W (gray+flatten+stats+threshold)", () => new LookPreview(preview).Render(bwLook));
staged.Render(bwLook);
int step = 0;
Time("staged: darkness step (threshold only)", () => staged.Render(new FilterOptions(PageColorMode.BlackWhite, 30 + (step++ % 40))));
Time("staged: brightness step (threshold only)", () => staged.Render(bwLook with { Tone = new ToneAdjust(step++ % 50, 0) }));
Time("staged: quarter turn (all stages)", () => staged.RotateClockwise(1));
Time("old: B&W filter from scratch (per step before)", () => DocumentFilter.Apply(preview, bwLook, previewDpi));
