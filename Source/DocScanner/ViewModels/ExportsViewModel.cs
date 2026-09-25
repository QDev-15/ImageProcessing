using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Core;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>"PDF đã xuất": every exported PDF, newest first; tap for open / share / save / delete.</summary>
public partial class ExportsViewModel(ExportLibrary library, ExportCoordinator exports) : ObservableObject
{
	public ObservableCollection<ExportItem> Files { get; } = [];

	[ObservableProperty]
	private string summary = "";

	[ObservableProperty]
	private bool isRefreshing;

	[RelayCommand]
	public async Task RefreshAsync()
	{
		IReadOnlyList<ExportedFile> files = await Task.Run(library.List);
		Files.Clear();
		foreach (ExportedFile f in files) Files.Add(new ExportItem(f, OpenAsync, DeleteAsync));
		long bytes = files.Sum(f => f.Bytes);
		Summary = files.Count == 0 ? "" : $"{files.Count} file · {Size(bytes)}";
		IsRefreshing = false;
	}

	private async Task OpenAsync(ExportItem item)
	{
		bool deleted = await exports.OfferActionsAsync(item.File.Path, item.File.Name, $"{item.File.Name}\n{item.Details}", allowDelete: true);
		if (deleted) await RefreshAsync();
	}

	private async Task DeleteAsync(ExportItem item)
	{
		bool ok = await Shell.Current.DisplayAlertAsync("Xoá PDF", $"Xoá \"{item.File.Name}\"?\n(Tài liệu gốc vẫn còn, có thể xuất lại.)", "Xoá", "Giữ lại");
		if (!ok) return;
		library.Delete(item.File.Path);
		await RefreshAsync();
	}

	public static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1048576.0:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

public sealed class ExportItem
{
	public ExportItem(ExportedFile file, Func<ExportItem, Task> open, Func<ExportItem, Task> delete)
	{
		File = file;
		Details = $"{file.Created:dd/MM/yyyy HH:mm} · {ExportsViewModel.Size(file.Bytes)}";
		OpenCommand = new Command(() => _ = open(this));
		DeleteCommand = new Command(() => _ = delete(this));
	}

	public ExportedFile File { get; }
	public string Name => File.Name;
	public string Details { get; }
	public ICommand OpenCommand { get; }
	public ICommand DeleteCommand { get; }
}
