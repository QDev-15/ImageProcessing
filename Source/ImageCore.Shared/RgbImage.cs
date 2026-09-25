namespace ImageCoreService;

/// <summary>8-bit RGB buffer (interleaved R,G,B; row-major, no padding). Platform-neutral
/// twin of <see cref="GrayImage"/> for the steps that need color (edge detection, unwarp).</summary>
public sealed class RgbImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }

    public RgbImage(int width, int height, byte[]? data = null)
    {
        Width = width;
        Height = height;
        Data = data ?? new byte[width * height * 3];
        if (Data.Length != width * height * 3) throw new ArgumentException("Buffer size mismatch.", nameof(data));
    }

    /// <summary>From Android-style packed ARGB ints (alpha ignored).</summary>
    public static RgbImage FromArgb(int[] argb, int width, int height)
    {
        if (argb.Length < width * height) throw new ArgumentException("Pixel buffer too small.", nameof(argb));
        var img = new RgbImage(width, height);
        for (int i = 0, o = 0; i < width * height; i++, o += 3)
        {
            int p = argb[i];
            img.Data[o] = (byte)(p >> 16);
            img.Data[o + 1] = (byte)(p >> 8);
            img.Data[o + 2] = (byte)p;
        }
        return img;
    }

    public GrayImage ToGray()
    {
        var g = new GrayImage(Width, Height);
        for (int i = 0, o = 0; i < g.Data.Length; i++, o += 3)
            g.Data[i] = (byte)((Data[o] * 299 + Data[o + 1] * 587 + Data[o + 2] * 114 + 500) / 1000);
        return g;
    }

    /// <summary>Resamples to exactly <paramref name="width"/> x <paramref name="height"/>: an integer box
    /// filter down to within 2x of the target (so a big shrink does not alias), then bilinear.</summary>
    public RgbImage Resize(int width, int height)
    {
        if (width == Width && height == Height) return this;
        int factor = Math.Max(1, Math.Min(Width / width, Height / height));
        RgbImage s = Downscale(factor);
        var dst = new RgbImage(width, height);
        double kx = (double)s.Width / width, ky = (double)s.Height / height;
        for (int y = 0; y < height; y++)
        {
            double sy = (y + 0.5) * ky - 0.5;
            int y0 = Math.Clamp((int)Math.Floor(sy), 0, s.Height - 1), y1 = Math.Min(y0 + 1, s.Height - 1);
            double fy = Math.Clamp(sy - y0, 0, 1);
            for (int x = 0; x < width; x++)
            {
                double sx = (x + 0.5) * kx - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sx), 0, s.Width - 1), x1 = Math.Min(x0 + 1, s.Width - 1);
                double fx = Math.Clamp(sx - x0, 0, 1);
                for (int c = 0; c < 3; c++)
                {
                    double top = s.Data[(y0 * s.Width + x0) * 3 + c] * (1 - fx) + s.Data[(y0 * s.Width + x1) * 3 + c] * fx;
                    double bot = s.Data[(y1 * s.Width + x0) * 3 + c] * (1 - fx) + s.Data[(y1 * s.Width + x1) * 3 + c] * fx;
                    dst.Data[(y * width + x) * 3 + c] = (byte)(top * (1 - fy) + bot * fy + 0.5);
                }
            }
        }
        return dst;
    }

    /// <summary>Box-filter downscale by an integer factor (factor 1 returns this image).</summary>
    public RgbImage Downscale(int factor)
    {
        if (factor <= 1) return this;
        int w = Math.Max(1, Width / factor), h = Math.Max(1, Height / factor);
        var dst = new RgbImage(w, h);
        int area = factor * factor;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < factor; dy++)
                {
                    int o = ((y * factor + dy) * Width + x * factor) * 3;
                    for (int dx = 0; dx < factor; dx++, o += 3)
                    {
                        r += Data[o];
                        g += Data[o + 1];
                        b += Data[o + 2];
                    }
                }
                int d = (y * w + x) * 3;
                dst.Data[d] = (byte)(r / area);
                dst.Data[d + 1] = (byte)(g / area);
                dst.Data[d + 2] = (byte)(b / area);
            }
        });
        return dst;
    }
}
