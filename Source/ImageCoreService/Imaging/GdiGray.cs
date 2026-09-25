using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// GDI+ (System.Drawing.Common) bridge for <see cref="GrayImage"/>. The pure grayscale
/// buffer lives in ImageCore.Shared so the mobile app can use it without GDI+.
/// </summary>
public static class GdiGray
{
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
    public static unsafe Bitmap ToBitmap8bpp(this GrayImage gray, float dpiX, float dpiY)
    {
        int width = gray.Width, height = gray.Height;
        var bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
        ColorPalette pal = bmp.Palette;
        for (int i = 0; i < 256; i++) pal.Entries[i] = Color.FromArgb(i, i, i);
        bmp.Palette = pal;
        bmp.SetResolution(dpiX, dpiY);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            for (int y = 0; y < height; y++)
                new ReadOnlySpan<byte>(gray.Data, y * width, width).CopyTo(new Span<byte>((byte*)bd.Scan0 + (long)y * bd.Stride, width));
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
    public static unsafe Bitmap ToBitmap1bpp(this GrayImage gray, float dpiX, float dpiY)
    {
        int width = gray.Width, height = gray.Height;
        byte[] data = gray.Data;
        var bmp = new Bitmap(width, height, PixelFormat.Format1bppIndexed);
        bmp.SetResolution(dpiX, dpiY);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
        try
        {
            Parallel.For(0, height, y =>
            {
                byte* row = (byte*)bd.Scan0 + (long)y * bd.Stride;
                new Span<byte>(row, bd.Stride).Clear();
                int o = y * width;
                for (int x = 0; x < width; x++)
                    if (data[o + x] != 0)
                        row[x >> 3] |= (byte)(0x80 >> (x & 7));
            });
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return bmp;
    }
}
