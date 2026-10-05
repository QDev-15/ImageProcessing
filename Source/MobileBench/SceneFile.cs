using ImageCore.Shared.Tests;
using ImageCoreService;

/// <summary>Writes a synthetic 4000x3000 "sheet on a desk" photo as a 24-bit BMP (for trying the app on an
/// emulator: convert it to JPEG and put it in the app's documents).</summary>
internal static class SceneFile
{
    public static void Write(string path)
    {
        int w = 4000, h = 3000;
        var q = new Quad(new PointD(w * 0.22, h * 0.08), new PointD(w * 0.78, h * 0.12), new PointD(w * 0.74, h * 0.93), new PointD(w * 0.18, h * 0.88));
        RgbImage img = SceneBuilder.Render(w, h, q, new SceneBuilder.Options((150, 60, 40), (240, 236, 225), Stripes: true, Noise: 4));
        int stride = (w * 3 + 3) & ~3;
        using var bw = new BinaryWriter(File.Create(path));
        bw.Write((byte)'B'); bw.Write((byte)'M'); bw.Write(54 + stride * h); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(stride * h);
        bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        var row = new byte[stride];
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 3;
                row[x * 3] = img.Data[o + 2]; row[x * 3 + 1] = img.Data[o + 1]; row[x * 3 + 2] = img.Data[o];
            }
            bw.Write(row);
        }
    }
}
