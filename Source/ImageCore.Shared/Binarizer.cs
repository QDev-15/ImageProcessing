namespace ImageCoreService;

[System.ComponentModel.TypeConverter(typeof(EnumDescriptionConverter))]
public enum BinarizationMethod
{
    /// <summary>Local adaptive threshold (Sauvola &amp; Pietikäinen, 2000). Default:
    /// handles yellowed paper, uneven scanner lighting, shadows and faint ink.</summary>
    [System.ComponentModel.Description("Sauvola (thích nghi)")] Sauvola,
    /// <summary>Single global threshold (Otsu, 1979). Faster; fine for clean, evenly lit pages.</summary>
    [System.ComponentModel.Description("Otsu (toàn trang)")] Otsu,
}

/// <summary>
/// Gray -> binary conversion. Both algorithms are classic published methods implemented
/// from scratch here (no third-party code, no patents) so they are free to ship in a
/// commercial product.
///
/// Why Sauvola is the default: it computes a threshold per pixel from the local mean m
/// and standard deviation s over a window, T = m * (1 + k * (s / R - 1)). On blank paper s
/// is tiny, so T drops well below the paper level and scanner noise stays white; near
/// strokes s is large, so T approaches m and thin / faint strokes survive. A single global
/// threshold (Otsu, or the old fixed 128) cannot do both on a page with a gradient or a
/// yellowed background. Local mean/variance come from integral images, so the cost is
/// O(pixels) regardless of window size.
/// </summary>
public static class Binarizer
{
    public static GrayImage Binarize(GrayImage src, BinarizationMethod method, int dpi,
        double sauvolaK = DefaultSauvolaK, int windowSize = 0)
    {
        return method == BinarizationMethod.Otsu
            ? Threshold(src, OtsuThreshold(src))
            : Sauvola(src, windowSize > 0 ? windowSize : DefaultWindow(dpi), sauvolaK);
    }

    public const double DefaultSauvolaK = 0.34;

    /// <summary>~1/8 inch window: a few text strokes wide at any DPI.</summary>
    public static int DefaultWindow(int dpi) => Math.Clamp((int)Math.Round(dpi / 8.0) | 1, 15, 151);

    public static int OtsuThreshold(GrayImage src)
    {
        var hist = new long[256];
        foreach (byte v in src.Data) hist[v]++;
        return OtsuThreshold(hist);
    }

    public static int OtsuThreshold(long[] hist)
    {
        long total = 0;
        double sumAll = 0;
        for (int i = 0; i < 256; i++) { total += hist[i]; sumAll += i * (double)hist[i]; }
        if (total == 0) return 128;

        double sumB = 0, bestVar = -1;
        long wB = 0;
        int best = 128;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            long wF = total - wB;
            if (wF == 0) break;
            sumB += t * (double)hist[t];
            double mB = sumB / wB, mF = (sumAll - sumB) / wF;
            double between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > bestVar) { bestVar = between; best = t; }
        }
        return best;
    }

    /// <summary>Pixels &lt;= threshold become ink (0), the rest paper (255).</summary>
    public static GrayImage Threshold(GrayImage src, int threshold)
    {
        var dst = new GrayImage(src.Width, src.Height);
        for (int i = 0; i < src.Data.Length; i++)
            dst.Data[i] = src.Data[i] <= threshold ? (byte)0 : (byte)255;
        return dst;
    }

    public static GrayImage Sauvola(GrayImage src, int window, double k)
    {
        int w = src.Width, h = src.Height;
        int stride = w + 1;
        // Integral images of v and v^2 (one extra row/column of zeros). long is plenty:
        // 255^2 * 100M pixels still fits comfortably.
        var sum = new long[(long)stride * (h + 1)];
        var sq = new long[(long)stride * (h + 1)];
        for (int y = 0; y < h; y++)
        {
            long rowSum = 0, rowSq = 0;
            int o = y * w;
            long io = (long)(y + 1) * stride, ip = (long)y * stride;
            for (int x = 0; x < w; x++)
            {
                int v = src.Data[o + x];
                rowSum += v;
                rowSq += v * v;
                sum[io + x + 1] = sum[ip + x + 1] + rowSum;
                sq[io + x + 1] = sq[ip + x + 1] + rowSq;
            }
        }

        const double R = 128.0; // dynamic range of the standard deviation for 8-bit input
        int half = window / 2;
        var dst = new GrayImage(w, h);
        Parallel.For(0, h, y =>
        {
            int y0 = Math.Max(0, y - half), y1 = Math.Min(h - 1, y + half);
            long r0 = (long)y0 * stride, r1 = (long)(y1 + 1) * stride;
            int o = y * w;
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - half), x1 = Math.Min(w - 1, x + half);
                long n = (long)(x1 - x0 + 1) * (y1 - y0 + 1);
                long s = sum[r1 + x1 + 1] - sum[r0 + x1 + 1] - sum[r1 + x0] + sum[r0 + x0];
                long s2 = sq[r1 + x1 + 1] - sq[r0 + x1 + 1] - sq[r1 + x0] + sq[r0 + x0];
                double mean = s / (double)n;
                double variance = Math.Max(0, s2 / (double)n - mean * mean);
                double t = mean * (1 + k * (Math.Sqrt(variance) / R - 1));
                dst.Data[o + x] = src.Data[o + x] <= t ? (byte)0 : (byte)255;
            }
        });
        return dst;
    }
}
