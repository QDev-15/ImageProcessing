using DocScanner.ViewModels;

namespace DocScanner.Views;

public partial class HomePage : ContentPage
{
	private readonly HomeViewModel _viewModel;

	public HomePage(HomeViewModel viewModel)
	{
		DocScanner.Core.Perf.Log("startup: HomePage ctor");
		InitializeComponent();
		DocScanner.Core.Perf.Log("startup: HomePage XAML inflated");
		BindingContext = _viewModel = viewModel;
		Loaded += (_, _) => DocScanner.Core.Perf.Log("startup: HomePage loaded");
	}

	protected override async void OnAppearing()
	{
		DocScanner.Core.Perf.Log("startup: Home appearing");
		base.OnAppearing();
		_viewModel.Attach();
		await _viewModel.RefreshAsync();
		DocScanner.Core.Perf.Log("startup: Home list loaded");
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
	}
}
