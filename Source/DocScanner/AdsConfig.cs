namespace DocScanner;

/// <summary>
/// AdMob ad unit IDs the app uses. <b>Placeholders below are inert</b> (AdMob refuses to serve real
/// ads on them, but -- unlike the App ID -- an ad unit ID that doesn't exist yet just fails a single ad
/// load quietly; it does not crash the app) and must be replaced with the real ones from your own AdMob
/// console (Apps &gt; Doc Scanner) before a Google Play release build -- see MOBILE-STATUS.md, "Quảng
/// cáo (AdMob)". Until then <see cref="MauiProgram.CreateMauiApp"/> forces Google's official test ads
/// (<c>AdConfig.UseTestAdUnitIds = true</c>) whenever <see cref="HasRealIds"/> is false -- in EVERY
/// build configuration, Debug or Release, so a Release build made before AdMob is set up still shows
/// (test) ads instead of silently none, and a real AdMob account is never at risk of an accidental real
/// ad request from a dev machine. The AdMob <b>App ID</b> (a different, separate ID) is NOT here: it
/// lives in <c>DocScanner.csproj</c>'s <c>AndroidManifestPlaceholders</c> (a malformed App ID crashes
/// the app at startup before any C# runs, so it is set to Google's own valid test App ID there, not to
/// a placeholder like these two).
/// </summary>
public static class AdsConfig
{
    public const string BannerAdUnitId = "ca-app-pub-REPLACE_ME/REPLACE_ME";
    public const string InterstitialAdUnitId = "ca-app-pub-REPLACE_ME/REPLACE_ME";

    /// <summary>False until both ad unit IDs above have been replaced with real ones.</summary>
    public static bool HasRealIds => !BannerAdUnitId.Contains("REPLACE_ME") && !InterstitialAdUnitId.Contains("REPLACE_ME");
}
