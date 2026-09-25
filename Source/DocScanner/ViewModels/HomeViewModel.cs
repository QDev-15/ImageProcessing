using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Services;
using ImageCoreService;

namespace DocScanner.ViewModels;

public partial class HomeViewModel(PermissionService permissions) : ObservableObject
{
	[ObservableProperty]
	private string status = "";

	/// <summary>Proof that ImageCore.Shared loads on the device.</summary>
	public string CoreInfo { get; } = $"Lõi xử lý ảnh: Sauvola k = {Binarizer.DefaultSauvolaK}";

	[RelayCommand]
	private async Task CaptureAsync()
	{
		Status = await permissions.EnsureCameraAsync()
			? "Đã có quyền camera. Chụp ảnh sẽ được làm ở Bước 3."
			: "Chưa được cấp quyền camera.";
	}

	[RelayCommand]
	private void PickFromGallery() => Status = "Nhập từ thư viện sẽ được làm ở Bước 3.";

	[RelayCommand]
	private Task OpenAsync(string route) => Shell.Current.GoToAsync(route);
}
