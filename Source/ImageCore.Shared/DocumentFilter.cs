using System.ComponentModel;

namespace ImageCoreService;

/// <summary>How a scanned page is kept.</summary>
public enum PageColorMode
{
    /// <summary>The photo's colors, straightened only.</summary>
    [Description("Màu")] Color,
    /// <summary>Grayscale with the background flattened (shadows / yellow paper removed).</summary>
    [Description("Xám")] Gray,
    /// <summary>Pure black and white (adaptive threshold): smallest files, crisp text.</summary>
    [Description("Đen trắng")] BlackWhite,
}

/// <param name="Darkness">0..100, 50 = default. Higher keeps fainter strokes (and more noise) in black and white.</param>
/// <param name="CleanBackground">Flatten shadows / yellowed paper to even white (gray and black-and-white modes).</param>
public sealed record FilterOptions(
    PageColorMode Mode,
    int Darkness = FilterOptions.DefaultDarkness,
    bool CleanBackground = true,
    bool Despeckle = true,
    BinarizationMethod Method = BinarizationMethod.Sauvola)
{
    public const int DefaultDarkness = 50;
}

/// <summary>A filtered page: either color (<see cref="Color"/>) or gray / black-and-white (<see cref="Gray"/>).</summary>
public sealed class FilteredPage
{
    public RgbImage? Color { get; init; }
    public GrayImage? Gray { get; init; }
    /// <summary>True when <see cref="Gray"/> holds only 0 and 255.</summary>
    public bool IsBilevel { get; init; }
    public int Width => Color?.Width ?? Gray!.Width;
    public int Height => Color?.Height ?? Gray!.Height;
}

/// <summary>
/// Turns a straightened page photo into its final look. Runs on the in-memory warp result, so the
/// page is compressed exactly once, when it is saved.
/// </summary>
public static class DocumentFilter
{
    /// <summary>Sauvola k for a darkness setting: 50 -> 0.34 (the tuned default), 0 -> 0.55 (thin, clean),
    /// 100 -> 0.13 (bold, keeps faint pencil).</summary>
    public static double SauvolaKFor(int darkness) => 0.55 - 0.0042 * Math.Clamp(darkness, 0, 100);

    /// <summary>Otsu threshold shift for a darkness setting (+/- 40 levels).</summary>
    public static int OtsuOffsetFor(int darkness) => (int)Math.Round((Math.Clamp(darkness, 0, 100) - 50) * 0.8);

    /// <param name="dpi">Resolution of the page (sets the Sauvola window and the speck size).</param>
    public static FilteredPage Apply(RgbImage page, FilterOptions o, int dpi)
    {
        switch (o.Mode)
        {
            case PageColorMode.Color:
                return new FilteredPage { Color = page }; // the photo's own colors (cleaning measured no size gain on real pages)

            case PageColorMode.Gray:
            {
                GrayImage gray = page.ToGray();
                return new FilteredPage { Gray = o.CleanBackground ? BackgroundFlattener.Flatten(gray) : gray };
            }

            default:
            {
                GrayImage gray = page.ToGray();
                if (o.CleanBackground) gray = BackgroundFlattener.Flatten(gray);
                GrayImage bin = o.Method == BinarizationMethod.Otsu
                    ? Binarizer.Threshold(gray, Math.Clamp(Binarizer.OtsuThreshold(gray) + OtsuOffsetFor(o.Darkness), 1, 254))
                    : Binarizer.Sauvola(gray, Binarizer.DefaultWindow(dpi), SauvolaKFor(o.Darkness));
                if (o.Despeckle) DocumentCleanup.Despeckle(bin, DocumentCleanup.DefaultSpeckleArea(dpi));
                return new FilteredPage { Gray = bin, IsBilevel = true };
            }
        }
    }
}
