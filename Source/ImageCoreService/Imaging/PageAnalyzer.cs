using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>What a page really contains -- decides the export codec per page.</summary>
public enum PageColorKind { Bitonal, Gray, Color }

/// <summary>Blank-page and color/gray/bitonal classification.</summary>
public static class PageAnalyzer
{
    public const double DefaultBlankInkPercent = 0.03;

    /// <summary>
    /// True when the page has (almost) no ink. Looks at the central area only (5% margins
    /// ignored: scanner edges, punch holes, shadows), marks pixels clearly darker than the
    /// paper as ink, drops isolated specks, then compares the ink ratio against
    /// <paramref name="maxInkPercent"/> (default 0.03% of the area: a single short handwritten or
    /// typed line on A4 is above that, scanner dust is not).
    /// </summary>
    public static bool IsBlank(GrayImage gray, int dpi, double maxInkPercent = DefaultBlankInkPercent) =>
        InkPercent(gray, dpi) < maxInkPercent;

    /// <summary>Share of the central area covered by real ink (specks removed), in percent.</summary>
    public static double InkPercent(GrayImage gray, int dpi)
    {
        int f = DocumentCleanup.AnalysisFactor(dpi);
        GrayImage s = gray.Downscale(f);
        int mx = s.Width / 20, my = s.Height / 20;
        if (s.Width - 2 * mx < 8 || s.Height - 2 * my < 8) return 100;
        GrayImage c = s.Crop(Rectangle.FromLTRB(mx, my, s.Width - mx, s.Height - my));

        int paper = DocumentCleanup.Percentile(c, 0.90);
        int inkLevel = Math.Min(paper - 50, (int)(paper * 0.7));
        var bin = new GrayImage(c.Width, c.Height);
        for (int i = 0; i < c.Data.Length; i++) bin.Data[i] = c.Data[i] <= inkLevel ? (byte)0 : (byte)255;
        DocumentCleanup.Despeckle(bin, 2);

        int ink = 0;
        foreach (byte v in bin.Data) if (v == 0) ink++;
        return ink * 100.0 / bin.Data.Length;
    }

    /// <summary>
    /// Color: a meaningful share of clearly saturated pixels (analysis is done on a
    /// downscaled copy, which also averages away color fringing on black text edges).
    /// Gray: little color, but a large area of mid-tones (photos, shading, stamps in
    /// gray). Bitonal: everything else (text / line art on paper).
    /// </summary>
    public static unsafe PageColorKind Classify(Bitmap bmp, int dpi,
        double colorPercent = 0.5, double grayMidtonePercent = 8)
    {
        if (bmp.PixelFormat == PixelFormat.Format1bppIndexed) return PageColorKind.Bitonal;

        int f = DocumentCleanup.AnalysisFactor(dpi);
        int w = bmp.Width / f, h = bmp.Height / f;
        if (w < 1 || h < 1) return PageColorKind.Color;

        long colored = 0, midtone = 0, total = 0;
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int mx = w / 50, my = h / 50; // skip the outermost 2% (scanner edge artifacts)
            for (int y = my; y < h - my; y++)
            {
                for (int x = mx; x < w - mx; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < f; dy++)
                    {
                        byte* row = (byte*)bd.Scan0 + (long)(y * f + dy) * bd.Stride + x * f * 3;
                        for (int dx = 0; dx < f; dx++) { b += row[dx * 3]; g += row[dx * 3 + 1]; r += row[dx * 3 + 2]; }
                    }
                    int n = f * f;
                    r /= n; g /= n; b /= n;
                    int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    int lum = (r * 299 + g * 587 + b * 114) / 1000;
                    total++;
                    if (max - min > 60 && max > 60) colored++;
                    else if (lum > 60 && lum < 180) midtone++;
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bd);
        }

        if (total == 0) return PageColorKind.Color;
        if (colored * 100.0 / total >= colorPercent) return PageColorKind.Color;
        if (midtone * 100.0 / total >= grayMidtonePercent) return PageColorKind.Gray;
        return PageColorKind.Bitonal;
    }
}
