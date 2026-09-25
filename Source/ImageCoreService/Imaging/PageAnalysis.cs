using System.Drawing;

namespace ImageCoreService;

/// <summary>What automatic page analysis decided: the page is blank, or these ops improve it.</summary>
public sealed record AnalysisResult(bool IsBlank, PageOps Ops, string Summary);

/// <summary>
/// Blank detection, black-border crop, deskew and upright orientation -- decided on the page's
/// ~1600 px proxy and recorded as <see cref="PageOps"/> (crop as a fraction of the page, skew in
/// degrees, a quarter-turn), so no full-resolution pixel is ever rewritten at import. The same
/// ops are applied to the full-size render only when the page is exported (or shown zoomed in).
/// Steps run in the order the ops are applied: crop, then deskew of the cropped page, then
/// orientation of the straightened one.
/// </summary>
public static class PageAnalysis
{
    public static AnalysisResult Analyze(PageCache cache, PageSource source, PageProcessingOptions o, OcrEngine? osd)
    {
        using Bitmap proxy = cache.GetProxy(source);
        (int dpi, _) = ImageUtils.ResolveDpiXY(proxy);
        GrayImage gray = GdiGray.FromBitmap(proxy);

        if (o.DetectBlank && Perf.Measure("proc.blank", () => PageAnalyzer.IsBlank(gray, dpi, o.BlankInkPercent)))
            return new AnalysisResult(true, PageOps.None, "trang trắng");

        var notes = new List<string>();
        RectangleF? crop = null;
        double skew = 0;
        int rotate = 0;
        Bitmap current = proxy;
        try
        {
            if (o.CropBorders)
            {
                Rectangle r = Perf.Measure("proc.cropdetect", () => DocumentCleanup.DetectContentBounds(gray, dpi));
                if (r.Width < proxy.Width || r.Height < proxy.Height)
                {
                    crop = new RectangleF((float)r.X / proxy.Width, (float)r.Y / proxy.Height,
                        (float)r.Width / proxy.Width, (float)r.Height / proxy.Height);
                    current = BitmapTransforms.Crop(current, r);
                    gray = GdiGray.FromBitmap(current);
                    notes.Add("cắt viền");
                }
            }

            if (o.Deskew)
            {
                GrayImage g = gray;
                double angle = Perf.Measure("proc.skewdetect", () => DocumentCleanup.DetectSkew(g, dpi));
                if (angle != 0)
                {
                    skew = angle;
                    Bitmap straight = BitmapTransforms.RotateArbitrary(current, -angle);
                    if (!ReferenceEquals(current, proxy)) current.Dispose();
                    current = straight;
                    notes.Add($"chỉnh nghiêng {angle:0.0}°");
                }
            }

            if (o.AutoOrient && osd != null)
            {
                Bitmap seen = current;
                int turn = Perf.Measure("proc.osd", () => osd.DetectUprightRotation(seen));
                if (turn != 0)
                {
                    rotate = turn;
                    notes.Add($"xoay {turn}°");
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(current, proxy)) current.Dispose();
        }
        return new AnalysisResult(false, new PageOps(rotate, skew, crop), string.Join(", ", notes));
    }
}
