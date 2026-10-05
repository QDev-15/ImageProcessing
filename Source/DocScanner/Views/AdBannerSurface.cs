namespace DocScanner.Views;

/// <summary>Cross-platform placeholder for the free-tier ad banner: a MAUI View with a custom Handler
/// (<c>Platforms/Android/AdBannerSurfaceHandler.cs</c>) that hands every page the SAME native AdView instead of
/// building a fresh one each time.
///
/// Drop this at the bottom of any page, sized with <c>HeightRequest="50"</c> (AdMob's standard banner height) --
/// same spot the old "AdBannerView" ContentView (removed 2026-10-05) used to sit in. That version, and a later
/// attempt that wrapped the whole Activity's native root view in a column with one AdView below it (also
/// 2026-10-05), both turned out wrong in opposite ways: the ContentView rebuilt (and re-requested) a brand new ad
/// on every single navigation, which was the actual "chuyển màn hình chậm" the owner reported; the Activity-level
/// version fixed that but then MAUI's own full-screen redraw silently covered the banner regardless of its real,
/// correctly-measured bounds (confirmed on-device: logs showed the right size and a loaded ad, nothing ever
/// visible). Going through a real Handler -- MAUI's own supported way to host a native view -- sidesteps both:
/// each page's Grid lays this out normally (no "MAUI draws over it" conflict, since it IS the thing MAUI is
/// drawing), while the Handler itself keeps returning the one shared AdView, detached from whatever page held it
/// last, so it is still created and loaded only once per app run.</summary>
public sealed class AdBannerSurface : View;
