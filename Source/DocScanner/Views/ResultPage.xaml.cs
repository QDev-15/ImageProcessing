using DocScanner.ViewModels;

namespace DocScanner.Views;

public partial class ResultPage : ContentPage
{
	private readonly ResultViewModel _viewModel;

	public ResultPage(ResultViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.Attach();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
	}
}
