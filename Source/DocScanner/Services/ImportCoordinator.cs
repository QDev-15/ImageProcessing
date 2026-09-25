using DocScanner.Core;

namespace DocScanner.Services;

/// <summary>Result of one pick / capture: the document that received the pages, and how the
/// import went.</summary>
public sealed record ImportOutcome(DocumentRecord Document, ImportResult Result);

/// <summary>
/// Glue between the system pickers (gallery / camera) and <see cref="ImportService"/>.
/// The target document is created lazily, only after the user actually picked something, so
/// cancelling a picker never leaves an empty document behind.
/// </summary>
public sealed class ImportCoordinator(ImportService import, PermissionService permissions)
{
	public async Task<ImportOutcome?> FromGalleryAsync(Func<DocumentRecord> document,
		IProgress<ImportProgress> progress, CancellationToken ct)
	{
		// SelectionLimit 0 = no limit. Android's photo picker shows no storage permission prompt.
		List<FileResult> picked = await MediaPicker.Default.PickPhotosAsync(
			new MediaPickerOptions { Title = "Chọn ảnh tài liệu", SelectionLimit = 0 });
		List<FileResult> files = picked.OfType<FileResult>().ToList();
		if (files.Count == 0) return null;

		DocumentRecord doc = document();
		var sources = files.Select(f => new ImportSource(f.FileName, _ => f.OpenReadAsync())).ToList();
		return new ImportOutcome(doc, await import.ImportAsync(doc, sources, progress, ct));
	}

	public async Task<ImportOutcome?> FromCameraAsync(Func<DocumentRecord> document,
		IProgress<ImportProgress> progress, CancellationToken ct)
	{
		if (!MediaPicker.Default.IsCaptureSupported)
		{
			await Shell.Current.DisplayAlertAsync("Không có camera", "Thiết bị này không hỗ trợ chụp ảnh.", "OK");
			return null;
		}
		if (!await permissions.EnsureCameraAsync()) return null;

		FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();
		if (photo == null) return null;
		try
		{
			DocumentRecord doc = document();
			var sources = new List<ImportSource> { new(photo.FileName, _ => photo.OpenReadAsync()) };
			return new ImportOutcome(doc, await import.ImportAsync(doc, sources, progress, ct));
		}
		finally
		{
			// The camera app leaves its JPEG in our cache; the document now owns a copy.
			try { File.Delete(photo.FullPath); } catch (IOException) { }
		}
	}
}
