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

    /// <summary>
    /// Sauvola threshold with a square <paramref name="window"/>, borders clipped. Memory is
    /// O(width): each horizontal band of rows keeps running per-column sums of v and v^2 and slides
    /// them down one row at a time (add the row entering the window, drop the one leaving it); the
    /// window sum along a row is another running sum over those columns. An 8.7 MP A4 page therefore
    /// needs a few KB of working memory instead of the ~140 MB two full integral images would take.
    /// The sums are exact integers, so the result is bit-identical to the integral-image version.
    /// </summary>
    public static GrayImage Sauvola(GrayImage src, int window, double k)
    {
        int w = src.Width, h = src.Height;
        int half = Math.Max(0, window / 2);
        var dst = new GrayImage(w, h);
        if (w == 0 || h == 0) return dst;

        // Bands run in parallel; each needs to prime its column sums over ~window rows, so keep
        // bands several windows tall.
        int bands = Math.Clamp(Environment.ProcessorCount, 1, Math.Max(1, h / Math.Max(64, 4 * half)));
        int bandHeight = (h + bands - 1) / bands;
        byte[] data = src.Data, output = dst.Data;

        Parallel.For(0, bands, b =>
        {
            int yStart = b * bandHeight, yEnd = Math.Min(h, yStart + bandHeight);
            if (yStart >= yEnd) return;

            var colSum = new long[w];
            var colSq = new long[w];
            void AddRow(int r, int sign)
            {
                int o = r * w;
                for (int x = 0; x < w; x++)
                {
                    int v = data[o + x];
                    colSum[x] += sign * v;
                    colSq[x] += sign * v * v;
                }
            }

            int top = Math.Max(0, yStart - half), bottom = Math.Min(h - 1, yStart + half);
            for (int r = top; r <= bottom; r++) AddRow(r, +1);

            const double R = 128.0; // dynamic range of the standard deviation for 8-bit input
            for (int y = yStart; y < yEnd; y++)
            {
                if (y > yStart)
                {
                    int newTop = Math.Max(0, y - half), newBottom = Math.Min(h - 1, y + half);
                    if (newBottom > bottom) AddRow(newBottom, +1);
                    if (newTop > top) AddRow(top, -1);
                    top = newTop;
                    bottom = newBottom;
                }
                int rows = bottom - top + 1;

                // Running window over the columns of this row.
                long s = 0, s2 = 0;
                int right = Math.Min(w - 1, half);
                for (int x = 0; x <= right; x++) { s += colSum[x]; s2 += colSq[x]; }
                int o = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (x > 0)
                    {
                        int enter = x + half, leave = x - half - 1;
                        if (enter < w) { s += colSum[enter]; s2 += colSq[enter]; }
                        if (leave >= 0) { s -= colSum[leave]; s2 -= colSq[leave]; }
                    }
                    int x0 = Math.Max(0, x - half), x1 = Math.Min(w - 1, x + half);
                    long n = (long)(x1 - x0 + 1) * rows;
                    double mean = s / (double)n;
                    double variance = Math.Max(0, s2 / (double)n - mean * mean);
                    double t = mean * (1 + k * (Math.Sqrt(variance) / R - 1));
                    output[o + x] = data[o + x] <= t ? (byte)0 : (byte)255;
                }
            }
        });
        return dst;
    }
}
