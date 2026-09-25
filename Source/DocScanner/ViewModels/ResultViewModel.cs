using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;

namespace DocScanner.ViewModels;

/// <summary>The straightened page. Opening it starts the render when the outline changed since the
/// last one (the queue does the work; this screen waits and updates itself).</summary>
public partial class ResultViewModel(DocumentStore store, PageIngestQueue queue, PageEditService edit) : ObservableObject, IQueryAttributable
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
		Title = $"Trang {pages.ToList().IndexOf(page) + 1} · đã cắt";
		AspectText = page.FreeAspect ? "Khổ giấy: theo khung" : "Khổ giấy: A4";

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
			Status = "Đang cắt trang...";
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
		Image = ImageSource.FromFile(store.CroppedPath(_docId, page.Id, page.CroppedRevision));
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

	/// <summary>Back to the outline editor.</summary>
	[RelayCommand]
	private static Task EditAgainAsync() => Shell.Current.GoToAsync("..");

	/// <summary>Back to the page list of the document.</summary>
	[RelayCommand]
	private static Task DoneAsync() => Shell.Current.GoToAsync("../..");
}
