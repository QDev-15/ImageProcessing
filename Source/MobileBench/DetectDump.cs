using System.Globalization;
using ImageCore.Shared.Tests;
using ImageCoreService;

/// <summary>Runs the edge detector on 40 varied synthetic scenes and prints every result with
/// round-trip precision. Diffing the output before / after a change proves the change did not
/// alter a single detection.</summary>
internal static class DetectDump
{
    public static void Run(string path)
    {
        var rnd = new Random(12345);
        var detector = new DocumentEdgeDetector();
        using var w = new StreamWriter(path);
        for (int k = 0; k < 40; k++)
        {
            int iw = 640, ih = 480;
            double cx = iw * (0.35 + 0.3 * rnd.NextDouble()), cy = ih * (0.35 + 0.3 * rnd.NextDouble());
            double sw = iw * (0.25 + 0.35 * rnd.NextDouble()), sh = ih * (0.35 + 0.5 * rnd.NextDouble());
            double a = (rnd.NextDouble() - 0.5) * 0.8, ca = Math.Cos(a), sa = Math.Sin(a);
            PointD P(double x, double y) => new(cx + x * ca - y * sa + (rnd.NextDouble() - 0.5) * 20, cy + x * sa + y * ca + (rnd.NextDouble() - 0.5) * 20);
            var q = new Quad(P(-sw / 2, -sh / 2), P(sw / 2, -sh / 2), P(sw / 2, sh / 2), P(-sw / 2, sh / 2));
            byte B() => (byte)rnd.Next(20, 230);
            var opts = new SceneBuilder.Options((B(), B(), B()), ((byte)rnd.Next(215, 255), (byte)rnd.Next(210, 255), (byte)rnd.Next(200, 250)),
                Stripes: rnd.Next(2) == 0, Shadow: rnd.NextDouble() * 0.3, Noise: rnd.Next(0, 10), Clutter: rnd.Next(3) == 0,
                DropShadow: rnd.Next(3) == 0 ? rnd.Next(4, 16) : 0, Seed: k);
            QuadDetection d = detector.Detect(SceneBuilder.Render(iw, ih, q, opts));
            string pts = string.Join(' ', d.Quad.ToArray().Select(p => p.X.ToString("R", CultureInfo.InvariantCulture) + "," + p.Y.ToString("R", CultureInfo.InvariantCulture)));
            w.WriteLine($"{k} {d.Detected} {d.Confidence.ToString("R", CultureInfo.InvariantCulture)} {pts}");
        }
    }
}
