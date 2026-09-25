using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>One document: its pages (filling in as the background pipeline works), adding more photos,
/// reordering / deleting pages with one level of undo, renaming, and exporting a PDF.</summary>
public partial class DocumentViewModel(DocumentStore store, ImportCoordinator importer, PageIngestQueue queue, ExportCoordinator exports)
	: ImportViewModelBase, IQueryAttributable
{
	private string? _docId;
	private PageItem? _dragged;

	/// <summary>The last undoable change (page moved or deleted); null when there is nothing to undo.</summary>
	private Func<bool>? _undo;

	public ObservableCollection<PageItem> Pages { get; } = [];

	[ObservableProperty]
	private string title = "";

	/// <summary>"Đang xử lý n ảnh..." while pages are still being prepared in the background.</summary>
	[ObservableProperty]
	private string progressText = "";

	[ObservableProperty]
	private bool hasProgress;

	/// <summary>Text of the undo bar ("Đã xoá Trang 3"); the bar shows while it is not empty.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanUndo))]
	private string undoText = "";

	public bool CanUndo => UndoText.Length > 0;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("docId", out object? id) && id is string s)
		{
			if (s != _docId) ClearUndo();
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
			Pages.Add(new PageItem(pages[i], i + 1, ThumbFor, OpenPage, DeletePage, ShowPageMenu, p => _dragged = p, DropOn));
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

	#region Reorder / delete / undo

	/// <summary>Deletes at once (no confirmation): the undo bar can bring the page back.</summary>
	private void DeletePage(PageItem item)
	{
		if (_docId == null) return;
		string docId = _docId;
		store.EmptyTrash(docId); // only the most recent deletion is undoable
		DeletedPage? deleted = store.TrashPage(docId, item.Record.Id);
		if (deleted == null) return;
		SetUndo($"Đã xoá {item.Label}", () => store.RestorePage(deleted));
		Reload();
	}

	private void MovePage(PageItem item, int newIndex)
	{
		if (_docId == null) return;
		List<string> before = Pages.Select(p => p.Record.Id).ToList();
		if (!store.MovePage(_docId, item.Record.Id, newIndex)) return;
		string docId = _docId;
		SetUndo($"Đã chuyển {item.Label}", () => store.SetOrder(docId, before));
		Reload();
	}

	/// <summary>Drag and drop: the dragged tile takes the place of the tile it is dropped on.</summary>
	private void DropOn(PageItem target)
	{
		PageItem? dragged = _dragged;
		_dragged = null;
		if (dragged == null || dragged == target) return;
		int to = Pages.IndexOf(target);
		if (to >= 0) MovePage(dragged, to);
	}

	private async void ShowPageMenu(PageItem item)
	{
		int index = Pages.IndexOf(item), last = Pages.Count - 1;
		var actions = new List<string> { "Mở / chỉnh khung" };
		if (index > 0) actions.AddRange(["Đưa lên đầu", "Lên trước 1 trang"]);
		if (index < last) actions.AddRange(["Ra sau 1 trang", "Đưa xuống cuối"]);
		string? choice = await Shell.Current.DisplayActionSheetAsync(item.Label, "Đóng", "Xoá trang", [.. actions]);
		switch (choice)
		{
			case "Mở / chỉnh khung": OpenPage(item); break;
			case "Đưa lên đầu": MovePage(item, 0); break;
			case "Lên trước 1 trang": MovePage(item, index - 1); break;
			case "Ra sau 1 trang": MovePage(item, index + 1); break;
			case "Đưa xuống cuối": MovePage(item, last); break;
			case "Xoá trang": DeletePage(item); break;
		}
	}

	private void SetUndo(string text, Func<bool> undo)
	{
		_undo = undo;
		UndoText = text;
	}

	private void ClearUndo()
	{
		_undo = null;
		UndoText = "";
		if (_docId != null) store.EmptyTrash(_docId);
	}

	[RelayCommand]
	private void Undo()
	{
		Func<bool>? undo = _undo;
		_undo = null;
		UndoText = "";
		if (undo != null && !undo())
			_ = Shell.Current.DisplayAlertAsync("Không hoàn tác được", "Trang đã bị xoá hẳn.", "OK");
		Reload();
	}

	[RelayCommand]
	private void DismissUndo() => ClearUndo();

	#endregion

	[RelayCommand]
	private async Task RenameAsync()
	{
		if (_docId == null) return;
		string? name = await Shell.Current.DisplayPromptAsync("Đổi tên tài liệu", "Tên mới:", "Lưu", "Huỷ",
			initialValue: Title, maxLength: 120, keyboard: Keyboard.Text);
		if (name != null && store.Rename(_docId, name)) Title = store.Get(_docId)!.Name;
	}

	[RelayCommand]
	private Task ExportPdfAsync()
	{
		if (_docId == null) return Task.CompletedTask;
		ClearUndo();
		return exports.ExportAsync(_docId);
	}
}
