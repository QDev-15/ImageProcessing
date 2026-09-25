using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ImageCoreService;

namespace ImageOptimizerTool;

/// <summary>
/// Main window: page list (thumbnails, drag to reorder, drop files to import), preview,
/// scan / import / export, page edits with undo. All heavy work (image processing, OCR,
/// export) runs on background tasks with a progress bar and a cancel link; the page list
/// itself lives in <see cref="ScanProject"/> (auto-saved session or saved project folder).
/// </summary>
public partial class MainForm : Form
{
    private AppSettings _settings;
    private ScanProject _project = null!;
    private ScannerService? _scanner;
    private CancellationTokenSource? _work;
    private PageIngestor _ingest = null!;
    private IngestProgress _ingestProgress;
    private string? _lastProfileName;

    // Orientation-detection engines (Tesseract is not thread-safe): one per concurrent worker.
    private readonly OsdEnginePool _osdPool = new();
    private BackgroundOcr _bgOcr = null!;
    private string _ocrProgress = "";


    private readonly string[] _startupFiles;

    public MainForm(AppSettings settings, string[]? startupFiles = null)
    {
        _settings = settings;
        _startupFiles = startupFiles ?? Array.Empty<string>();
        InitializeComponent();
        InitZoomControls();
        InitVirtualList();
        _bgOcr = new BackgroundOcr(() => _project, () => _settings, () => _lastProfileName,
            () => _work == null && _scanner?.IsScanning != true && (_ingest?.Pending ?? 0) == 0);
        _bgOcr.ProgressChanged += (done, total) => BeginInvokeSafe(() =>
        {
            _ocrProgress = total == 0 ? "" : $"  |  OCR nền {done}/{total}";
            UpdatePageCountLabel();
        });
    }

    /// <summary>Zoom buttons + zoom % label on the toolbar, wired to the preview.</summary>
    private void InitZoomControls()
    {
        var lblZoom = new ToolStripLabel("100%") { AutoSize = false, Width = 48, TextAlign = ContentAlignment.MiddleCenter, ToolTipText = "Mức zoom" };
        ToolStripButton Btn(string text, string tip, Action act)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = tip };
            b.Click += (_, _) => act();
            return b;
        }

        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(Btn("Zoom −", "Thu nhỏ (cuộn chuột xuống)", picPreview.ZoomOut));
        toolStrip.Items.Add(lblZoom);
        toolStrip.Items.Add(Btn("Zoom +", "Phóng to (cuộn chuột lên)", picPreview.ZoomIn));
        toolStrip.Items.Add(Btn("Vừa khung", "Vừa khung xem (double-click ảnh)", picPreview.FitToWindow));
        toolStrip.Items.Add(Btn("100%", "Kích thước thật (1 pixel ảnh = 1 pixel màn hình)", picPreview.ActualSize));
        picPreview.ZoomChanged += (_, _) => lblZoom.Text = $"{picPreview.Zoom * 100:0}%";
    }

    #region Lifecycle

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        ApplyThumbnailSize();
        string work = _settings.GetWorkFolder();
        string? last = ScanProject.FindLatestSession(work);
        if (last != null && MessageBox.Show(this,
                "Phiên làm việc trước chưa được lưu thành dự án (có thể app đã bị đóng đột ngột).\n\nMở lại phiên đó?",
                "Khôi phục phiên", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try { SetProject(ScanProject.Open(last)); }
            catch (Exception ex) { Log.Error("Restoring session failed", ex); SetProject(ScanProject.NewSession(work)); }
        }
        else
        {
            if (last != null) TryDeleteFolder(last);
            SetProject(ScanProject.NewSession(work));
        }
        CleanOldEmptySessions(work);
        SetStatus("Sẵn sàng. Kéo thả file ảnh / PDF vào danh sách bên trái để import.");

        // Files passed on the command line (Explorer "Open with", drag onto the exe).
        string[] files = _startupFiles.Where(f => File.Exists(f) && PageImporter.IsSupported(f)).ToArray();
        if (files.Length > 0) await ImportFilesAsync(files, insertAt: null);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_scanner?.IsScanning == true)
        {
            MessageBox.Show(this, "Đang scan. Hãy huỷ scan hoặc đợi scan xong trước khi thoát.", "Thoát", MessageBoxButtons.OK, MessageBoxIcon.Information);
            e.Cancel = true;
            return;
        }
        _work?.Cancel();
        _bgOcr.Dispose();
        _ingest.Dispose();
        _thumbs.Dispose();
        _project.Flush();
        _scanner?.Dispose();
        _osdPool.Dispose();
        // An empty session leaves nothing behind; a non-empty one is offered for restore next time.
        if (_project.IsSession && _project.Pages.Count == 0) _project.DeleteIfSession();
        Log.Info("App closed");
    }

    private void SetProject(ScanProject project)
    {
        if (_project != null)
        {
            _project.Changed -= Project_Changed;
            _project.PageUpdated -= Project_PageUpdated;
            _ingest.ProgressChanged -= Ingest_ProgressChanged;
            _ingest.Idle -= Ingest_Idle;
            _ingest.Dispose();
        }
        _project = project;
        _project.MaxUndo = _settings.MaxUndoSteps;
        _project.Changed += Project_Changed;
        _project.PageUpdated += Project_PageUpdated;
        _ingest = new PageIngestor(_project, () => _settings, () => _lastProfileName, _osdPool, degree: Math.Clamp(Environment.ProcessorCount / 4, 1, 3));
        _ingest.ProgressChanged += Ingest_ProgressChanged;
        _ingest.Idle += Ingest_Idle;
        _ingestProgress = default;
        _thumbs.Clear();
        _thumbKeys.Clear();
        imlThumbs.Images.Clear();
        AddThumbnailPlaceholders();
        _previewKey = null;
        RefreshPageList();
    }

    private void Project_Changed(object? sender, EventArgs e) => RequestRefresh();
    private void Project_PageUpdated(PageRecord page) => RequestRefresh();

    private void Ingest_ProgressChanged(IngestProgress p)
    {
        _ingestProgress = p;
        BeginInvokeSafe(() =>
        {
            RefreshBusyUi();
            if (_work == null) SetStatus($"Đang xử lý trang {p.Done}/{p.Total}...");
        });
    }

    private void Ingest_Idle(IngestSummary s)
    {
        BeginInvokeSafe(() =>
        {
            _ingestProgress = default;
            RefreshBusyUi();
            if (_work != null || s.Total == 0) return;
            string msg = $"Đã xử lý {s.Total} trang";
            if (s.Blank > 0) msg += $", bỏ {s.Blank} trang trắng";
            if (s.Failed > 0) msg += $", {s.Failed} trang lỗi (xem log)";
            SetStatus(msg + ".");
        });
    }

    private void BeginInvokeSafe(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { /* form closing */ }
    }

    /// <summary>Progress bar / cancel link / command enablement for foreground work plus the background ingest queue.</summary>
    private void RefreshBusyUi()
    {
        bool ingesting = _ingest != null && _ingest.Pending > 0;
        if (_work == null)
        {
            progressBar.Visible = ingesting;
            if (ingesting)
            {
                progressBar.Maximum = Math.Max(1, _ingestProgress.Total);
                progressBar.Value = Math.Clamp(_ingestProgress.Done, 0, progressBar.Maximum);
            }
        }
        lnkCancel.Visible = _work != null || ingesting || _scanner?.IsScanning == true;
        UpdateCommandState();
    }

    #endregion

    #region Page list / thumbnails / preview

    // The list is virtual: it only ever materializes the items on screen, so a project with
    // hundreds of pages opens and scrolls instantly. _view is the immutable snapshot it shows.
    private IReadOnlyList<PageRecord> _view = Array.Empty<PageRecord>();
    private ThumbnailLoader _thumbs = null!;
    private readonly HashSet<string> _thumbKeys = new(); // thumbnail keys present in imlThumbs
    private int _refreshQueued;
    private bool _suppressSelection;
    private string? _previewKey;

    private void InitVirtualList()
    {
        lvPages.VirtualMode = true;
        lvPages.RetrieveVirtualItem += lvPages_RetrieveVirtualItem;
        _thumbs = new ThumbnailLoader((page, box) => _project.Cache.GetThumbnail(page, box), OnThumbnailReady, this);
        AddThumbnailPlaceholders();
    }

    private void ApplyThumbnailSize()
    {
        int w = Math.Clamp(_settings.ThumbnailSize, 60, 300);
        var size = new Size(w, (int)(w * 1.35));
        if (imlThumbs.ImageSize == size) return;
        _thumbs?.Clear();
        _thumbKeys.Clear();
        imlThumbs.Images.Clear();
        imlThumbs.ImageSize = size;
        AddThumbnailPlaceholders();
    }

    /// <summary>Stand-in images: waiting for processing, failed, thumbnail still loading.</summary>
    private void AddThumbnailPlaceholders()
    {
        Size box = imlThumbs.ImageSize;
        // The ImageList keeps a reference to what it is given (it re-reads it whenever its native
        // handle is rebuilt), so the bitmaps handed over must NOT be disposed.
        void Add(string key, Color fill, string glyph)
        {
            var bmp = new Bitmap(box.Width, box.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(fill);
                g.DrawRectangle(Pens.Silver, 0, 0, box.Width - 1, box.Height - 1);
                using var font = new Font("Segoe UI", Math.Max(10, box.Width / 6f), FontStyle.Bold);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(glyph, font, Brushes.Gray, new RectangleF(0, 0, box.Width, box.Height), sf);
            }
            imlThumbs.Images.Add(key, bmp);
        }
        Add("ph:loading", Color.FromArgb(245, 245, 245), "");
        Add("ph:pending", Color.FromArgb(235, 240, 250), "...");
        Add("ph:failed", Color.FromArgb(252, 232, 232), "!");
    }

    private const int PlaceholderLoading = 0, PlaceholderPending = 1, PlaceholderFailed = 2;

    private string ThumbKey(PageRecord p) => $"{p.ViewKey}|{imlThumbs.ImageSize.Width}x{imlThumbs.ImageSize.Height}";

    private void lvPages_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        IReadOnlyList<PageRecord> view = _view;
        if (e.ItemIndex < 0 || e.ItemIndex >= view.Count)
        {
            e.Item = new ListViewItem("");
            return;
        }
        PageRecord p = view[e.ItemIndex];
        string prefix = p.State switch { PageState.Pending => "... ", PageState.Failed => "! ", _ => "" };
        var item = new ListViewItem($"{prefix}{e.ItemIndex + 1}. {p.Label}") { Tag = p.Id };
        // Virtual items are not attached to the list yet, so ImageKey cannot resolve: use indexes.
        // The three placeholders are always the first images of the list.
        switch (p.State)
        {
            case PageState.Pending:
                item.ImageIndex = PlaceholderPending;
                break;
            case PageState.Failed:
                item.ImageIndex = PlaceholderFailed;
                item.ToolTipText = p.Error;
                break;
            default:
                string key = ThumbKey(p);
                int index = _thumbKeys.Contains(key) ? imlThumbs.Images.IndexOfKey(key) : -1;
                if (index >= 0) item.ImageIndex = index;
                else
                {
                    item.ImageIndex = PlaceholderLoading;
                    _thumbs.Request(key, p, imlThumbs.ImageSize);
                }
                break;
        }
        e.Item = item;
    }

    /// <summary>A thumbnail finished (UI thread): put it into the image list and repaint.</summary>
    private void OnThumbnailReady(string key, Bitmap bmp)
    {
        try
        {
            Size box = imlThumbs.ImageSize;
            if (key.EndsWith($"|{box.Width}x{box.Height}", StringComparison.Ordinal) && _thumbKeys.Add(key))
            {
                imlThumbs.Images.Add(key, (Image)bmp.Clone()); // the list owns this copy (see AddThumbnailPlaceholders)
                lvPages.Invalidate();
            }
        }
        finally
        {
            bmp.Dispose();
        }
    }

    /// <summary>Page list changed (possibly from a worker thread): rebuild the view once per burst.</summary>
    private void RequestRefresh()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (!InvokeRequired) { RefreshPageList(); return; }
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
            BeginInvokeSafe(() => { _refreshQueued = 0; RefreshPageList(); });
    }

    private HashSet<string> SelectedIds()
    {
        IReadOnlyList<PageRecord> view = _view;
        return lvPages.SelectedIndices.Cast<int>().Where(i => i >= 0 && i < view.Count).Select(i => view[i].Id).ToHashSet();
    }

    private void SelectByIds(HashSet<string> ids)
    {
        _suppressSelection = true;
        try
        {
            lvPages.SelectedIndices.Clear();
            for (int i = 0; i < _view.Count; i++)
                if (ids.Contains(_view[i].Id)) lvPages.SelectedIndices.Add(i);
        }
        finally { _suppressSelection = false; }
        OnSelectionChanged();
    }

    private void RefreshPageList()
    {
        HashSet<string> selected = SelectedIds();
        _view = _project.Pages;
        _suppressSelection = true;
        try
        {
            lvPages.BeginUpdate();
            lvPages.VirtualListSize = _view.Count;
            lvPages.SelectedIndices.Clear();
            for (int i = 0; i < _view.Count; i++)
                if (selected.Contains(_view[i].Id)) lvPages.SelectedIndices.Add(i);
            lvPages.EndUpdate();
        }
        finally { _suppressSelection = false; }
        lvPages.Invalidate();

        UpdatePageCountLabel();
        _bgOcr.Signal();
        Text = $"Image Optimizer Tool - {(_project.IsSession ? "(phiên chưa lưu)" : _project.Folder)}";
        PruneThumbnails();
        OnSelectionChanged();
    }

    private void UpdatePageCountLabel() => lblPageCount.Text = $"{_view.Count} trang{_ocrProgress}";

    /// <summary>Drops thumbnails of looks no page has any more (old rotations, deleted pages).</summary>
    private void PruneThumbnails()
    {
        if (_thumbKeys.Count <= _view.Count * 3 + 60) return;
        var live = _view.Select(ThumbKey).ToHashSet();
        foreach (string key in _thumbKeys.Where(k => !live.Contains(k)).ToList())
        {
            imlThumbs.Images.RemoveByKey(key);
            _thumbKeys.Remove(key);
        }
    }

    private void lvPages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (!_suppressSelection) OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        UpdateCommandState();
        List<int> sel = SelectedIndices();
        ShowPreview(sel.Count == 1 && sel[0] < _view.Count ? sel[0] : null);
    }

    private int _previewVersion;

    /// <summary>
    /// Progressive preview: the ~1600 px proxy (with the page's ops applied) appears almost at
    /// once; the full-resolution render then replaces it in place, keeping zoom and position.
    /// Every step runs off the UI thread and only the newest request may show its result.
    /// </summary>
    private async void ShowPreview(int? index)
    {
        int version = ++_previewVersion;
        if (index is not int i || i < 0 || i >= _view.Count)
        {
            SetPreviewImage(null, keepView: false);
            lblPreviewInfo.Text = "";
            _previewKey = null;
            return;
        }

        PageRecord p = _view[i];
        if (p.State != PageState.Ready)
        {
            SetPreviewImage(null, keepView: false);
            lblPreviewInfo.Text = p.State == PageState.Pending ? "Đang xử lý trang..." : "Trang lỗi: " + p.Error;
            _previewKey = null;
            return;
        }
        if (_previewKey == p.ViewKey && picPreview.Image != null) return; // already showing this look

        _previewKey = p.ViewKey;
        int pageCount = _view.Count;
        PageCache cache = _project.Cache;
        int targetDpi = _settings.LimitDpiToScanSetting ? _settings.GetTargetDpi(_lastProfileName) : 0;
        lblPreviewInfo.Text = "Đang tải...";
        try
        {
            (Bitmap proxy, string info) = await Task.Run(() =>
            {
                Bitmap bmp = cache.RenderPreview(p);
                return (bmp, DescribePage(p, i, pageCount));
            });
            if (version != _previewVersion || IsDisposed) { proxy.Dispose(); return; }
            SetPreviewImage(proxy, keepView: false);
            lblPreviewInfo.Text = info;

            await Task.Delay(120); // skip the full render when the user is just flicking through pages
            if (version != _previewVersion || IsDisposed) return;
            Bitmap full = await Task.Run(() => PageRenderer.RenderFull(p, targetDpi));
            if (version != _previewVersion || IsDisposed) { full.Dispose(); return; }
            SetPreviewImage(full, keepView: true);
        }
        catch (Exception ex)
        {
            if (version == _previewVersion) lblPreviewInfo.Text = "Không xem trước được: " + ex.Message;
        }
    }

    private void SetPreviewImage(Bitmap? next, bool keepView)
    {
        Image? old = picPreview.Image;
        if (keepView) picPreview.ReplaceImage(next); else picPreview.Image = next;
        if (!ReferenceEquals(old, next)) old?.Dispose();
    }

    /// <summary>Info line from the file header (no pixel decode), adjusted for the page's rotation.</summary>
    private static string DescribePage(PageRecord p, int index, int pageCount)
    {
        (int w, int h, int dx, int dy) = PageRenderer.ReadInfo(p.Source);
        if (p.Ops.Rotate % 180 != 0) (w, h, dx, dy) = (h, w, dy, dx);
        double wMm = w * 25.4 / dx, hMm = h * 25.4 / dy;
        return $"Trang {index + 1}/{pageCount}   {w}x{h} px   {dx}x{dy} dpi   {wMm:0}x{hMm:0} mm   {(p.Source.IsPdf ? "PDF" : Path.GetExtension(p.Source.File).TrimStart('.').ToUpperInvariant())}   {p.Label}";
    }

    private List<int> SelectedIndices() => lvPages.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();

    private void UpdateCommandState()
    {
        bool busy = _work != null;
        bool scanning = _scanner?.IsScanning == true;
        bool ingesting = _ingest != null && _ingest.Pending > 0;
        bool idle = !busy && !scanning;   // may start scanning / importing / editing
        bool quiet = idle && !ingesting;  // needs a settled page list (export, save, new / open)
        bool hasPages = _view.Count > 0;
        bool hasSel = lvPages.SelectedIndices.Count > 0;

        foreach (ToolStripItem item in new ToolStripItem[] { mnuScan, tsbScan, mnuImportImages, tsbImportImages, mnuImportPdf, tsbImportPdf, mnuSettings, tsbSettings, mnuScanProfiles })
            item.Enabled = idle;
        foreach (ToolStripItem item in new ToolStripItem[] { mnuNewProject, mnuOpenProject })
            item.Enabled = quiet;
        foreach (ToolStripItem item in new ToolStripItem[] { mnuExportPdf, tsbExportPdf, mnuExportTiff, tsbExportTiff, mnuClearAll, mnuSaveProject, mnuSaveProjectAs })
            item.Enabled = quiet && hasPages;
        foreach (ToolStripItem item in new ToolStripItem[] { mnuRotateLeft, tsbRotateLeft, mnuRotateRight, tsbRotateRight, mnuRotate180, mnuDeletePages, tsbDelete, mnuAutoProcess, mnuInsertPages })
            item.Enabled = idle && hasSel;
        mnuUndo.Enabled = tsbUndo.Enabled = idle && _project.CanUndo;
        mnuRedo.Enabled = tsbRedo.Enabled = idle && _project.CanRedo;
        mnuCancelScan.Enabled = scanning;
    }

    #endregion

    #region Drag & drop (reorder + import files)

    private void lvPages_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (_work != null || _scanner?.IsScanning == true) return;
        lvPages.DoDragDrop(SelectedIndices(), DragDropEffects.Move);
    }

    private void lvPages_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = _work == null ? DragDropEffects.Copy : DragDropEffects.None;
            return;
        }
        if (e.Data?.GetDataPresent(typeof(List<int>)) != true) { e.Effect = DragDropEffects.None; return; }

        e.Effect = DragDropEffects.Move;
        Point pt = lvPages.PointToClient(new Point(e.X, e.Y));
        int idx = lvPages.InsertionMark.NearestIndex(pt);
        if (idx >= 0)
        {
            Rectangle r = lvPages.GetItemRect(idx);
            lvPages.InsertionMark.AppearsAfterItem = pt.X > r.Left + r.Width / 2;
        }
        lvPages.InsertionMark.Index = idx;
    }

    private void lvPages_DragLeave(object? sender, EventArgs e) => lvPages.InsertionMark.Index = -1;

    private async void lvPages_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            await ImportFilesAsync(files.Where(PageImporter.IsSupported).ToArray(), insertAt: null);
            return;
        }
        if (e.Data?.GetData(typeof(List<int>)) is not List<int> moving || moving.Count == 0) return;

        int mark = lvPages.InsertionMark.Index;
        bool after = lvPages.InsertionMark.AppearsAfterItem;
        lvPages.InsertionMark.Index = -1;
        if (mark < 0) mark = _project.Pages.Count - 1;
        int target = after ? mark + 1 : mark;

        var movedIds = moving.Select(i => _project.Pages[i].Id).ToHashSet();
        _project.Execute(pages =>
        {
            var moved = moving.Select(i => pages[i]).ToList();
            int insertAt = target - moving.Count(i => i < target);
            foreach (int i in moving.OrderByDescending(i => i)) pages.RemoveAt(i);
            pages.InsertRange(Math.Clamp(insertAt, 0, pages.Count), moved);
        });
        SelectByIds(movedIds);
    }

    #endregion

    #region Background work helper

    /// <summary>Runs <paramref name="work"/> on the thread pool with progress + cancel UI.
    /// Returns default when cancelled or failed (the error is shown and logged).</summary>
    private async Task<T?> RunWorkAsync<T>(string title, Func<IProgress<WorkProgress>, CancellationToken, T> work)
    {
        var cts = _work = new CancellationTokenSource();
        progressBar.Value = 0;
        progressBar.Visible = true;
        lnkCancel.Visible = true;
        SetStatus(title + "...");
        UpdateCommandState();
        var progress = new Progress<WorkProgress>(p =>
        {
            progressBar.Maximum = Math.Max(1, p.Total);
            progressBar.Value = Math.Clamp(p.Current, 0, progressBar.Maximum);
            SetStatus(p.Message);
        });
        try
        {
            return await Task.Run(() => work(progress, cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus(title + ": đã huỷ.");
            return default;
        }
        catch (Exception ex)
        {
            Log.Error(title + " failed", ex);
            SetStatus(title + ": lỗi.");
            MessageBox.Show(this, $"{title} thất bại:\n{ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return default;
        }
        finally
        {
            _work = null;
            cts.Dispose();
            progressBar.Visible = false;
            RefreshBusyUi(); // background ingest may still be running
        }
    }

    private void lnkCancel_Click(object? sender, EventArgs e)
    {
        _work?.Cancel();
        if (_ingest.Pending > 0) _ingest.CancelPending();
        if (_scanner?.IsScanning == true) CancelScan();
    }

    #endregion

    #region Project menu

    private void mnuNewProject_Click(object? sender, EventArgs e)
    {
        if (_project.IsSession && _project.Pages.Count > 0 && MessageBox.Show(this,
                "Phiên hiện tại chưa được lưu thành dự án. Bỏ phiên này và tạo dự án mới?", "Dự án mới",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        ScanProject old = _project;
        SetProject(ScanProject.NewSession(_settings.GetWorkFolder()));
        old.DeleteIfSession();
        SetStatus("Đã tạo dự án mới.");
    }

    private void mnuOpenProject_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Title = "Mở dự án", Filter = "Dự án (project.xml)|project.xml" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ScanProject old = _project;
            SetProject(ScanProject.Open(Path.GetDirectoryName(dlg.FileName)!));
            if (old.Pages.Count == 0) old.DeleteIfSession();
            SetStatus("Đã mở dự án " + _project.Folder);
        }
        catch (Exception ex)
        {
            Log.Error("Open project failed", ex);
            MessageBox.Show(this, "Không mở được dự án:\n" + ex.Message, "Mở dự án", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void mnuSaveProject_Click(object? sender, EventArgs e)
    {
        if (_project.IsSession) { mnuSaveProjectAs_Click(sender, e); return; }
        _project.SaveAs(_project.Folder);
        SetStatus("Đã lưu dự án " + _project.Folder);
    }

    private void mnuSaveProjectAs_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Chọn (hoặc tạo) thư mục để lưu dự án", ShowNewFolderButton = true, UseDescriptionForTitle = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string oldFolder = _project.Folder;
            bool wasSession = _project.IsSession;
            _project.SaveAs(dlg.SelectedPath);
            if (wasSession) TryDeleteFolder(oldFolder);
            SetStatus("Đã lưu dự án vào " + _project.Folder);
        }
        catch (Exception ex)
        {
            Log.Error("Save project failed", ex);
            MessageBox.Show(this, "Không lưu được dự án:\n" + ex.Message, "Lưu dự án", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void mnuExit_Click(object? sender, EventArgs e) => Close();

    #endregion

    #region Import

    private async void mnuImportImages_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import ảnh",
            Multiselect = true,
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.gif)|*.jpg;*.jpeg;*.jpe;*.png;*.bmp;*.tif;*.tiff;*.gif|Tất cả (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) await ImportFilesAsync(dlg.FileNames, insertAt: null);
    }

    private async void mnuImportPdf_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Title = "Import PDF", Multiselect = true, Filter = "PDF (*.pdf)|*.pdf" };
        if (dlg.ShowDialog(this) == DialogResult.OK) await ImportFilesAsync(dlg.FileNames, insertAt: null);
    }

    private async void mnuInsertPages_Click(object? sender, EventArgs e)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0) return;
        using var dlg = new OpenFileDialog
        {
            Title = "Chèn trang",
            Multiselect = true,
            Filter = "Ảnh / PDF|*.jpg;*.jpeg;*.jpe;*.png;*.bmp;*.tif;*.tiff;*.gif;*.pdf|Tất cả (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) await ImportFilesAsync(dlg.FileNames, insertAt: sel[0]);
    }

    private async Task ImportFilesAsync(string[] files, int? insertAt)
    {
        if (files.Length == 0 || _work != null) return;
        PageIngestor ingest = _ingest;
        // Reading (PDF rendering, file copies) is the only foreground step: pages show up in the
        // list as placeholders while it runs and are processed in the background afterwards.
        int? added = await RunWorkAsync<int?>("Import", (progress, cancel) =>
        {
            try { return ingest.Import(files, insertAt, progress, cancel); }
            catch (OperationCanceledException) { ingest.CancelPending(); throw; }
        });
        if (added is int n && n > 0) SetStatus($"Đã thêm {n} trang, đang xử lý nền...");
    }

    #endregion

    #region Scan

    private void EnsureScanner() => _scanner ??= new ScannerService();

    private void mnuScan_Click(object? sender, EventArgs e)
    {
        if (_work != null) return;
        EnsureScanner();
        Cursor = Cursors.WaitCursor;
        IReadOnlyList<ScanSource> sources;
        try { sources = _scanner!.GetSources(_settings.ScanDriver); }
        finally { Cursor = Cursors.Default; }

        using var dlg = new ScanOptionsForm(_settings, sources, forScan: true);
        DialogResult result = dlg.ShowDialog(this);
        if (dlg.ProfilesChanged) SaveSettings();
        if (result != DialogResult.OK || dlg.SelectedSource == null || dlg.SelectedProfile == null) return;

        StartScan(dlg.SelectedSource, dlg.SelectedProfile);
    }

    private void StartScan(ScanSource source, ScanProfile profile)
    {
        _lastProfileName = profile.Name;
        string incoming = Path.Combine(_project.PagesFolder, "_incoming");
        int scanned = 0;
        var errors = new List<string>();

        lnkCancel.Visible = true;
        SetStatus($"Đang scan từ {source.Name}...");
        try
        {
            _scanner!.StartScan(source, profile, incoming,
                onPageScanned: path =>
                {
                    scanned++;
                    SetStatus($"Đã nhận trang {scanned} từ máy scan...");
                    // The scanner keeps feeding: the page shows up at once and is processed in the background.
                    _ingest.AddScanned(path, $"Scan {DateTime.Now:HH:mm:ss} #{scanned}");
                },
                onError: ex =>
                {
                    errors.Add(ex.Message);
                    string hint = ex is ScanException se && se.Kind is ScanErrorKind.PaperJam or ScanErrorKind.DoubleFeed
                        ? $"\n\nCác trang đã scan ({scanned}) được giữ lại. Xử lý xong, bấm Scan để scan tiếp."
                        : "";
                    MessageBox.Show(this, ex.Message + hint, "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                },
                onFinished: () =>
                {
                    RefreshBusyUi();
                    SetStatus($"Scan xong: {scanned} trang" + (errors.Count > 0 ? " (có lỗi, xem log)" : "") + ".");
                });
        }
        catch (Exception ex)
        {
            Log.Error("Starting scan failed", ex);
            MessageBox.Show(this, "Không bắt đầu scan được:\n" + ex.Message, "Scan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            lnkCancel.Visible = false;
        }
        UpdateCommandState();
    }

    private void mnuCancelScan_Click(object? sender, EventArgs e) => CancelScan();

    private void CancelScan()
    {
        _scanner?.Cancel();
        SetStatus("Đang huỷ scan...");
    }

    #endregion

    #region Page edits

    private void mnuUndo_Click(object? sender, EventArgs e) { if (_work == null) _project.Undo(); }
    private void mnuRedo_Click(object? sender, EventArgs e) { if (_work == null) _project.Redo(); }

    private void mnuRotateLeft_Click(object? sender, EventArgs e) => RotateSelected(270);
    private void mnuRotateRight_Click(object? sender, EventArgs e) => RotateSelected(90);
    private void mnuRotate180_Click(object? sender, EventArgs e) => RotateSelected(180);

    /// <summary>Rotation is an op on the page (no pixels are rewritten), so it is instant
    /// however many pages are selected or how large they are.</summary>
    private void RotateSelected(int degrees)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0 || _work != null) return;
        var ids = sel.Select(i => _project.Pages[i].Id).ToHashSet();
        _project.Execute(pages =>
        {
            foreach (int i in sel)
                if (pages[i].State == PageState.Ready)
                    pages[i] = pages[i] with { Ops = pages[i].Ops.RotatedBy(degrees) };
        });
        SelectByIds(ids);
    }

    private async void mnuAutoProcess_Click(object? sender, EventArgs e)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0 || _work != null) return;
        var targets = sel.Select(i => _project.Pages[i]).Where(p => p.State == PageState.Ready).ToList();
        if (targets.Count == 0) return;
        var options = PageProcessingOptions.FromSettings(_settings);
        options.DetectBlank = false;
        PageCache cache = _project.Cache;
        List<PageRecord>? done = await RunWorkAsync("Xử lý trang", (progress, cancel) =>
        {
            var list = new List<PageRecord>();
            OcrEngine? osd = _settings.AutoOrient ? _osdPool.Rent() : null;
            try
            {
                for (int k = 0; k < targets.Count; k++)
                {
                    cancel.ThrowIfCancellationRequested();
                    progress.Report(new WorkProgress(k + 1, targets.Count, $"Xử lý trang {k + 1}/{targets.Count}"));
                    // The analysis is recorded as ops on the page; no pixel is rewritten.
                    AnalysisResult r = PageAnalysis.Analyze(cache, targets[k].Source, options, osd);
                    list.Add(r.Ops == targets[k].Ops ? targets[k]
                        : targets[k] with { Ops = r.Ops, Label = r.Summary.Length > 0 ? $"{targets[k].Label} ({r.Summary})" : targets[k].Label });
                }
            }
            finally { if (osd != null) _osdPool.Return(osd); }
            return list;
        });
        if (done == null) return;
        var byId = done.ToDictionary(r => r.Id);
        _project.Execute(pages =>
        {
            for (int i = 0; i < pages.Count; i++)
                if (byId.TryGetValue(pages[i].Id, out PageRecord? r)) pages[i] = r;
        });
        SelectByIds(byId.Keys.ToHashSet());
        SetStatus($"Đã xử lý {done.Count} trang.");
    }

    private void mnuDeletePages_Click(object? sender, EventArgs e)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0 || _work != null) return;
        _project.Execute(pages => { foreach (int i in sel.OrderByDescending(i => i)) pages.RemoveAt(i); });
        SetStatus($"Đã xoá {sel.Count} trang (Ctrl+Z để hoàn tác).");
    }

    private void mnuClearAll_Click(object? sender, EventArgs e)
    {
        if (_project.Pages.Count == 0 || _work != null) return;
        _project.Execute(pages => pages.Clear());
        SetStatus("Đã xoá tất cả trang (Ctrl+Z để hoàn tác).");
    }

    #endregion

    #region Export

    private async void mnuExportPdf_Click(object? sender, EventArgs e) => await ExportAsync(ExportFormat.Pdf);
    private async void mnuExportTiff_Click(object? sender, EventArgs e) => await ExportAsync(ExportFormat.Tiff);

    private async Task ExportAsync(ExportFormat format)
    {
        if (_project.Pages.Count == 0 || _work != null) return;
        if (_ingest.Pending > 0)
        {
            MessageBox.Show(this, $"Còn {_ingest.Pending} trang đang được xử lý. Vui lòng đợi xong rồi xuất.", "Xuất file", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string ext = format == ExportFormat.Pdf ? ".pdf" : ".tif";
        if (!string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            try { Directory.CreateDirectory(_settings.OutputFolder); } catch { /* falls back to Documents below */ }
        }
        string folder = string.IsNullOrWhiteSpace(_settings.OutputFolder) || !Directory.Exists(_settings.OutputFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _settings.OutputFolder;
        string? explicitPath = null;

        if (_settings.SplitMode == DocumentSplitMode.None)
        {
            using var dlg = new SaveFileDialog
            {
                Title = format == ExportFormat.Pdf ? "Xuất PDF" : "Xuất TIFF",
                Filter = format == ExportFormat.Pdf ? "PDF (*.pdf)|*.pdf" : "TIFF (*.tif)|*.tif",
                InitialDirectory = folder,
                FileName = FileNamer.Build(_settings.FileNamePattern, DateTime.Now, 1, null, _lastProfileName) + ext,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            explicitPath = dlg.FileName;
            folder = Path.GetDirectoryName(dlg.FileName)!;
        }
        else
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = $"Tách tài liệu theo {(_settings.SplitMode == DocumentSplitMode.Barcode ? "barcode" : "trang trắng")}: chọn thư mục lưu các file",
                UseDescriptionForTitle = true,
                SelectedPath = folder,
                ShowNewFolderButton = true,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            folder = dlg.SelectedPath;
        }

        var pages = _project.Pages.ToList();
        int failed = pages.Count(p => p.State == PageState.Failed);
        if (failed > 0)
            SetStatus($"Bỏ qua {failed} trang lỗi khi xuất.");
        AppSettings settings = _settings.Clone();
        string? profile = _lastProfileName;
        var sw = Stopwatch.StartNew();
        OcrCache ocrCache = _project.OcrCache; // pages read in the background are not read again
        _bgOcr.Pause();
        List<string>? outputs = await RunWorkAsync(format == ExportFormat.Pdf ? "Xuất PDF" : "Xuất TIFF",
            (progress, cancel) => BatchExporter.Export(pages, settings, format, folder, explicitPath, profile, progress, cancel, ocrCache));
        _bgOcr.Resume();
        if (outputs == null || outputs.Count == 0) return;

        long bytes = outputs.Sum(f => new FileInfo(f).Length);
        string codecs = format == ExportFormat.Pdf
            ? $"{(settings.UseJBig2 ? "JBIG2" : "CCITT G4")} / {(settings.UseJpeg2000 ? "JPEG2000" : "JPEG")}{(settings.PdfA ? ", PDF/A-2b" : "")}{(settings.Ocr ? ", OCR" : "")}"
            : $"{(settings.UseJBig2 ? "JBIG2" : "CCITT G4")} / {(settings.UseJpeg2000 ? "JPEG2000" : "JPEG")}";
        SetStatus($"Đã xuất {outputs.Count} file ({bytes / 1024.0:0.0} KB, {codecs}) trong {sw.Elapsed.TotalSeconds:0.0}s: {outputs[0]}{(outputs.Count > 1 ? " ..." : "")}");
        if (MessageBox.Show(this, $"Đã xuất {outputs.Count} file ({bytes / 1024.0:0.0} KB).\n\nMở thư mục chứa file?", "Xuất file",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{outputs[0]}\"") { UseShellExecute = true });
        }
    }

    #endregion

    #region Settings / help

    private void mnuSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _settings = dlg.Result;
        SaveSettings();
        _project.MaxUndo = _settings.MaxUndoSteps;
        ApplyThumbnailSize();
        RefreshPageList();
        SetStatus("Đã lưu cài đặt.");
    }

    private void mnuScanProfiles_Click(object? sender, EventArgs e)
    {
        using var dlg = new ScanOptionsForm(_settings, Array.Empty<ScanSource>(), forScan: false);
        dlg.ShowDialog(this);
        if (dlg.ProfilesChanged) SaveSettings();
    }

    private void SaveSettings()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            Log.Error("Saving settings failed", ex);
            MessageBox.Show(this, "Không lưu được cài đặt:\n" + ex.Message, "Cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void mnuOpenLogFolder_Click(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.LogFolder}\"") { UseShellExecute = true });
    }

    private void mnuAbout_Click(object? sender, EventArgs e)
    {
        string notices = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString() ?? "";
        if (MessageBox.Show(this,
                $"Image Optimizer Tool {version}\n\nScan / import tài liệu, xử lý ảnh, OCR, xuất PDF/A và TIFF.\n\n" +
                $"Cài đặt: {AppPaths.SettingsFile}\nLog: {AppPaths.LogFolder}\n\nMở danh sách license thư viện bên thứ ba?",
                "Giới thiệu", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes && File.Exists(notices))
        {
            Process.Start(new ProcessStartInfo(notices) { UseShellExecute = true });
        }
    }

    #endregion

    private void SetStatus(string text) => lblStatus.Text = text;

    private static void TryDeleteFolder(string folder)
    {
        try { Directory.Delete(folder, true); } catch (Exception ex) { Log.Warn("Could not delete " + folder, ex); }
    }

    private void CleanOldEmptySessions(string work)
    {
        try
        {
            foreach (string d in Directory.EnumerateDirectories(work, "session_*"))
            {
                if (string.Equals(Path.GetFullPath(d), Path.GetFullPath(_project.Folder), StringComparison.OrdinalIgnoreCase)) continue;
                string pages = Path.Combine(d, "pages");
                if (!Directory.Exists(pages) || !Directory.EnumerateFiles(pages, "*", SearchOption.AllDirectories).Any())
                    TryDeleteFolder(d);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Cleaning old sessions failed", ex);
        }
    }
}
