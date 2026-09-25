using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

public partial class HomeViewModel(DocumentStore store, ImportCoordinator importer, PageIngestQueue queue) : ImportViewModelBase
{
	private bool _resumed;

	public ObservableCollection<DocumentItem> Documents { get; } = [];

	/// <summary>Proof that ImageCore.Shared loads on the device.</summary>
	public string CoreInfo { get; } = $"Lõi xử lý ảnh: Sauvola k = {ImageCoreService.Binarizer.DefaultSauvolaK}";

	/// <summary>Follow the background pipeline while this screen is visible.</summary>
	public void Attach() => queue.PageUpdated += OnPageUpdated;

	public void Detach() => queue.PageUpdated -= OnPageUpdated;

	private void OnPageUpdated(PageUpdate update) =>
		MainThread.BeginInvokeOnMainThread(() =>
		{
			DocumentItem? item = Documents.FirstOrDefault(d => d.Record.Id == update.DocId);
			if (item != null) RefreshItem(item);
		});

	private void RefreshItem(DocumentItem item)
	{
		IReadOnlyList<PageRecord> pages = store.Pages(item.Record.Id);
		item.Refresh(pages, pages.Count > 0 ? store.ThumbPath(item.Record.Id, pages[0]) : null);
	}

	[RelayCommand]
	public async Task RefreshAsync()
	{
		IReadOnlyList<DocumentRecord> docs = await Task.Run(store.List);
		Documents.Clear();
		foreach (DocumentRecord d in docs)
		{
			var item = new DocumentItem(d, OpenDocument, DeleteDocument);
			RefreshItem(item);
			Documents.Add(item);
		}

		// Once per app run: continue whatever an earlier run left unfinished (killed mid-batch).
		if (!_resumed)
		{
			_resumed = true;
			await Task.Run(queue.ResumePending);
		}
	}

	[RelayCommand]
	private Task PickFromGalleryAsync() => ImportIntoNewDocumentAsync(importer.FromGalleryAsync);

	[RelayCommand]
	private Task CaptureAsync() => ImportIntoNewDocumentAsync(importer.FromCameraAsync);

	private async Task ImportIntoNewDocumentAsync(
		Func<Func<DocumentRecord>, IProgress<ImportProgress>, CancellationToken, Task<ImportOutcome?>> pick)
	{
		ImportOutcome? outcome = await RunImportAsync((progress, ct) => pick(() => store.Create(), progress, ct));
		if (outcome == null) return;

		if (outcome.Result.Added == 0)
		{
			store.Delete(outcome.Document.Id); // nothing usable was added: no empty document
			await RefreshAsync();
			return;
		}
		// The pages are already in the document (still being processed in the background): go straight in.
		await OpenAsync(outcome.Document.Id);
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
		await Task.Run(() => store.Delete(item.Record.Id));
		Documents.Remove(item);
	}
}
