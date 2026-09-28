using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocScanner.Services;

namespace DocScanner.ViewModels;

/// <summary>An exported PDF, one page at a time (zoomable; swipe or ‹ › for the other pages). The pages themselves are
/// rendered by the platform view (<c>PdfViewerPage</c>).</summary>
public partial class PdfViewerViewModel(ExportCoordinator exports) : ObservableObject, IQueryAttributable
{
	[ObservableProperty]
	private string title = "";

	[ObservableProperty]
	private string pageText = "";

	[ObservableProperty]
	private bool canGoPrevious;

	[ObservableProperty]
	private bool canGoNext;

	public string? Path { get; private set; }
	public int Index { get; private set; }
	public int PageCount { get; private set; }

	/// <summary>A new file was opened (the view opens its renderer and reports <see cref="SetPageCount"/>).</summary>
	public event Action<string>? FileChanged;

	/// <summary>Show page <see cref="Index"/>.</summary>
	public event Action<int>? PageRequested;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (!query.TryGetValue("path", out object? p) || p is not string path) return;
		Path = Uri.UnescapeDataString(path);
		Title = System.IO.Path.GetFileNameWithoutExtension(Path);
		Index = 0;
		FileChanged?.Invoke(Path);
	}

	public void SetPageCount(int count)
	{
		PageCount = count;
		ShowPage(0);
	}

	private void ShowPage(int index)
	{
		if (PageCount == 0)
		{
			PageText = "Không mở được file";
			CanGoPrevious = CanGoNext = false;
			return;
		}
		Index = Math.Clamp(index, 0, PageCount - 1);
		PageText = $"Trang {Index + 1}/{PageCount}";
		CanGoPrevious = Index > 0;
		CanGoNext = Index < PageCount - 1;
		PageRequested?.Invoke(Index);
	}

	[RelayCommand]
	private void Go(int delta)
	{
		int next = Index + delta;
		if (next >= 0 && next < PageCount) ShowPage(next);
	}

	/// <summary>Share / save / open with another app / delete.</summary>
	[RelayCommand]
	private async Task MoreAsync()
	{
		if (Path == null) return;
		bool deleted = await exports.OfferActionsAsync(Path, Title, Title, allowDelete: true, offerView: false);
		if (deleted) await Shell.Current.GoToAsync("..");
	}
}
