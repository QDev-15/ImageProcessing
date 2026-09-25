using System.Drawing;
using System.Xml.Serialization;

namespace ImageCoreService;

/// <summary>
/// The page list being worked on, persisted as a project folder:
///   &lt;folder&gt;\project.xml   page order, labels, sources and ops (paths relative to the folder)
///   &lt;folder&gt;\pages\...     source files (imported originals, scans, PDFs), never modified
///   &lt;folder&gt;\cache\...     thumbnails / previews / OCR; safe to delete, rebuilt on demand
/// An unsaved session is just a project in the work folder that is re-saved after every
/// change, so a crash loses nothing (see <see cref="FindLatestSession"/>).
///
/// Undo / redo keep snapshots of the page list. Source files are never deleted while any
/// snapshot can still reference them (<see cref="CleanUnreferencedFiles"/>).
///
/// Threading: mutations lock, and every change swaps in a new list (copy-on-write), so the UI
/// thread may keep enumerating <see cref="Pages"/> while background workers call
/// <see cref="Update"/> / <see cref="Discard"/>. Events can fire on the calling thread.
/// </summary>
public sealed class ScanProject
{
    public const string ProjectFileName = "project.xml";
    public const int FormatVersion = 2;

    private readonly object _gate = new();
    private readonly List<List<PageRecord>> _undo = new();
    private readonly List<List<PageRecord>> _redo = new();
    private Timer? _persistTimer;

    public string Folder { get; private set; }
    public bool IsSession { get; private set; }
    public List<PageRecord> Pages { get; private set; } = new();
    public int MaxUndo { get; set; } = 50;

    public string PagesFolder => Path.Combine(Folder, "pages");
    public string CacheFolder => Path.Combine(Folder, "cache");

    private PageCache? _cache;

    /// <summary>OCR words per page (see <see cref="BackgroundOcr"/>).</summary>
    public OcrCache OcrCache => new(Path.Combine(CacheFolder, "ocr"));

    /// <summary>Proxies / thumbnails of this project (follows the folder across "Save as").</summary>
    public PageCache Cache
    {
        get
        {
            lock (_gate)
                return _cache is { } c && string.Equals(c.Folder, CacheFolder, StringComparison.OrdinalIgnoreCase)
                    ? c
                    : _cache = new PageCache(CacheFolder);
        }
    }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>The page list changed structurally (add / remove / reorder / undo).</summary>
    public event EventHandler? Changed;

    /// <summary>One page's record was replaced in place (background work finished, ops changed
    /// silently); the page list itself is otherwise the same. Raised on the calling thread.</summary>
    public event Action<PageRecord>? PageUpdated;

    private ScanProject(string folder, bool isSession)
    {
        Folder = folder;
        IsSession = isSession;
        Directory.CreateDirectory(PagesFolder);
    }

    public static ScanProject NewSession(string workFolder)
    {
        string folder = Path.Combine(workFolder, "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        var p = new ScanProject(folder, isSession: true);
        p.Persist();
        return p;
    }

    /// <summary>Most recent session in the work folder that still has pages (crash recovery).</summary>
    public static string? FindLatestSession(string workFolder)
    {
        if (!Directory.Exists(workFolder)) return null;
        return Directory.EnumerateDirectories(workFolder, "session_*")
            .Where(d => File.Exists(Path.Combine(d, ProjectFileName)) && Directory.EnumerateFiles(Path.Combine(d, "pages")).Any())
            .OrderByDescending(d => d)
            .FirstOrDefault();
    }

    /// <summary>
    /// Opens a project. A version-1 file (page = image file + label) is upgraded: the original
    /// project.xml is kept as project.xml.v1.bak, every page becomes a raster source with no
    /// ops (pixels untouched), and the file is rewritten as version 2.
    /// </summary>
    public static ScanProject Open(string folder)
    {
        string file = Path.Combine(folder, ProjectFileName);
        ProjectDto dto;
        using (FileStream fs = File.OpenRead(file))
            dto = (ProjectDto)(new XmlSerializer(typeof(ProjectDto)).Deserialize(fs) ?? throw new InvalidDataException("Empty project file."));

        bool upgrade = dto.Version < FormatVersion;
        if (upgrade)
        {
            string bak = file + ".v1.bak";
            if (!File.Exists(bak)) File.Copy(file, bak);
            Log.Info($"Project {folder}: upgrading format v{dto.Version} -> v{FormatVersion} (backup {Path.GetFileName(bak)})");
        }

        var p = new ScanProject(folder, isSession: Path.GetFileName(folder).StartsWith("session_", StringComparison.Ordinal));
        foreach (PageDto page in dto.Pages)
        {
            string path = Path.GetFullPath(Path.Combine(folder, page.File));
            if (!File.Exists(path))
            {
                Log.Warn("Project page file missing, skipped: " + path);
                continue;
            }
            PageState state = Enum.TryParse(page.State, out PageState s) ? s : PageState.Ready;
            string? error = string.IsNullOrEmpty(page.Error) ? null : page.Error;
            if (state == PageState.Pending) { state = PageState.Failed; error = "Xử lý bị gián đoạn."; }
            p.Pages.Add(new PageRecord(
                string.IsNullOrEmpty(page.Id) ? PageRecord.NewId() : page.Id,
                new PageSource(path, page.PdfPage, page.NativeDpi),
                new PageOps(page.Rotate, page.Deskew, PageOps.ParseCrop(page.Crop)),
                state, page.Label, error));
        }
        if (upgrade) p.Persist();
        return p;
    }

    /// <summary>Applies a change to the page list as one undoable step.</summary>
    public void Execute(Action<List<PageRecord>> change)
    {
        lock (_gate)
        {
            var before = Pages;
            var after = Pages.ToList();
            change(after);
            if (after.SequenceEqual(before)) return;

            _undo.Add(before);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            _redo.Clear();
            Pages = after;
        }
        OnChanged();
    }

    public void Undo()
    {
        lock (_gate)
        {
            if (!CanUndo) return;
            _redo.Add(Pages);
            Pages = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
        }
        OnChanged();
    }

    public void Redo()
    {
        lock (_gate)
        {
            if (!CanRedo) return;
            _undo.Add(Pages);
            Pages = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
        }
        OnChanged();
    }

    /// <summary>Inserts a page at <paramref name="index"/> (end when null). <paramref name="undoable"/>
    /// false adds it without an undo step -- for the 2nd..nth page of one import, so a single
    /// Undo removes the whole import.</summary>
    public void AddPage(PageRecord page, int? index, bool undoable)
    {
        lock (_gate)
        {
            var after = Pages.ToList();
            after.Insert(Math.Clamp(index ?? after.Count, 0, after.Count), page);
            if (undoable)
            {
                _undo.Add(Pages);
                if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
                _redo.Clear();
            }
            Pages = after;
        }
        OnChanged();
    }

    /// <summary>Batch form of <see cref="AddPage"/>: one change (one undo step, one save, one
    /// notification) however many pages -- a 300-page PDF is added in one go.</summary>
    public void AddPages(IReadOnlyList<PageRecord> pages, int? index, bool undoable)
    {
        if (pages.Count == 0) return;
        lock (_gate)
        {
            var after = Pages.ToList();
            after.InsertRange(Math.Clamp(index ?? after.Count, 0, after.Count), pages);
            if (undoable)
            {
                _undo.Add(Pages);
                if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
                _redo.Clear();
            }
            Pages = after;
        }
        OnChanged();
    }

    public PageRecord? Find(string id)
    {
        lock (_gate) return Pages.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Replaces a page (matched by Id) everywhere it appears -- the current list AND every
    /// undo / redo snapshot -- WITHOUT creating an undo step. For results that arrive later
    /// (a background ingest finishing), so undoing never brings back a half-processed page.
    /// Returns false when the page no longer exists.
    /// </summary>
    public bool Update(string id, Func<PageRecord, PageRecord> change)
    {
        PageRecord? updated = null;
        lock (_gate)
        {
            PageRecord? current = Pages.FirstOrDefault(p => p.Id == id);
            if (current == null) return false;
            updated = change(current);
            Pages = Swap(Pages, updated);
            for (int i = 0; i < _undo.Count; i++) _undo[i] = Swap(_undo[i], updated);
            for (int i = 0; i < _redo.Count; i++) _redo[i] = Swap(_redo[i], updated);
        }
        PersistSoon();
        PageUpdated?.Invoke(updated);
        return true;
    }

    private static List<PageRecord> Swap(List<PageRecord> list, PageRecord updated)
    {
        int i = list.FindIndex(p => p.Id == updated.Id);
        if (i < 0) return list;
        var copy = list.ToList();
        copy[i] = updated;
        return copy;
    }

    /// <summary>Removes pages (by Id) from the current list and every snapshot without an undo
    /// step -- e.g. an ingested page turned out to be blank.</summary>
    public void Discard(IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        if (set.Count == 0) return;
        lock (_gate)
        {
            Pages = Pages.Where(p => !set.Contains(p.Id)).ToList();
            for (int i = 0; i < _undo.Count; i++) _undo[i] = _undo[i].Where(p => !set.Contains(p.Id)).ToList();
            for (int i = 0; i < _redo.Count; i++) _redo[i] = _redo[i].Where(p => !set.Contains(p.Id)).ToList();
        }
        OnChanged();
    }

    /// <summary>Copies a file into the project's pages folder (new unique name).</summary>
    public string ImportFile(string sourcePath)
    {
        string dest = Path.Combine(PagesFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant());
        File.Copy(sourcePath, dest);
        return dest;
    }

    /// <summary>
    /// Saves the project into <paramref name="folder"/> (copying source files there if they
    /// live elsewhere, e.g. "Save as" from a session). Undo history is kept.
    /// </summary>
    public void SaveAs(string folder)
    {
        folder = Path.GetFullPath(folder);
        lock (_gate)
        {
            if (!string.Equals(folder, Folder, StringComparison.OrdinalIgnoreCase))
            {
                string pagesDir = Path.Combine(folder, "pages");
                Directory.CreateDirectory(pagesDir);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string Relocate(string path)
                {
                    if (map.TryGetValue(path, out string? moved)) return moved;
                    string dest = Path.Combine(pagesDir, Path.GetFileName(path));
                    if (!string.Equals(Path.GetFullPath(path), dest, StringComparison.OrdinalIgnoreCase))
                        File.Copy(path, dest, overwrite: true);
                    return map[path] = dest;
                }
                List<PageRecord> Remap(List<PageRecord> list) =>
                    list.Select(r => r with { Source = r.Source with { File = Relocate(r.Source.File) } }).ToList();

                Pages = Remap(Pages);
                for (int i = 0; i < _undo.Count; i++) _undo[i] = Remap(_undo[i]);
                for (int i = 0; i < _redo.Count; i++) _redo[i] = Remap(_redo[i]);
                Folder = folder;
                IsSession = false;
            }
        }
        Persist();
        OnChanged();
    }

    /// <summary>Writes project.xml now.</summary>
    public void Persist()
    {
        List<PageRecord> snapshot;
        string folder;
        lock (_gate)
        {
            snapshot = Pages;
            folder = Folder;
        }
        Directory.CreateDirectory(folder);
        var dto = new ProjectDto
        {
            Version = FormatVersion,
            Pages = snapshot.Select(p => new PageDto
            {
                Id = p.Id,
                File = Path.GetRelativePath(folder, p.Source.File),
                PdfPage = p.Source.PdfPage,
                NativeDpi = p.Source.NativeDpi,
                Label = p.Label,
                State = p.State.ToString(),
                Error = p.Error ?? "",
                Rotate = p.Ops.Rotate,
                Deskew = p.Ops.Deskew,
                Crop = PageOps.FormatCrop(p.Ops.Crop),
            }).ToList(),
        };
        string file = Path.Combine(folder, ProjectFileName);
        string tmp = file + ".tmp";
        lock (_persistLock)
        {
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(ProjectDto)).Serialize(fs, dto);
            File.Move(tmp, file, overwrite: true);
        }
    }

    private readonly object _persistLock = new();

    /// <summary>Coalesces the autosave of a burst of <see cref="Update"/> calls (a 300-page
    /// import finishing page after page) into one write shortly after the last.</summary>
    private void PersistSoon()
    {
        if (!IsSession) return;
        lock (_gate)
        {
            _persistTimer ??= new Timer(_ =>
            {
                try { Persist(); } catch (Exception ex) { Log.Warn("Session autosave failed", ex); }
            });
            _persistTimer.Change(400, Timeout.Infinite);
        }
    }

    /// <summary>Writes any pending autosave immediately (call before closing).</summary>
    public void Flush()
    {
        Timer? t;
        lock (_gate) { t = _persistTimer; _persistTimer = null; }
        if (t == null) return;
        t.Dispose();
        try { if (IsSession) Persist(); } catch (Exception ex) { Log.Warn("Session flush failed", ex); }
    }

    /// <summary>Deletes files in the pages folder referenced neither by the current page
    /// list nor by any undo / redo snapshot.</summary>
    public int CleanUnreferencedFiles()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
            foreach (var list in _undo.Concat(_redo).Append(Pages))
                foreach (PageRecord r in list) used.Add(Path.GetFullPath(r.Source.File));
        int n = 0;
        foreach (string f in Directory.EnumerateFiles(PagesFolder))
        {
            if (used.Contains(Path.GetFullPath(f))) continue;
            try { File.Delete(f); n++; } catch { /* in use: leave it */ }
        }
        return n;
    }

    /// <summary>Removes a session folder entirely (when the user starts over).</summary>
    public void DeleteIfSession()
    {
        if (!IsSession) return;
        Flush();
        try { Directory.Delete(Folder, true); } catch (Exception ex) { Log.Warn("Could not delete session folder " + Folder, ex); }
    }

    private void OnChanged()
    {
        if (IsSession)
        {
            try { Persist(); } catch (Exception ex) { Log.Warn("Session autosave failed", ex); }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [XmlRoot("ScanProject")]
    public sealed class ProjectDto
    {
        /// <summary>Absent (0) in version-1 files.</summary>
        [XmlAttribute] public int Version { get; set; }

        [XmlArray("Pages"), XmlArrayItem("Page")]
        public List<PageDto> Pages { get; set; } = new();
    }

    public sealed class PageDto
    {
        [XmlAttribute] public string Id { get; set; } = "";
        [XmlAttribute] public string File { get; set; } = "";
        [XmlAttribute, System.ComponentModel.DefaultValue(-1)] public int PdfPage { get; set; } = -1;
        [XmlAttribute, System.ComponentModel.DefaultValue(0.0)] public double NativeDpi { get; set; }
        [XmlAttribute] public string Label { get; set; } = "";
        [XmlAttribute] public string State { get; set; } = "Ready";
        [XmlAttribute] public string Error { get; set; } = "";
        [XmlAttribute] public int Rotate { get; set; }
        [XmlAttribute] public double Deskew { get; set; }
        [XmlAttribute] public string Crop { get; set; } = "";
    }
}
