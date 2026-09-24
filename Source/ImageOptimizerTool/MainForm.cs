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
    private Task _scanTail = Task.CompletedTask;
    private string? _lastProfileName;

    // Tesseract OSD engine for auto-orientation, created on first use, shared by the
    // background processing tasks (serialized with _osdLock -- Tesseract is not thread-safe).
    private OcrEngine? _osd;
    private readonly object _osdLock = new();

    private readonly Dictionary<string, Image> _thumbCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _thumbWork;

    private readonly string[] _startupFiles;

    public MainForm(AppSettings settings, string[]? startupFiles = null)
    {
        _settings = settings;
        _startupFiles = startupFiles ?? Array.Empty<string>();
        InitializeComponent();
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
        _thumbWork?.Cancel();
        _scanner?.Dispose();
        lock (_osdLock) _osd?.Dispose();
        // An empty session leaves nothing behind; a non-empty one is offered for restore next time.
        if (_project.IsSession && _project.Pages.Count == 0) _project.DeleteIfSession();
        Log.Info("App closed");
    }

    private void SetProject(ScanProject project)
    {
        if (_project != null) _project.Changed -= Project_Changed;
        _project = project;
        _project.MaxUndo = _settings.MaxUndoSteps;
        _project.Changed += Project_Changed;
        foreach (Image img in _thumbCache.Values) img.Dispose();
        _thumbCache.Clear();
        imlThumbs.Images.Clear();
        RefreshPageList();
    }

    private void Project_Changed(object? sender, EventArgs e) => RefreshPageList();

    #endregion

    #region Page list / thumbnails / preview

    private void ApplyThumbnailSize()
    {
        int w = Math.Clamp(_settings.ThumbnailSize, 60, 300);
        var size = new Size(w, (int)(w * 1.35));
        if (imlThumbs.ImageSize == size) return;
        foreach (Image img in _thumbCache.Values) img.Dispose();
        _thumbCache.Clear();
        imlThumbs.Images.Clear();
        imlThumbs.ImageSize = size;
    }

    private void RefreshPageList()
    {
        var selectedFiles = lvPages.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lvPages.BeginUpdate();
        lvPages.Items.Clear();
        for (int i = 0; i < _project.Pages.Count; i++)
        {
            PageRecord p = _project.Pages[i];
            var item = new ListViewItem($"{i + 1}. {p.Label}") { Tag = p.FilePath, ImageKey = p.FilePath };
            if (selectedFiles.Contains(p.FilePath)) item.Selected = true;
            lvPages.Items.Add(item);
        }
        lvPages.EndUpdate();

        lblPageCount.Text = $"{_project.Pages.Count} trang";
        Text = $"Image Optimizer Tool - {(_project.IsSession ? "(phiên chưa lưu)" : _project.Folder)}";
        UpdateCommandState();
        GenerateMissingThumbnails();
        if (lvPages.SelectedItems.Count == 0) ShowPreview(null);
    }

    private void GenerateMissingThumbnails()
    {
        var missing = _project.Pages.Select(p => p.FilePath).Where(f => !_thumbCache.ContainsKey(f)).Distinct().ToList();
        if (missing.Count == 0) return;

        _thumbWork?.Cancel();
        var cts = _thumbWork = new CancellationTokenSource();
        Size box = imlThumbs.ImageSize;
        Task.Run(() =>
        {
            foreach (string f in missing)
            {
                if (cts.IsCancellationRequested) return;
                Bitmap thumb;
                try
                {
                    using Bitmap src = ImageUtils.Load(f);
                    thumb = ImageUtils.MakeThumbnail(src, box.Width, box.Height);
                }
                catch (Exception ex)
                {
                    Log.Warn("Thumbnail failed: " + f, ex);
                    continue;
                }
                BeginInvoke(() =>
                {
                    if (cts.IsCancellationRequested || _thumbCache.ContainsKey(f) || imlThumbs.ImageSize != box) { thumb.Dispose(); return; }
                    _thumbCache[f] = thumb;
                    imlThumbs.Images.Add(f, thumb);
                    lvPages.Invalidate();
                });
            }
        });
    }

    private void lvPages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateCommandState();
        ShowPreview(lvPages.SelectedIndices.Count == 1 ? lvPages.SelectedIndices[0] : null);
    }

    private void ShowPreview(int? index)
    {
        Image? old = picPreview.Image;
        picPreview.Image = null;
        old?.Dispose();
        lblPreviewInfo.Text = "";
        if (index is not int i || i < 0 || i >= _project.Pages.Count) return;

        PageRecord p = _project.Pages[i];
        try
        {
            Bitmap bmp = ImageUtils.Load(p.FilePath);
            (int dx, int dy) = ImageUtils.ResolveDpiXY(bmp);
            picPreview.Image = bmp;
            double wMm = bmp.Width * 25.4 / dx, hMm = bmp.Height * 25.4 / dy;
            lblPreviewInfo.Text = $"Trang {i + 1}/{_project.Pages.Count}   {bmp.Width}x{bmp.Height} px   {dx}x{dy} dpi   {wMm:0}x{hMm:0} mm   {Path.GetExtension(p.FilePath).TrimStart('.').ToUpperInvariant()}   {p.Label}";
        }
        catch (Exception ex)
        {
            lblPreviewInfo.Text = "Không xem trước được: " + ex.Message;
        }
    }

    private List<int> SelectedIndices() => lvPages.SelectedIndices.Cast<int>().OrderBy(i => i).ToList();

    private void UpdateCommandState()
    {
        bool busy = _work != null;
        bool scanning = _scanner?.IsScanning == true;
        bool idle = !busy && !scanning;
        bool hasPages = _project.Pages.Count > 0;
        bool hasSel = lvPages.SelectedIndices.Count > 0;

        foreach (ToolStripItem item in new ToolStripItem[] { mnuScan, tsbScan, mnuImportImages, tsbImportImages, mnuImportPdf, tsbImportPdf, mnuNewProject, mnuOpenProject, mnuSettings, tsbSettings, mnuScanProfiles })
            item.Enabled = idle;
        foreach (ToolStripItem item in new ToolStripItem[] { mnuExportPdf, tsbExportPdf, mnuExportTiff, tsbExportTiff, mnuClearAll, mnuSaveProject, mnuSaveProjectAs })
            item.Enabled = idle && hasPages;
        foreach (ToolStripItem item in new ToolStripItem[] { mnuRotateLeft, tsbRotateLeft, mnuRotateRight, tsbRotateRight, mnuRotate180, mnuDeletePages, tsbDelete, mnuAutoProcess, mnuInsertPages })
            item.Enabled = idle && hasSel;
        mnuUndo.Enabled = tsbUndo.Enabled = idle && _project.CanUndo;
        mnuRedo.Enabled = tsbRedo.Enabled = idle && _project.CanRedo;
        mnuCancelScan.Enabled = scanning;
        mnuSaveProject.Enabled = idle && hasPages;
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

        var files2 = moving.Select(i => _project.Pages[i].FilePath).ToHashSet();
        _project.Execute(pages =>
        {
            var moved = moving.Select(i => pages[i]).ToList();
            int insertAt = target - moving.Count(i => i < target);
            foreach (int i in moving.OrderByDescending(i => i)) pages.RemoveAt(i);
            pages.InsertRange(Math.Clamp(insertAt, 0, pages.Count), moved);
        });
        foreach (ListViewItem item in lvPages.Items) item.Selected = files2.Contains((string)item.Tag!);
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
            lnkCancel.Visible = _scanner?.IsScanning == true;
            UpdateCommandState();
        }
    }

    private void lnkCancel_Click(object? sender, EventArgs e)
    {
        _work?.Cancel();
        if (_scanner?.IsScanning == true) CancelScan();
    }

    private OcrEngine? GetOsd()
    {
        if (!_settings.AutoOrient) return null;
        lock (_osdLock)
        {
            if (_osd != null) return _osd;
            try { _osd = new OcrEngine(loadOcr: false); }
            catch (Exception ex) { Log.Warn("OSD engine unavailable; auto-orient disabled for this session", ex); }
            return _osd;
        }
    }

    /// <summary>Auto-processing of one new page (background thread). Returns null when the
    /// page is blank and blank removal is on.</summary>
    private PageRecord? ProcessNewPage(string file, string label)
    {
        if (!_settings.AutoProcessOnImport)
            return new PageRecord(file, label);
        var options = PageProcessingOptions.FromSettings(_settings);
        OcrEngine? osd = GetOsd();
        PageProcessResult r;
        lock (_osdLock) r = PageProcessor.Process(file, options, osd, _project.PagesFolder);
        if (r.IsBlank)
        {
            Log.Info($"Blank page skipped: {label}");
            return null;
        }
        return new PageRecord(r.OutputPath, r.Summary.Length > 0 ? $"{label} ({r.Summary})" : label);
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
        List<PageRecord>? added = await RunWorkAsync("Import", (progress, cancel) =>
        {
            var result = new List<PageRecord>();
            int skipped = 0;
            for (int f = 0; f < files.Length; f++)
            {
                cancel.ThrowIfCancellationRequested();
                progress.Report(new WorkProgress(f, files.Length, $"Đọc {Path.GetFileName(files[f])}..."));
                List<string> pages = PageImporter.Import(files[f], _project.PagesFolder);
                for (int i = 0; i < pages.Count; i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    string label = pages.Count > 1 ? $"{Path.GetFileName(files[f])} #{i + 1}" : Path.GetFileName(files[f]);
                    progress.Report(new WorkProgress(f, files.Length, $"Xử lý {label}..."));
                    PageRecord? rec = ProcessNewPage(pages[i], label);
                    if (rec == null) skipped++;
                    else result.Add(rec);
                }
            }
            if (skipped > 0) Log.Info($"Import: {skipped} blank page(s) removed");
            return result;
        });
        if (added == null || added.Count == 0) return;

        _project.Execute(pages => pages.InsertRange(insertAt is int at ? Math.Clamp(at, 0, pages.Count) : pages.Count, added));
        SetStatus($"Đã import {added.Count} trang.");
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
        int scanned = 0, blank = 0;
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
                    _scanTail = AddScannedPageAsync(_scanTail, path, $"Scan {DateTime.Now:HH:mm:ss} #{scanned}", () => blank++);
                },
                onError: ex =>
                {
                    errors.Add(ex.Message);
                    string hint = ex is ScanException se && se.Kind is ScanErrorKind.PaperJam or ScanErrorKind.DoubleFeed
                        ? $"\n\nCác trang đã scan ({scanned}) được giữ lại. Xử lý xong, bấm Scan để scan tiếp."
                        : "";
                    MessageBox.Show(this, ex.Message + hint, "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                },
                onFinished: async () =>
                {
                    await _scanTail;
                    lnkCancel.Visible = _work != null;
                    UpdateCommandState();
                    SetStatus($"Scan xong: {scanned} trang" + (blank > 0 ? $", bỏ {blank} trang trắng" : "") + (errors.Count > 0 ? " (có lỗi, xem log)" : "") + ".");
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

    /// <summary>Processes scanned pages strictly in arrival order (each waits for the previous)
    /// while the scanner keeps feeding.</summary>
    private async Task AddScannedPageAsync(Task previous, string path, string label, Action onBlank)
    {
        await previous;
        try
        {
            PageRecord? rec = await Task.Run(() => ProcessNewPage(path, label));
            if (rec == null) { onBlank(); return; }
            // Move out of _incoming so it is a regular project page file.
            string final = Path.Combine(_project.PagesFolder, Path.GetFileName(rec.FilePath));
            if (!string.Equals(Path.GetFullPath(rec.FilePath), Path.GetFullPath(final), StringComparison.OrdinalIgnoreCase))
            {
                File.Move(rec.FilePath, final, overwrite: true);
                rec = rec with { FilePath = final };
            }
            _project.Execute(pages => pages.Add(rec));
        }
        catch (Exception ex)
        {
            Log.Error("Processing scanned page failed: " + path, ex);
            _project.Execute(pages => pages.Add(new PageRecord(path, label)));
        }
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

    private async void mnuRotateLeft_Click(object? sender, EventArgs e) => await RotateSelectedAsync(270);
    private async void mnuRotateRight_Click(object? sender, EventArgs e) => await RotateSelectedAsync(90);
    private async void mnuRotate180_Click(object? sender, EventArgs e) => await RotateSelectedAsync(180);

    private async Task RotateSelectedAsync(int degrees)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0 || _work != null) return;
        var targets = sel.Select(i => _project.Pages[i]).ToList();
        List<PageRecord>? rotated = await RunWorkAsync("Xoay trang", (progress, cancel) =>
        {
            var list = new List<PageRecord>();
            for (int k = 0; k < targets.Count; k++)
            {
                cancel.ThrowIfCancellationRequested();
                progress.Report(new WorkProgress(k + 1, targets.Count, $"Xoay trang {k + 1}/{targets.Count}"));
                list.Add(targets[k] with { FilePath = PageProcessor.RotateFile(targets[k].FilePath, degrees, _project.PagesFolder) });
            }
            return list;
        });
        if (rotated == null) return;
        ReplacePages(sel, rotated);
    }

    private async void mnuAutoProcess_Click(object? sender, EventArgs e)
    {
        List<int> sel = SelectedIndices();
        if (sel.Count == 0 || _work != null) return;
        var targets = sel.Select(i => _project.Pages[i]).ToList();
        var options = PageProcessingOptions.FromSettings(_settings);
        options.DetectBlank = false;
        List<PageRecord>? done = await RunWorkAsync("Xử lý trang", (progress, cancel) =>
        {
            var list = new List<PageRecord>();
            OcrEngine? osd = GetOsd();
            for (int k = 0; k < targets.Count; k++)
            {
                cancel.ThrowIfCancellationRequested();
                progress.Report(new WorkProgress(k + 1, targets.Count, $"Xử lý trang {k + 1}/{targets.Count}"));
                PageProcessResult r;
                lock (_osdLock) r = PageProcessor.Process(targets[k].FilePath, options, osd, _project.PagesFolder);
                list.Add(r.Changed ? new PageRecord(r.OutputPath, $"{targets[k].Label} ({r.Summary})") : targets[k]);
            }
            return list;
        });
        if (done == null) return;
        ReplacePages(sel, done);
        SetStatus($"Đã xử lý {done.Count} trang.");
    }

    private void ReplacePages(List<int> indices, List<PageRecord> replacements)
    {
        _project.Execute(pages =>
        {
            for (int k = 0; k < indices.Count; k++) pages[indices[k]] = replacements[k];
        });
        var files = replacements.Select(r => r.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ListViewItem item in lvPages.Items) item.Selected = files.Contains((string)item.Tag!);
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

        string ext = format == ExportFormat.Pdf ? ".pdf" : ".tif";
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

        var pages = _project.Pages.Select(p => p.FilePath).ToList();
        AppSettings settings = _settings.Clone();
        string? profile = _lastProfileName;
        var sw = Stopwatch.StartNew();
        List<string>? outputs = await RunWorkAsync(format == ExportFormat.Pdf ? "Xuất PDF" : "Xuất TIFF",
            (progress, cancel) => BatchExporter.Export(pages, settings, format, folder, explicitPath, profile, progress, cancel));
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
