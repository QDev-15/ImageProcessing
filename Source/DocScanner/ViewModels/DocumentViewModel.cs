using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>One document: its pages (filling in as the background pipeline works), plus adding more photos.</summary>
public partial class DocumentViewModel(DocumentStore store, ImportCoordinator importer, PageIngestQueue queue)
	: ImportViewModelBase, IQueryAttributable
{
	private string? _docId;

	public ObservableCollection<PageItem> Pages { get; } = [];

	[ObservableProperty]
	private string title = "";

	/// <summary>"Đang xử lý n ảnh..." while pages are still being prepared in the background.</summary>
	[ObservableProperty]
	private string progressText = "";

	[ObservableProperty]
	private bool hasProgress;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("docId", out object? id) && id is string s)
		{
			_docId = s;
			Reload();
		}
	}

	/// <summary>Follow the background pipeline while this screen is visible.</summary>
	public void Attach()
	{
		queue.PageUpdated += OnPageUpdated;
		Reload();
	}

	public void Detach() => queue.PageUpdated -= OnPageUpdated;

	private void OnPageUpdated(PageUpdate update) =>
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (update.DocId != _docId) return;
			Pages.FirstOrDefault(p => p.Record.Id == update.PageId)?.Refresh();
			UpdateProgress();
		});

	/// <summary>Rebuilds the tiles from the document.</summary>
	public void Reload()
	{
		if (_docId == null) return;
		DocumentRecord? doc = store.Get(_docId);
		if (doc == null) return;

		Title = doc.Name;
		IReadOnlyList<PageRecord> pages = store.Pages(_docId);
		Pages.Clear();
		for (int i = 0; i < pages.Count; i++)
			Pages.Add(new PageItem(pages[i], i + 1, ThumbFor, OpenPage, DeletePage));
		UpdateProgress();
	}

	/// <summary>The straightened thumbnail once there is a current one, else the plain thumbnail.</summary>
	private string ThumbFor(PageRecord p) =>
		p.CroppedRevision > 0 && !p.NeedsRender
			? store.CroppedThumbPath(_docId!, p.Id, p.CroppedRevision)
			: store.ThumbPath(_docId!, p);

	private void UpdateProgress()
	{
		int busy = Pages.Count(p => p.Record.State is PageState.Pending or PageState.Preview);
		HasProgress = busy > 0;
		ProgressText = busy > 0 ? $"Đang xử lý {busy} ảnh..." : "";
	}

	[RelayCommand]
	private Task AddFromGalleryAsync() => AddAsync(importer.FromGalleryAsync);

	[RelayCommand]
	private Task AddFromCameraAsync() => AddAsync(importer.FromCameraAsync);

	private async Task AddAsync(
		Func<Func<DocumentRecord>, IProgress<ImportProgress>, CancellationToken, Task<ImportOutcome?>> pick)
	{
		if (_docId == null) return;
		DocumentRecord? doc = store.Get(_docId);
		if (doc == null) return;
		await RunImportAsync((progress, ct) => pick(() => doc, progress, ct));
		Reload();
	}

	private void OpenPage(PageItem item)
	{
		if (_docId == null) return;
		if (item.Record.State == PageState.Failed)
		{
			_ = Shell.Current.DisplayAlertAsync("Không đọc được ảnh", item.Record.Error ?? "Định dạng không hỗ trợ.", "OK");
			return;
		}
		_ = Shell.Current.GoToAsync($"{AppShell.Routes.Crop}?docId={_docId}&pageId={item.Record.Id}");
	}

	private void DeletePage(PageItem item) => _ = ConfirmDeletePageAsync(item);

	private async Task ConfirmDeletePageAsync(PageItem item)
	{
		if (_docId == null) return;
		bool ok = await Shell.Current.DisplayAlertAsync("Xoá trang", $"Xoá {item.Label}?", "Xoá", "Giữ lại");
		if (!ok) return;
		string docId = _docId;
		await Task.Run(() => store.DeletePage(docId, item.Record.Id));
		Reload();
	}
}
