using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>Every setting of the app in one place (each one is stored the moment it changes, in the same place the
/// screens read it from), the storage used, updates, and the app information.</summary>
public partial class SettingsViewModel(IAppUpdater updater, DocumentStore store, ExportLibrary exportsLibrary) : ObservableObject
{
	// Keys shared with the screens that use them.
	private const string CameraAutoKey = "camera_auto_capture", OpenModeKey = "document_open_mode", PdfQualityKey = "pdf_quality";

	public IReadOnlyList<string> OpenModes { get; } = ["Xem (phóng to, lật trang)", "Sửa (khung, bộ lọc)"];
	public IReadOnlyList<string> PdfQualities { get; } = PdfQuality.All.Select(q => q.Label).ToList();

	[ObservableProperty]
	private bool cameraAutoCapture = Preferences.Default.Get(CameraAutoKey, true);

	[ObservableProperty]
	private int openModeIndex = Preferences.Default.Get(OpenModeKey, "view") == "view" ? 0 : 1;

	[ObservableProperty]
	private int pdfQualityIndex = Math.Max(0, PdfQuality.All.ToList().IndexOf(PdfQuality.FromKey(Preferences.Default.Get(PdfQualityKey, PdfQuality.Medium.Key))));

	[ObservableProperty]
	private bool autoUpdate = updater.AutoUpdate;

	[ObservableProperty]
	private string updateUrl = updater.ServerUrl;

	[ObservableProperty]
	private string updateStatus = updater.LastStatus;

	[ObservableProperty]
	private bool isChecking;

	[ObservableProperty]
	private string storageText = "";

	public string VersionText => $"Phiên bản {AppInfo.Current.VersionString} (bản dựng {AppInfo.Current.BuildString})";

	public bool CanUpdate => updater.UnsupportedReason == null;
	public string UpdateHint => updater.UnsupportedReason
		?? "Mỗi đêm lúc 01:00 ứng dụng đọc tệp update.json ở địa chỉ bên dưới; có bản mới thì tự tải, kiểm tra và cài.";

	partial void OnCameraAutoCaptureChanged(bool value) => Preferences.Default.Set(CameraAutoKey, value);
	partial void OnOpenModeIndexChanged(int value) => Preferences.Default.Set(OpenModeKey, value == 0 ? "view" : "edit");
	partial void OnPdfQualityIndexChanged(int value)
	{
		if (value >= 0 && value < PdfQuality.All.Count) Preferences.Default.Set(PdfQualityKey, PdfQuality.All[value].Key);
	}

	partial void OnAutoUpdateChanged(bool value)
	{
		updater.AutoUpdate = value;
		updater.Schedule(replace: true);
		if (value) _ = AskNotificationsAsync();
	}

	/// <summary>Stored when the field is left (not on every key stroke).</summary>
	[RelayCommand]
	private void SaveUrl()
	{
		updater.ServerUrl = UpdateUrl;
		updater.Schedule(replace: true);
	}

	/// <summary>The install confirmation may come as a notification when the update runs at night (Android 13+ asks first).</summary>
	private static async Task AskNotificationsAsync()
	{
		try { await Permissions.RequestAsync<Permissions.PostNotifications>(); } catch (Exception) { }
	}

	[RelayCommand]
	private async Task CheckNowAsync()
	{
		if (IsChecking) return;
		SaveUrl();
		IsChecking = true;
		try
		{
			string result = await updater.CheckAsync(install: false);
			if (result.StartsWith("Có bản mới"))
			{
				bool install = await Shell.Current.DisplayAlertAsync("Cập nhật", result + " Tải và cài ngay?", "Cài", "Để sau");
				if (install) result = await updater.CheckAsync(install: true);
			}
			UpdateStatus = updater.LastStatus;
			if (!result.StartsWith("Có bản mới")) await Shell.Current.DisplayAlertAsync("Cập nhật", result, "OK");
		}
		finally
		{
			IsChecking = false;
		}
	}

	/// <summary>Space used on the phone by the documents (originals, renders) and the exported PDFs.</summary>
	public async Task LoadStorageAsync()
	{
		(long docs, int count, long pdfs, int pdfCount) = await Task.Run(() =>
		{
			long Size(string dir) => Directory.Exists(dir)
				? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
			IReadOnlyList<ExportedFile> exported = exportsLibrary.List();
			return (Size(store.Root), store.List().Count, exported.Sum(f => f.Bytes), exported.Count);
		});
		StorageText = $"{count} tài liệu · {ExportsViewModel.Size(docs)}\n"
			+ (pdfCount == 0 ? "Chưa có PDF đã xuất" : $"{pdfCount} PDF đã xuất · {ExportsViewModel.Size(pdfs)}");
	}

	[RelayCommand]
	private Task OpenAboutAsync() => Shell.Current.GoToAsync(AppShell.Routes.About);
}
