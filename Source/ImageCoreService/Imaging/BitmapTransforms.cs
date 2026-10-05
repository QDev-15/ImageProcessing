using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>GDI+ rotate / crop of whole bitmaps. The analysis side (skew angle, border
/// bounds) is <see cref="DocumentCleanup"/> in ImageCore.Shared.</summary>
public static class BitmapTransforms
{
    /// <summary>Rotates by <paramref name="angleDeg"/> (positive = clockwise) about the
    /// center, same canvas size, white background, bicubic. Output 24bpp.</summary>
    public static Bitmap RotateArbitrary(Bitmap src, double angleDeg)
    {
        var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
        dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);
        using Graphics g = Graphics.FromImage(dst);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TranslateTransform(src.Width / 2f, src.Height / 2f);
        g.RotateTransform((float)angleDeg);
        g.TranslateTransform(-src.Width / 2f, -src.Height / 2f);
        g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
        return dst;
    }

    /// <summary>Lossless 90/180/270 rotation (clockwise). Keeps pixel format and DPI
    /// (swapped for 90/270).</summary>
    public static Bitmap RotateRight(Bitmap src, int degreesClockwise)
    {
        var copy = (Bitmap)src.Clone();
        float dx = src.HorizontalResolution, dy = src.VerticalResolution;
        switch (((degreesClockwise % 360) + 360) % 360)
        {
            case 90: copy.RotateFlip(RotateFlipType.Rotate90FlipNone); copy.SetResolution(dy, dx); break;
            case 180: copy.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
            case 270: copy.RotateFlip(RotateFlipType.Rotate270FlipNone); copy.SetResolution(dy, dx); break;
        }
        return copy;
    }

    public static Bitmap Crop(Bitmap src, Rectangle r)
    {
        var dst = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
        dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);
        using Graphics g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
        return dst;
    }
}
