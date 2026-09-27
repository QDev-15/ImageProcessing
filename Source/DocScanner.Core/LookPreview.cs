using ImageCoreService;

namespace DocScanner.Core;

/// <summary>A picture for the result screen: a color page, or a gray / black-and-white one.</summary>
public sealed record PreviewFrame(RgbImage? Color, GrayImage? Gray);

/// <summary>
/// The result screen's live preview as a pipeline whose stages are kept once computed, so each change redoes only
/// what it affects (the way photo editors keep their intermediate buffers):
///
///   straightened page (color) -> gray -> background flattened -> Sauvola statistics -> black and white
///
/// - color: the page itself, nothing to compute; brightness / contrast are a GPU color matrix in the view;
/// - gray: the gray (or flattened) stage, computed once;
/// - black and white: darkness (Sauvola k) and brightness only change the final threshold, one comparison per pixel,
///   because the window mean / deviation are kept (<see cref="Binarizer.Stats"/>): the sliders follow the finger;
/// - a quarter turn turns every stage already computed (a copy each; the statistics of a turned page are exactly the
///   turned statistics) instead of computing them again.
/// <see cref="Warm"/> computes the stages the other looks need in the background, so switching look is immediate too.
/// Same filters and parameters as the saved page (<see cref="DocumentFilter"/>), at screen size. Thread-safe.
/// </summary>
public sealed class LookPreview
{
    private readonly object _lock = new();
    private GrayImage? _gray, _flat;
    private SauvolaStats? _grayStats, _flatStats;

    public LookPreview(RgbImage straightened) : this(straightened, null, null, null, null) { }

    private LookPreview(RgbImage page, GrayImage? gray, GrayImage? flat, SauvolaStats? grayStats, SauvolaStats? flatStats)
    {
        Page = page;
        Dpi = CropRenderService.PageDpi(page.Width, page.Height);
        _gray = gray;
        _flat = flat;
        _grayStats = grayStats;
        _flatStats = flatStats;
    }

    /// <summary>The straightened, unfiltered page.</summary>
    public RgbImage Page { get; }

    /// <summary>Resolution the filters are sized for (window, speck size), as for the saved page.</summary>
    public int Dpi { get; }

    /// <summary>The page in <paramref name="look"/>, without brightness / contrast for color and gray (the view applies
    /// those live), with the brightness for black and white (it moves the threshold).</summary>
    public PreviewFrame Render(FilterOptions look)
    {
        switch (look.Mode)
        {
            case PageColorMode.Color:
                return new PreviewFrame(Page, null);
            case PageColorMode.Gray:
                return new PreviewFrame(null, Source(look.CleanBackground));
            default:
            {
                if (look.Method == BinarizationMethod.Otsu)
                    return new PreviewFrame(null, DocumentFilter.Apply(Page, look, Dpi).Gray!); // rare; not worth a cached path
                GrayImage source = Source(look.CleanBackground);
                SauvolaStats stats = Stats(look.CleanBackground);
                var bw = new GrayImage(source.Width, source.Height);
                Binarizer.Threshold(source, stats, DocumentFilter.SauvolaKFor(look.Darkness), look.Tone.BrightnessLevels, bw);
                if (look.Despeckle) DocumentCleanup.Despeckle(bw, DocumentCleanup.DefaultSpeckleArea(Dpi));
                return new PreviewFrame(null, bw);
            }
        }
    }

    /// <summary>Computes the stages the looks the user may pick next need (gray, flattened, statistics), so that
    /// switching is instant. Cheap to call again: what exists is kept.</summary>
    public void Warm(bool cleanBackground)
    {
        Source(cleanBackground);
        Stats(cleanBackground);
    }

    /// <summary>This preview turned by quarter turns: every stage computed so far is turned with it, nothing is recomputed.</summary>
    public LookPreview RotateClockwise(int turns)
    {
        if (((turns % 4) + 4) % 4 == 0) return this;
        lock (_lock)
        {
            return new LookPreview(Page.RotateClockwise(turns), _gray?.RotateClockwise(turns), _flat?.RotateClockwise(turns),
                _grayStats?.RotateClockwise(turns), _flatStats?.RotateClockwise(turns));
        }
    }

    private GrayImage Source(bool clean)
    {
        lock (_lock)
        {
            _gray ??= Page.ToGray();
            if (!clean) return _gray;
            return _flat ??= BackgroundFlattener.Flatten(_gray);
        }
    }

    private SauvolaStats Stats(bool clean)
    {
        GrayImage source = Source(clean);
        lock (_lock)
        {
            if (clean) return _flatStats ??= Binarizer.Stats(source, Binarizer.DefaultWindow(Dpi));
            return _grayStats ??= Binarizer.Stats(source, Binarizer.DefaultWindow(Dpi));
        }
    }
}
