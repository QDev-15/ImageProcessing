namespace ImageCoreService;

/// <summary>
/// Straightens a photographed page: resamples the four-sided region <c>srcQuad</c> of a photo
/// into an upright rectangle. Pure managed code, same commercial-license position as the rest
/// of the library.
///
/// Coordinates follow the "pixel edge" convention: pixel <c>i</c> covers <c>[i, i+1)</c>, so a
/// point normalized to 0..1 times the image width lands exactly on the image edge. Each output
/// pixel is mapped through the projective transform to a source position and read with
/// bilinear interpolation; when the source is clearly denser than the output (a photo shrunk to
/// a page) it is read at 2x2 sub-positions and averaged, which keeps small text from aliasing.
///
/// The outline may reach beyond the photo (a sheet cut by the frame has its corners off-picture):
/// what falls outside the source is filled with white (paper), so the outline keeps its true shape
/// and only the part that was never photographed is blank.
/// </summary>
public static class PerspectiveWarp
{
    /// <param name="src">The photo (or the decoded part of it).</param>
    /// <param name="srcQuad">Corner points in <paramref name="src"/> pixels: TL, TR, BR, BL of the page as it
    /// should appear upright.</param>
    /// <param name="outWidth">Width of the result.</param>
    /// <param name="outHeight">Height of the result.</param>
    public static RgbImage Warp(RgbImage src, Quad srcQuad, int outWidth, int outHeight)
    {
        if (outWidth < 1 || outHeight < 1) throw new ArgumentOutOfRangeException(nameof(outWidth));

        // Output pixel position -> source position.
        var rect = new[] { new PointD(0, 0), new PointD(outWidth, 0), new PointD(outWidth, outHeight), new PointD(0, outHeight) };
        Homography h = Homography.FromPoints(rect, srcQuad.ToArray());
        double h0 = h[0], h1 = h[1], h2 = h[2], h3 = h[3], h4 = h[4], h5 = h[5], h6 = h[6], h7 = h[7], h8 = h[8];

        // How many source pixels one output pixel spans (average of the two directions).
        double ratio = (Dist(srcQuad.TopLeft, srcQuad.TopRight) / outWidth + Dist(srcQuad.TopLeft, srcQuad.BottomLeft) / outHeight) / 2;
        bool supersample = ratio > 1.25;

        var dst = new RgbImage(outWidth, outHeight);
        int sw = src.Width, sh = src.Height;
        byte[] sData = src.Data;
        byte[] dData = dst.Data;

        Parallel.For(0, outHeight, y =>
        {
            int o = y * outWidth * 3;
            for (int x = 0; x < outWidth; x++, o += 3)
            {
                double r = 0, g = 0, b = 0;
                if (supersample)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        double px = x + 0.25 + (k & 1) * 0.5, py = y + 0.25 + (k >> 1) * 0.5;
                        Sample(sData, sw, sh, h0, h1, h2, h3, h4, h5, h6, h7, h8, px, py, ref r, ref g, ref b);
                    }
                    r *= 0.25; g *= 0.25; b *= 0.25;
                }
                else
                {
                    Sample(sData, sw, sh, h0, h1, h2, h3, h4, h5, h6, h7, h8, x + 0.5, y + 0.5, ref r, ref g, ref b);
                }
                dData[o] = (byte)(r + 0.5);
                dData[o + 1] = (byte)(g + 0.5);
                dData[o + 2] = (byte)(b + 0.5);
            }
        });
        return dst;
    }

    /// <summary>Adds the bilinear sample of the source at the position (px, py) of the output.</summary>
    private static void Sample(byte[] s, int sw, int sh,
        double h0, double h1, double h2, double h3, double h4, double h5, double h6, double h7, double h8,
        double px, double py, ref double r, ref double g, ref double b)
    {
        double w = h6 * px + h7 * py + h8;
        // Pixel i is centred at i + 0.5, so shift by half a pixel before interpolating.
        double sx = (h0 * px + h1 * py + h2) / w - 0.5;
        double sy = (h3 * px + h4 * py + h5) / w - 0.5;

        // Beyond the outermost pixel centres by half a pixel = not in the photo: paper white.
        if (sx < -0.5 || sy < -0.5 || sx > sw - 0.5 || sy > sh - 0.5)
        {
            r += 255; g += 255; b += 255;
            return;
        }

        int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
        double fx = sx - x0, fy = sy - y0;
        int x1 = Math.Clamp(x0 + 1, 0, sw - 1), y1 = Math.Clamp(y0 + 1, 0, sh - 1);
        x0 = Math.Clamp(x0, 0, sw - 1);
        y0 = Math.Clamp(y0, 0, sh - 1);

        int i00 = (y0 * sw + x0) * 3, i10 = (y0 * sw + x1) * 3, i01 = (y1 * sw + x0) * 3, i11 = (y1 * sw + x1) * 3;
        double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
        r += s[i00] * w00 + s[i10] * w10 + s[i01] * w01 + s[i11] * w11;
        g += s[i00 + 1] * w00 + s[i10 + 1] * w10 + s[i01 + 1] * w01 + s[i11 + 1] * w11;
        b += s[i00 + 2] * w00 + s[i10 + 2] * w10 + s[i01 + 2] * w01 + s[i11 + 2] * w11;
    }

    private static double Dist(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>ISO 216 (A4, A3...): long side / short side = sqrt(2).</summary>
    public const double A4Ratio = 1.41421356237;

    /// <summary>
    /// Size of an A4 sheet (ratio 1 : sqrt 2) for straightening a quad: landscape when the outline is wider
    /// than tall, otherwise portrait. The long side keeps the finer resolution of the outline (never less
    /// than its longer side, nor than its shorter side times sqrt 2), then is shrunk to fit
    /// <paramref name="maxLongEdge"/> and <paramref name="maxPixels"/> (3508 x 2480 = A4 at 300 DPI).
    /// </summary>
    public static (int Width, int Height) A4Size(Quad q, int maxLongEdge, long maxPixels)
    {
        double w = Math.Max(Dist(q.TopLeft, q.TopRight), Dist(q.BottomLeft, q.BottomRight));
        double h = Math.Max(Dist(q.TopLeft, q.BottomLeft), Dist(q.TopRight, q.BottomRight));
        bool landscape = w > h;
        double longSide = landscape ? Math.Max(w, h * A4Ratio) : Math.Max(h, w * A4Ratio);
        longSide = Math.Min(longSide, Math.Min(maxLongEdge, Math.Sqrt(maxPixels * A4Ratio)));
        int l = Math.Max(23, (int)Math.Round(longSide));
        int s = Math.Max(16, (int)Math.Round(l / A4Ratio));
        return landscape ? (l, s) : (s, l);
    }

    /// <summary>
    /// Size of the rectangle a quad should be straightened into: as wide as its longer horizontal
    /// side and as tall as its longer vertical side (so no direction loses resolution), then shrunk
    /// (keeping the shape) until it fits <paramref name="maxLongEdge"/> and <paramref name="maxPixels"/>.
    /// </summary>
    public static (int Width, int Height) OutputSize(Quad q, int maxLongEdge, long maxPixels)
    {
        double w = Math.Max(Dist(q.TopLeft, q.TopRight), Dist(q.BottomLeft, q.BottomRight));
        double h = Math.Max(Dist(q.TopLeft, q.BottomLeft), Dist(q.TopRight, q.BottomRight));
        double scale = Math.Min(1.0, Math.Min(maxLongEdge / Math.Max(w, h), Math.Sqrt(maxPixels / (w * h))));
        return (Math.Max(16, (int)Math.Round(w * scale)), Math.Max(16, (int)Math.Round(h * scale)));
    }
}
