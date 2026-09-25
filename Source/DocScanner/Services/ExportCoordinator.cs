using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;

namespace DocScanner.Services;

/// <summary>
/// "Xuất PDF" from any screen (document, crop, result): runs the export with its own progress overlay
/// (<see cref="Views.ExportOverlay"/>, bound to this singleton), saves the PDF into the export library,
/// then offers Share / Save to Downloads / Open.
/// </summary>
public partial class ExportCoordinator(DocumentStore store, PdfExportService exporter, ExportLibrary library, IDownloadsService downloads)
	: ObservableObject
{
	private CancellationTokenSource? _cts;

	[ObservableProperty]
	private bool isExporting;

	[ObservableProperty]
	private string exportText = "";

	[RelayCommand]
	private void Cancel() => _cts?.Cancel();

	public async Task ExportAsync(string docId)
	{
		if (IsExporting) return;
		DocumentRecord? doc = store.Get(docId);
		if (doc == null) return;
		if (store.Pages(docId).Count == 0)
		{
			await Shell.Current.DisplayAlertAsync("Xuất PDF", "Tài liệu chưa có trang nào.", "OK");
			return;
		}

		PdfQuality? quality = await AskQualityAsync();
		if (quality == null) return;

		string path = library.NewPath(doc.Name, DateTime.Now);
		_cts = new CancellationTokenSource();
		IsExporting = true;
		ExportText = "Đang chuẩn bị xuất PDF...";
		PdfExportResult? result = null;
		try
		{
			var progress = new UiProgress(p => ExportText = $"{p.Stage} {p.Done}/{p.Total}...");
			CancellationToken ct = _cts.Token;
			result = await Task.Run(() => exporter.ExportAsync(docId, path, progress, ct, quality), ct);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			IsExporting = false;
			await Shell.Current.DisplayAlertAsync("Không xuất được PDF", ex.Message, "OK");
			return;
		}
		finally
		{
			IsExporting = false;
			_cts.Dispose();
			_cts = null;
		}

		if (result == null) return;
		string summary = $"{result.PageCount} trang · {result.Bytes / 1024.0:0} KB";
		if (result.SkippedPages.Count > 0)
			summary += $"\nBỏ qua trang {string.Join(", ", result.SkippedPages)} (ảnh lỗi hoặc chưa cắt được).";
		await OfferActionsAsync(result.Path, doc.Name, $"Đã xuất PDF ({summary})", allowDelete: false);
	}

	private const string QualityPreference = "pdf_quality";

	/// <summary>Small / medium / high (see <see cref="PdfQuality"/>); the last choice is ticked and remembered. Null = cancelled.</summary>
	private static async Task<PdfQuality?> AskQualityAsync()
	{
		string last = Preferences.Default.Get(QualityPreference, PdfQuality.Medium.Key);
		string[] labels = PdfQuality.All.Select(q => q.Key == last ? q.Label + "  ✓" : q.Label).ToArray();
		string? pick = await Shell.Current.DisplayActionSheetAsync("Chất lượng PDF", "Huỷ", null, labels);
		int index = pick == null ? -1 : Array.IndexOf(labels, pick);
		if (index < 0) return null;
		PdfQuality chosen = PdfQuality.All[index];
		Preferences.Default.Set(QualityPreference, chosen.Key);
		return chosen;
	}

	/// <summary>Share / save / open (and optionally delete) an exported PDF. Returns true when it was deleted.</summary>
	public async Task<bool> OfferActionsAsync(string path, string title, string heading, bool allowDelete)
	{
		const string share = "Chia sẻ...", save = "Lưu vào Tải xuống", open = "Mở";
		var actions = new List<string> { open, share };
		if (downloads.IsSupported) actions.Add(save);

		string? choice = await Shell.Current.DisplayActionSheetAsync(heading, "Đóng", allowDelete ? "Xoá" : null, [.. actions]);
		try
		{
			switch (choice)
			{
				case share:
					await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path, "application/pdf") });
					break;
				case save:
					string saved = await downloads.SaveAsync(path, Path.GetFileName(path), "application/pdf");
					await Shell.Current.DisplayAlertAsync("Đã lưu", $"Đã lưu vào thư mục Tải xuống:\n{saved}", "OK");
					break;
				case open:
					await Launcher.Default.OpenAsync(new OpenFileRequest(title, new ReadOnlyFile(path, "application/pdf")));
					break;
				case "Xoá":
					return library.Delete(path);
			}
		}
		catch (Exception ex)
		{
			await Shell.Current.DisplayAlertAsync("Lỗi", ex.Message, "OK");
		}
		return false;
	}

	/// <summary>Marshals progress to the UI thread; reports arriving after the export ended are dropped.</summary>
	private sealed class UiProgress(Action<ExportProgress> apply) : IProgress<ExportProgress>
	{
		public void Report(ExportProgress value) => MainThread.BeginInvokeOnMainThread(() => apply(value));
	}
}
