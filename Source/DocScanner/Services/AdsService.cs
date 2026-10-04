using DocScanner.Core;
using DocScanner.Core.Ads;
using DocScanner.Core.Licensing;
using Plugin.AdMob.Services;

namespace DocScanner.Services;

/// <summary>
/// Whether ads should be visible right now, and the one place in the app that talks to AdMob --
/// mirrors <see cref="LicenseService"/> being the one place that talks to Play Billing. The interstitial
/// rule itself (every 5th PDF export) lives in <see cref="AdsPolicy"/> and is unit-tested on its own;
/// this class only wires that decision to the actual ad SDK and to <see cref="ILicenseService"/>.
/// </summary>
public interface IAdsService
{
    /// <summary>False once Pro is bought (or restored) -- every page's <see cref="Views.AdBannerView"/> binds
    /// to this. An interstitial from <see cref="RegisterExport"/> is a separate full-screen ad (its own native
    /// activity), so it never visually competes with whatever banner the page underneath happens to show.</summary>
    bool ShowAds { get; }

    /// <summary>Raised whenever <see cref="ShowAds"/> may have changed (i.e. the license changed).</summary>
    event Action? Changed;

    /// <summary>Call once, right after a PDF export finishes -- success only; a cancelled or failed export
    /// must not advance the cycle, same rule as <see cref="ILicenseService.RecordExport"/>. Shows an
    /// interstitial when <see cref="AdsPolicy"/> says it is due and one happens to be ready; otherwise
    /// this cycle is skipped quietly (ads must never make the export flow wait or fail).</summary>
    void RegisterExport();
}

public sealed class AdsService : IAdsService
{
    private const string StateKey = "ads_exports_since_interstitial";

    private readonly ILicenseService _license;
    private readonly IInterstitialAdService _interstitial;

    public AdsService(ILicenseService license, IInterstitialAdService interstitial)
    {
        _license = license;
        _interstitial = interstitial;
        _license.Changed += () => Changed?.Invoke();
        PrepareInterstitial(); // one kept ready at all times, so the 5th export rarely has to skip
    }

    public bool ShowAds => !_license.State.IsPro;

    public event Action? Changed;

    public void RegisterExport()
    {
        var state = new AdsState(Preferences.Default.Get(StateKey, 0));
        (AdsState next, bool show) = AdsPolicy.AfterExport(state, _license.State.IsPro);
        Preferences.Default.Set(StateKey, next.ExportsSinceLastInterstitial);

        if (show && _interstitial.IsAdLoaded)
        {
            try { _interstitial.ShowAd(); }
            catch (Exception ex) { Perf.Log($"ads: interstitial show failed: {ex.Message}"); }
        }
        PrepareInterstitial(); // whether shown, skipped, or not due yet: keep one ready for next time
    }

    private void PrepareInterstitial()
    {
        try { _interstitial.PrepareAd(AdsConfig.InterstitialAdUnitId); }
        catch (Exception ex) { Perf.Log($"ads: interstitial preload failed: {ex.Message}"); }
    }
}
