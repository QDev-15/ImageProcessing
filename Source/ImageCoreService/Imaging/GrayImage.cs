using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// 8-bit luminance buffer (row-major, no padding) -- the working representation for
/// every document-cleanup algorithm here, so none of them has to care about GDI+ pixel
/// formats. For binary images the convention is 0 = ink (black), 255 = paper (white).
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

    /// <summary>Any GDI+ format -> luminance (Rec.601). LockBits does the format
    /// conversion (1bpp/8bpp indexed/32bpp...) for us.</summary>
    public static unsafe GrayImage FromBitmap(Bitmap bmp)
    {
        var img = new GrayImage(bmp.Width, bmp.Height);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int w = bmp.Width;
            Parallel.For(0, bmp.Height, y =>
            {
                byte* row = (byte*)bd.Scan0 + (long)y * bd.Stride;
                int o = y * w;
                for (int x = 0; x < w; x++)
                {
                    byte b = row[x * 3], g = row[x * 3 + 1], r = row[x * 3 + 2];
                    img.Data[o + x] = (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
                }
            });
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return img;
    }

    /// <summary>8bpp grayscale-palette bitmap.</summary>
    public unsafe Bitmap ToBitmap8bpp(float dpiX, float dpiY)
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);
        ColorPalette pal = bmp.Palette;
        for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
        bmp.Palette = pal;
        bmp.SetResolution(dpiX, dpiY);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            for (int y = 0; y < Height; y++)
                new ReadOnlySpan<byte>(Data, y * Width, Width).CopyTo(new Span<byte>((byte*)bd.Scan0 + (long)y * bd.Stride, Width));
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return bmp;
    }

    /// <summary>
    /// Binary buffer (0 = ink) -> 1bpp bitmap. GDI+'s default 1bpp palette is
    /// index 0 = black, 1 = white, so paper pixels get their bit SET -- same convention
    /// the G4/TIFF packers already rely on (PhotometricInterpretation MINISWHITE after
    /// the G4 encoder inverts nothing).
    /// </summary>
    public unsafe Bitmap ToBitmap1bpp(float dpiX, float dpiY)
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format1bppIndexed);
        bmp.SetResolution(dpiX, dpiY);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
        try
        {
            int w = Width;
            Parallel.For(0, Height, y =>
            {
                byte* row = (byte*)bd.Scan0 + (long)y * bd.Stride;
                new Span<byte>(row, bd.Stride).Clear();
                int o = y * w;
                for (int x = 0; x < w; x++)
                    if (Data[o + x] != 0)
                        row[x >> 3] |= (byte)(0x80 >> (x & 7));
            });
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return bmp;
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
