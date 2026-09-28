using DocScanner.ViewModels;
#if ANDROID
using DocScanner.Services;
#endif

namespace DocScanner.Views;

/// <summary>The PDF viewer: each page is rendered by the platform (PdfRenderer) at a size worth zooming into.</summary>
public partial class PdfViewerPage : ContentPage
{
	private readonly PdfViewerViewModel _viewModel;

	public PdfViewerPage(PdfViewerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if ANDROID
		_host = new ZoomImageHost(Picture) { Swipe = delta => _viewModel.GoCommand.Execute(delta) };
		_viewModel.FileChanged += OnFileChanged;
		_viewModel.PageRequested += OnPageRequested;
		Picture.HandlerChanged += (_, _) => { if (_pendingPage >= 0) OnPageRequested(_pendingPage); };
#endif
	}

#if ANDROID
	/// <summary>Long edge a page is rendered at: ~250 DPI for A4, sharp at 3-4x zoom on a phone screen.</summary>
	private const int PageEdge = 2900;

	private readonly ZoomImageHost _host;
	private PdfPages? _pages;
	private int _pendingPage = -1;

	private void OnFileChanged(string path)
	{
		_pages?.Dispose();
		_pages = null;
		int count = 0;
		try
		{
			_pages = new PdfPages(path);
			count = _pages.Count;
		}
		catch (Exception ex)
		{
			_ = DisplayAlertAsync("Không mở được PDF", ex.Message, "OK");
		}
		_viewModel.SetPageCount(count);
	}

	private void OnPageRequested(int index)
	{
		if (Picture.Handler == null)
		{
			_pendingPage = index;
			return;
		}
		_pendingPage = -1;
		PdfPages? pages = _pages;
		_host.ShowBitmap(pages == null ? null : () => pages.Render(index, PageEdge));
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		// Leaving for good (back to the list), not just covered by another page: close the file.
		if (Navigation.NavigationStack.Contains(this)) return;
		_host.Clear();
		_pages?.Dispose();
		_pages = null;
	}
#endif
}
