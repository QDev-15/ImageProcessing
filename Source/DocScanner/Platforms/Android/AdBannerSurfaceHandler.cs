using Android.Gms.Ads;
using Android.Views;
using DocScanner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Handlers;

namespace DocScanner.Views;

/// <summary>Maps <see cref="AdBannerSurface"/> to ONE shared native <c>AdView</c> for the whole app -- see
/// <see cref="AdBannerSurface"/>'s doc comment for why a shared instance through a real Handler, rather than a
/// fresh AdView per page or a hand-wired Activity-level one, is what actually fixes both the performance problem
/// and the banner not showing at all.</summary>
internal sealed class AdBannerSurfaceHandler : ViewHandler<AdBannerSurface, AdView>
{
	public static readonly IPropertyMapper<AdBannerSurface, AdBannerSurfaceHandler> Mapper =
		new PropertyMapper<AdBannerSurface, AdBannerSurfaceHandler>(ViewHandler.ViewMapper);

	private static AdView? _shared;
	private static IAdsService? _ads;

	/// <summary>True once a creative has actually come back for the CURRENT ad (reset on every new <c>LoadAd</c>,
	/// including AdMob's own periodic banner refresh). Without this, the banner reserved its 50dp strip and sat
	/// there plain black/blank whenever an ad was still loading, had no fill, or a refresh failed -- all states
	/// a real ad network hits often, not just a broken one (owner report, 2026-10-05). Hidden by default, shown
	/// only while there is an actual creative to show, keeps the UI clean with no visible cost.</summary>
	private static bool _loaded;

	public AdBannerSurfaceHandler() : base(Mapper)
	{
	}

	protected override AdView CreatePlatformView()
	{
		if (_shared is { } existing)
		{
			// Already shown on another (still-alive, e.g. back-stack) page: an Android View can only have one
			// parent, so take it back before this page's own container tries to add it.
			(existing.Parent as ViewGroup)?.RemoveView(existing);
			return existing;
		}

		_ads = IPlatformApplication.Current?.Services.GetService<IAdsService>();
		string adUnitId = Plugin.AdMob.Configuration.AdConfig.UseTestAdUnitIds
			? "ca-app-pub-3940256099942544/6300978111" // Google's published test banner id (developers.google.com/admob/android/test-ads)
			: AdsConfig.BannerAdUnitId;
		_shared = new AdView(Context) { AdUnitId = adUnitId, AdSize = AdSize.Banner, AdListener = new LoadListener() };
		UpdateVisibility();
		if (_ads != null) _ads.Changed += UpdateVisibility;
		if (_ads?.ShowAds != false) _shared.LoadAd(new AdRequest.Builder().Build()); // already Pro at startup: skip the request entirely
		return _shared;
	}

	/// <summary>Never tears down the shared AdView just because the page hosting it right now is going away --
	/// the next page's handler will simply reparent it (<see cref="CreatePlatformView"/>).</summary>
	protected override void DisconnectHandler(AdView platformView)
	{
	}

	private static void UpdateVisibility()
	{
		if (_shared == null) return;
		_shared.Visibility = _ads?.ShowAds != false && _loaded ? ViewStates.Visible : ViewStates.Gone;
	}

	private sealed class LoadListener : AdListener
	{
		public override void OnAdLoaded()
		{
			_loaded = true;
			UpdateVisibility();
		}

		public override void OnAdFailedToLoad(LoadAdError error)
		{
			_loaded = false; // no creative to show (first load, or a periodic refresh that came back empty)
			UpdateVisibility();
		}
	}
}
