using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

public partial class HomeViewModel(DocumentStore store, ImportCoordinator importer, BackgroundImporter imports, PageIngestQueue queue,
	ExportCoordinator exports)
	: CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
	private bool _resumed;

	public ObservableCollection<DocumentItem> Documents { get; } = [];

	/// <summary>Follow the background pipeline while this screen is visible.</summary>
	public void Attach()
	{
		queue.PageUpdated += OnPageUpdated;
		imports.Changed += OnImportChanged;
	}

	public void Detach()
	{
		queue.PageUpdated -= OnPageUpdated;
		imports.Changed -= OnImportChanged;
	}

	private void OnPageUpdated(PageUpdate update) => RefreshLater(update.DocId);

	private void OnImportChanged(string docId) => RefreshLater(docId);

	private void RefreshLater(string docId) =>
		MainThread.BeginInvokeOnMainThread(() =>
		{
			DocumentItem? item = Documents.FirstOrDefault(d => d.Record.Id == docId);
			if (item != null) RefreshItem(item);
		});

	private void RefreshItem(DocumentItem item)
	{
		IReadOnlyList<PageRecord> pages = store.Pages(item.Record.Id);
		item.Refresh(pages, pages.Count > 0 ? CoverThumb(item.Record.Id, pages[0]) : null, imports.Status(item.Record.Id));
	}

	/// <summary>The first page as the user sees it: straightened once it has a current render.</summary>
	private string CoverThumb(string docId, PageRecord p) =>
		p.CroppedRevision > 0 && !p.NeedsRender ? store.CroppedThumbPath(docId, p.Id, p.CroppedRevision) : store.ThumbPath(docId, p);

	[RelayCommand]
	public async Task RefreshAsync()
	{
		IReadOnlyList<DocumentRecord> docs = await Task.Run(store.List);
		Perf.Log($"startup: {docs.Count} documents listed");
		Documents.Clear();
		foreach (DocumentRecord d in docs)
		{
			var item = new DocumentItem(d, OpenDocument, DeleteDocument, ShowDocumentMenu);
			RefreshItem(item);
			Documents.Add(item);
		}

		// Once per app run: continue whatever an earlier run left unfinished (killed mid-batch).
		if (!_resumed)
		{
			_resumed = true;
			foreach (DocumentRecord d in docs)
			{
				store.EmptyTrash(d.Id); // pages deleted in an earlier run can no longer be undone
				store.RemoveUnfinishedImports(d.Id); // photos an earlier run never got to copy
			}
			foreach (DocumentItem item in Documents) RefreshItem(item); // page counts without those placeholders
			Perf.Log("startup: rows built");
			await Task.Run(queue.ResumePending);
		}
	}

	[RelayCommand]
	private Task PickFromGalleryAsync() => ImportIntoNewDocumentAsync(importer.FromGalleryAsync);

	[RelayCommand]
	private Task CaptureAsync() => ImportIntoNewDocumentAsync(importer.FromCameraAsync);

	/// <summary>Pick, then go straight into the new document: its photos are copied in the background and show up
	/// there one by one, with the import's progress.</summary>
	private async Task ImportIntoNewDocumentAsync(Func<Func<DocumentRecord>, Task<DocumentRecord?>> pick)
	{
		DocumentRecord? doc = await pick(() => store.Create());
		if (doc != null) await OpenAsync(doc.Id);
	}

	private void OpenDocument(DocumentItem item) => _ = OpenAsync(item.Record.Id);

	private static Task OpenAsync(string docId) =>
		Shell.Current.GoToAsync($"{AppShell.Routes.Document}?docId={docId}");

	private void DeleteDocument(DocumentItem item) => _ = ConfirmDeleteAsync(item);

	private async Task ConfirmDeleteAsync(DocumentItem item)
	{
		int count = store.Pages(item.Record.Id).Count;
		bool ok = await Shell.Current.DisplayAlertAsync("Xoá tài liệu",
			$"Xoá \"{item.Name}\" cùng {count} trang? Không thể hoàn tác.", "Xoá", "Giữ lại");
		if (!ok) return;
		imports.Stop(item.Record.Id); // photos still waiting would otherwise go into a deleted document
		await Task.Run(() => store.Delete(item.Record.Id));
		Documents.Remove(item);
	}

	private void ShowDocumentMenu(DocumentItem item) => _ = DocumentMenuAsync(item);

	/// <summary>The row's ⋮ menu: the everyday actions without opening the document.</summary>
	private async Task DocumentMenuAsync(DocumentItem item)
	{
		const string rename = "Đổi tên", export = "Xuất PDF", delete = "Xoá";
		string? choice = await Shell.Current.DisplayActionSheetAsync(item.Name, "Huỷ", delete, rename, export);
		switch (choice)
		{
			case rename:
				string? name = await Shell.Current.DisplayPromptAsync("Đổi tên tài liệu", "Tên mới:", "Lưu", "Huỷ",
					initialValue: item.Name, maxLength: 120, keyboard: Keyboard.Text);
				if (name != null && store.Rename(item.Record.Id, name)) RefreshItem(item);
				break;
			case export:
				await exports.ExportAsync(item.Record.Id);
				break;
			case delete:
				await ConfirmDeleteAsync(item);
				break;
		}
	}

	/// <summary>The list of exported PDFs (open / share / save / delete).</summary>
	[RelayCommand]
	private Task OpenExportsAsync() => Shell.Current.GoToAsync(AppShell.Routes.Exports);
}
