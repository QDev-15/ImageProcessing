using DocScanner.ViewModels;

namespace DocScanner.Views;

public partial class SettingsPage : ContentPage
{
	private readonly SettingsViewModel _viewModel;

	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.LoadStorageAsync();
	}

	private void OnUrlUnfocused(object? sender, FocusEventArgs e) => _viewModel.SaveUrlCommand.Execute(null);
}
