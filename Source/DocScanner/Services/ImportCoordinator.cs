using DocScanner.Core;

namespace DocScanner.Services;

/// <summary>
/// Glue between the system pickers (gallery / camera) and <see cref="BackgroundImporter"/>. Picking only collects the
/// photos; the copying runs in the background, so the caller opens the document at once and watches it fill in.
/// The target document is created lazily, only after the user actually picked something, so backing out of a picker
/// never leaves an empty document behind.
/// </summary>
public sealed class ImportCoordinator(BackgroundImporter importer, IPhotoPicker picker, PermissionService permissions)
{
	private bool _picking;

	/// <summary>The document the picked photos are going into, or null when nothing was picked.</summary>
	public async Task<DocumentRecord?> FromGalleryAsync(Func<DocumentRecord> document)
	{
		if (_picking) return null; // a second tap while the picker is opening
		_picking = true;
		try
		{
			IReadOnlyList<ImportSource> sources = await picker.PickAsync();
			if (sources.Count == 0) return null;
			DocumentRecord doc = document();
			importer.Start(doc.Id, sources);
			return doc;
		}
		finally
		{
			_picking = false;
		}
	}

	public async Task<DocumentRecord?> FromCameraAsync(Func<DocumentRecord> document)
	{
		if (!MediaPicker.Default.IsCaptureSupported)
		{
			await Shell.Current.DisplayAlertAsync("Không có camera", "Thiết bị này không hỗ trợ chụp ảnh.", "OK");
			return null;
		}
		if (!await permissions.EnsureCameraAsync()) return null;

		FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();
		if (photo == null) return null;
		DocumentRecord doc = document();
		string path = photo.FullPath;
		// The camera app leaves its JPEG in our cache; it is deleted once the document has its own copy.
		importer.Start(doc.Id, [new ImportSource(photo.FileName, _ => Task.FromResult<Stream>(
			new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1 << 16, FileOptions.DeleteOnClose)))]);
		return doc;
	}
}
