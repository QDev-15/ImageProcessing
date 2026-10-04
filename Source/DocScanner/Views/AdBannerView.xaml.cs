using DocScanner.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocScanner.Views;

/// <summary>
/// Drop this at the bottom of any page to show the free-tier banner there. Resolves
/// <see cref="IAdsService"/> itself from the app's DI container (works regardless of whether XAML or code
/// creates the view), so a page's own ViewModel never needs an ads-specific property just to host it.
/// </summary>
public partial class AdBannerView : ContentView
{
	private readonly IAdsService? _ads;

	public AdBannerView()
	{
		InitializeComponent();
		_ads = IPlatformApplication.Current?.Services.GetService<IAdsService>();
		if (_ads == null) return;

		Banner.IsVisible = _ads.ShowAds;
		_ads.Changed += OnAdsChanged;
		Unloaded += (_, _) => _ads.Changed -= OnAdsChanged;
	}

	private void OnAdsChanged() => MainThread.BeginInvokeOnMainThread(() => Banner.IsVisible = _ads!.ShowAds);
}
