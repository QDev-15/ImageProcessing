using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using DocScanner.Core;

namespace DocScanner.ViewModels;

/// <summary>Row of the document list. Its thumbnail and page counts follow the background
/// pipeline (see <see cref="Refresh"/>).</summary>
public partial class DocumentItem : ObservableObject
{
	private string? _thumbPath;

	public DocumentItem(DocumentRecord record, Action<DocumentItem> open, Action<DocumentItem> delete)
	{
		Record = record;
		Name = record.Name;
		OpenCommand = new Command(() => open(this));
		DeleteCommand = new Command(() => delete(this));
	}

	public DocumentRecord Record { get; }
	[ObservableProperty]
	private string name = "";

	public ICommand OpenCommand { get; }
	public ICommand DeleteCommand { get; }

	[ObservableProperty]
	private string subtitle = "";

	[ObservableProperty]
	private ImageSource? thumb;

	/// <param name="pages">Snapshot of the document's pages.</param>
	/// <param name="firstThumbPath">Where the first page's thumbnail will be, once it exists.</param>
	public void Refresh(IReadOnlyList<PageRecord> pages, string? firstThumbPath)
	{
		int busy = pages.Count(p => p.State is PageState.Pending or PageState.Preview);
		string date = Record.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
		Subtitle = busy > 0 ? $"{pages.Count} trang · đang xử lý {busy} · {date}" : $"{pages.Count} trang · {date}";

		Name = Record.Name; // may have been renamed
		if (firstThumbPath != null && firstThumbPath != _thumbPath && File.Exists(firstThumbPath))
		{
			Thumb = ImageSource.FromFile(firstThumbPath);
			_thumbPath = firstThumbPath;
		}
	}
}

/// <summary>Tile of the page grid inside a document. The tile exists from the moment the page is
/// added (a spinner), shows the thumbnail as soon as it has been made, and marks a photo that
/// could not be read.</summary>
public partial class PageItem : ObservableObject
{
	private readonly Func<PageRecord, string> _thumbPath;
	private string? _loadedKey;

	/// <param name="thumbPath">Which file to show for the page (the straightened thumbnail once there is a
	/// current one, otherwise the plain thumbnail).</param>
	public PageItem(PageRecord record, int number, Func<PageRecord, string> thumbPath, Action<PageItem> open, Action<PageItem> delete,
		Action<PageItem>? menu = null, Action<PageItem>? dragStart = null, Action<PageItem>? drop = null)
	{
		Record = record;
		_thumbPath = thumbPath;
		Label = $"Trang {number}";
		OpenCommand = new Command(() => open(this));
		DeleteCommand = new Command(() => delete(this));
		MenuCommand = new Command(() => menu?.Invoke(this));
		DragStartingCommand = new Command(() => dragStart?.Invoke(this));
		DropCommand = new Command(() => drop?.Invoke(this));
		Refresh();
	}

	public PageRecord Record { get; }
	public string Label { get; }
	public ICommand OpenCommand { get; }
	public ICommand DeleteCommand { get; }
	/// <summary>Page actions (move, delete...).</summary>
	public ICommand MenuCommand { get; }
	/// <summary>Drag to reorder: this tile is picked up / another tile is dropped on it.</summary>
	public ICommand DragStartingCommand { get; }
	public ICommand DropCommand { get; }

	[ObservableProperty]
	private ImageSource? thumb;

	[ObservableProperty]
	private bool showThumb;

	[ObservableProperty]
	private bool showSpinner;

	[ObservableProperty]
	private bool isFailed;

	[ObservableProperty]
	private string statusText = "";

	/// <summary>Re-reads the page state (called when the pipeline reports a change).</summary>
	public void Refresh()
	{
		PageState state = Record.State;
		IsFailed = state == PageState.Failed;
		StatusText = IsFailed ? "Không đọc được ảnh" : "";

		if (state == PageState.Pending)
		{
			Thumb = null; // being (re)built: do not keep showing the old picture
			_loadedKey = null;
		}
		else if (state is PageState.Preview or PageState.Ready)
		{
			// Reload when the file, the rotation or the render changes (the plain thumbnail keeps its name).
			string path = _thumbPath(Record);
			string key = $"{path}|{Record.UserRotation}|{Record.CroppedRevision}";
			if (key != _loadedKey && File.Exists(path))
			{
				Thumb = ImageSource.FromFile(path);
				_loadedKey = key;
			}
		}

		ShowThumb = Thumb != null && !IsFailed;
		ShowSpinner = state == PageState.Pending || (state == PageState.Preview && Thumb == null);
	}
}
