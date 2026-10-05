using DocScanner.ViewModels;

namespace DocScanner.Views;

/// <summary>Folder rows and document rows of the main screen.</summary>
public sealed class HomeRowSelector : DataTemplateSelector
{
	public DataTemplate? Folder { get; set; }
	public DataTemplate? Document { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
		(item is FolderItem ? Folder : Document)!;
}

/// <summary>The main screen, at the top level or inside a folder (route "folder").</summary>
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
		_viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(HomeViewModel.IsSearching) && _viewModel.IsSearching) Search.Focus();
		};
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

	/// <summary>Back leaves selection mode / search before leaving the screen.</summary>
	protected override bool OnBackButtonPressed() => _viewModel.HandleBack() || base.OnBackButtonPressed();
}
