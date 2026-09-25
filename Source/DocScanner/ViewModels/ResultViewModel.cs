using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;
using ImageCoreService;

namespace DocScanner.ViewModels;

/// <summary>The straightened page. Opening it starts the render when the outline changed since the
/// last one (the queue does the work; this screen waits and updates itself).</summary>
public partial class ResultViewModel(DocumentStore store, PageIngestQueue queue, PageEditService edit, ExportCoordinator exports) : ObservableObject, IQueryAttributable
{
	private string? _docId;
	private string? _pageId;
	private bool _requested;

	[ObservableProperty]
	private string title = "Kết quả";

	[ObservableProperty]
	private ImageSource? image;

	[ObservableProperty]
	private bool isBusy;

	[ObservableProperty]
	private string status = "";

	[ObservableProperty]
	private bool hasError;

	/// <summary>Current look of the page (drives the three mode buttons).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsColor), nameof(IsGray), nameof(IsBlackWhite), nameof(ShowCleanBackground))]
	private PageColorMode mode;

	/// <summary>0..100, black-and-white only.</summary>
	[ObservableProperty]
	private double darkness = FilterOptions.DefaultDarkness;

	[ObservableProperty]
	private bool cleanBackground = true;

	[ObservableProperty]
	private bool canGoPrevious;

	[ObservableProperty]
	private bool canGoNext;

	public bool IsColor => Mode == PageColorMode.Color;
	public bool IsGray => Mode == PageColorMode.Gray;
	public bool IsBlackWhite => Mode == PageColorMode.BlackWhite;
	public bool ShowCleanBackground => Mode != PageColorMode.Color;

	/// <summary>True while the controls are being filled from the page, so their change handlers do not write back.</summary>
	private bool _loading;

	/// <summary>Text of the shape button: what the page is now (A4 by default).</summary>
	[ObservableProperty]
	private string aspectText = "Khổ giấy: A4";

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("docId", out object? d) && d is string docId
			&& query.TryGetValue("pageId", out object? p) && p is string pageId)
		{
			_docId = docId;
			_pageId = pageId;
			_requested = false;
			Refresh();
		}
	}

	public void Attach()
	{
		queue.PageUpdated += OnPageUpdated;
		Refresh();
	}

	public void Detach() => queue.PageUpdated -= OnPageUpdated;

	private void OnPageUpdated(PageUpdate update)
	{
		if (update.PageId == _pageId) MainThread.BeginInvokeOnMainThread(Refresh);
	}

	private void Refresh()
	{
		if (_docId == null || _pageId == null) return;
		IReadOnlyList<PageRecord> pages = store.Pages(_docId);
		PageRecord? page = pages.FirstOrDefault(x => x.Id == _pageId);
		if (page == null)
		{
			Status = "Không tìm thấy trang.";
			IsBusy = false;
			return;
		}
		int index = pages.ToList().IndexOf(page);
		Title = $"Trang {index + 1}/{pages.Count} · kết quả";
		CanGoPrevious = index > 0;
		CanGoNext = index < pages.Count - 1;
		AspectText = page.FreeAspect ? "Khổ giấy: theo khung" : "Khổ giấy: A4";
		_loading = true;
		Mode = page.ColorMode;
		Darkness = page.BwDarkness;
		CleanBackground = page.CleanBackground;
		_loading = false;

		if (page.NeedsRender)
		{
			if (page.RenderError != null)
			{
				HasError = true;
				IsBusy = false;
				Status = "Không cắt được: " + page.RenderError;
				return;
			}
			HasError = false;
			IsBusy = true;
			Status = page.CroppedRevision > 0 ? "Đang dựng lại trang..." : "Đang cắt trang...";
			// Once per visit: a page still not current afterwards is either failing (shown above) or was edited again.
			if (!_requested && !queue.IsBusy(page.Id))
			{
				_requested = true;
				queue.EnqueueRender(_docId, page.Id);
			}
			return;
		}

		HasError = false;
		IsBusy = false;
		Image = ImageSource.FromFile(store.CroppedPath(_docId, page));
		Status = $"{page.CroppedWidth} × {page.CroppedHeight} px";
	}

	/// <summary>A4 (standard sheet) or the outline's own proportions (receipts, cards, other paper).</summary>
	[RelayCommand]
	private void ToggleAspect()
	{
		if (_docId == null || _pageId == null) return;
		PageRecord? page = store.Pages(_docId).FirstOrDefault(x => x.Id == _pageId);
		if (page == null || page.State != PageState.Ready) return;
		edit.SetFreeAspect(_docId, _pageId, !page.FreeAspect);
		_requested = false; // the page is stale now: Refresh asks the queue for a new render
		Refresh();
	}

	[RelayCommand]
	private void Retry()
	{
		if (_docId == null || _pageId == null) return;
		store.Update(_docId, d =>
		{
			PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == _pageId);
			if (p != null) p.RenderError = null;
		});
		_requested = false;
		Refresh();
	}

	/// <summary>Color / gray / black and white. The parameter is the enum name (from the XAML buttons).</summary>
	[RelayCommand]
	private void SetMode(string name)
	{
		if (!Enum.TryParse(name, out PageColorMode m) || m == Mode) return;
		Mode = m;
		ChangeFilter(mode: m);
	}

	/// <summary>Called when the darkness slider is released (not on every step: each change re-renders the page).</summary>
	[RelayCommand]
	private void CommitDarkness() => ChangeFilter(darkness: (int)Math.Round(Darkness));

	partial void OnCleanBackgroundChanged(bool value) => ChangeFilter(clean: value);

	private void ChangeFilter(PageColorMode? mode = null, int? darkness = null, bool? clean = null)
	{
		if (_loading || _docId == null || _pageId == null) return;
		if (!edit.SetFilter(_docId, _pageId, mode, darkness, clean)) return;
		_requested = false; // the page may be stale now: Refresh asks the queue for a new render
		Refresh();
	}

	/// <summary>Gives every page of the document this page's look; the other pages re-render in the background.</summary>
	[RelayCommand]
	private async Task ApplyToAllAsync()
	{
		if (_docId == null) return;
		var look = new FilterOptions(Mode, (int)Math.Round(Darkness), CleanBackground);
		IReadOnlyList<string> changed = edit.ApplyFilterToAll(_docId, look);
		foreach (string id in changed)
			if (id != _pageId) queue.EnqueueRender(_docId, id);
		_requested = false;
		Refresh();
		string text = changed.Count == 0 ? "Mọi trang đã dùng kiểu này." : $"Đã áp dụng cho {changed.Count} trang; các trang đang được dựng lại.";
		await Shell.Current.DisplayAlertAsync("Áp dụng cho mọi trang", text, "OK");
	}

	/// <summary>Back to the outline editor, on the page shown here (which may differ from the one opened).</summary>
	[RelayCommand]
	private Task EditAgainAsync() => Shell.Current.GoToAsync($"..?docId={_docId}&pageId={_pageId}");

	/// <summary>Previous (-1) / next (+1) page: buttons or a horizontal swipe on the picture.</summary>
	[RelayCommand]
	private void Go(int delta)
	{
		if (_docId == null || _pageId == null) return;
		var next = store.Neighbor(_docId, _pageId, delta);
		if (next == null) return;
		_pageId = next.Value.PageId;
		_requested = false;
		Image = null; // do not leave the previous page on screen while this one renders
		Refresh();
	}

	[RelayCommand]
	private void SwipeLeft() => Go(1);

	[RelayCommand]
	private void SwipeRight() => Go(-1);

	[RelayCommand]
	private Task ExportPdfAsync() => _docId == null ? Task.CompletedTask : exports.ExportAsync(_docId);
}
