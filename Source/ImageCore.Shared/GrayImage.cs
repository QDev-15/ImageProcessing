using System.Drawing;

namespace ImageCoreService;

/// <summary>
/// 8-bit luminance buffer (row-major, no padding) -- the working representation for
/// every document-cleanup algorithm here, so none of them has to care about platform
/// pixel formats (GDI+ conversions live in ImageCoreService.GdiGray). For binary images the convention is 0 = ink (black), 255 = paper (white).
/// </summary>
public sealed class GrayImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }

    public GrayImage(int width, int height, byte[]? data = null)
    {
        Width = width;
        Height = height;
        Data = data ?? new byte[width * height];
        if (Data.Length != width * height) throw new ArgumentException("Buffer size mismatch.", nameof(data));
    }

    public byte this[int x, int y]
    {
        get => Data[y * Width + x];
        set => Data[y * Width + x] = value;
    }

    /// <summary>Box-filter downscale by an integer factor (factor 1 returns a copy).</summary>
    public GrayImage Downscale(int factor)
    {
        if (factor <= 1) return new GrayImage(Width, Height, (byte[])Data.Clone());
        int w = Math.Max(1, Width / factor), h = Math.Max(1, Height / factor);
        var dst = new GrayImage(w, h);
        int area = factor * factor;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int sum = 0;
                for (int dy = 0; dy < factor; dy++)
                {
                    int o = (y * factor + dy) * Width + x * factor;
                    for (int dx = 0; dx < factor; dx++) sum += Data[o + dx];
                }
                dst.Data[y * w + x] = (byte)(sum / area);
            }
        });
        return dst;
    }

    public GrayImage Crop(Rectangle r)
    {
        var dst = new GrayImage(r.Width, r.Height);
        for (int y = 0; y < r.Height; y++)
            Array.Copy(Data, (r.Y + y) * Width + r.X, dst.Data, y * r.Width, r.Width);
        return dst;
    }
}
