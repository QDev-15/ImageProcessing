using System.Xml.Serialization;

namespace ImageCoreService;

/// <summary>One page of a project. Immutable: an edit produces a new file + new record,
/// which is what makes undo a simple snapshot swap.</summary>
public sealed record PageRecord(string FilePath, string Label);

/// <summary>
/// The page list being worked on, persisted as a project folder:
///   &lt;folder&gt;\project.xml   page order + labels (paths relative to the folder)
///   &lt;folder&gt;\pages\...     page image files (lossless)
/// An unsaved session is just a project in the work folder that is re-saved after every
/// change, so a crash loses nothing (see <see cref="FindLatestSession"/>).
///
/// Undo / redo keep snapshots of the page list. Page files are never deleted while any
/// snapshot can still reference them (<see cref="CleanUnreferencedFiles"/>).
/// </summary>
public sealed class ScanProject
{
    public const string ProjectFileName = "project.xml";

    private readonly List<List<PageRecord>> _undo = new();
    private readonly List<List<PageRecord>> _redo = new();

    public string Folder { get; private set; }
    public bool IsSession { get; private set; }
    public List<PageRecord> Pages { get; private set; } = new();
    public int MaxUndo { get; set; } = 50;

    public string PagesFolder => Path.Combine(Folder, "pages");
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? Changed;

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

    public static ScanProject Open(string folder)
    {
        string file = Path.Combine(folder, ProjectFileName);
        ProjectDto dto;
        using (FileStream fs = File.OpenRead(file))
            dto = (ProjectDto)(new XmlSerializer(typeof(ProjectDto)).Deserialize(fs) ?? throw new InvalidDataException("Empty project file."));
        var p = new ScanProject(folder, isSession: Path.GetFileName(folder).StartsWith("session_", StringComparison.Ordinal));
        foreach (PageDto page in dto.Pages)
        {
            string path = Path.GetFullPath(Path.Combine(folder, page.File));
            if (File.Exists(path)) p.Pages.Add(new PageRecord(path, page.Label));
            else Log.Warn("Project page file missing, skipped: " + path);
        }
        return p;
    }

    /// <summary>Applies a change to the page list as one undoable step.</summary>
    public void Execute(Action<List<PageRecord>> change)
    {
        var before = Pages.ToList();
        var after = Pages.ToList();
        change(after);
        if (after.SequenceEqual(before)) return;

        _undo.Add(before);
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        _redo.Clear();
        Pages = after;
        OnChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Add(Pages);
        Pages = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        OnChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Add(Pages);
        Pages = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
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
    /// Saves the project into <paramref name="folder"/> (copying page files there if they
    /// live elsewhere, e.g. "Save as" from a session). Undo history is kept.
    /// </summary>
    public void SaveAs(string folder)
    {
        folder = Path.GetFullPath(folder);
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
            List<PageRecord> Remap(List<PageRecord> list) => list.Select(r => r with { FilePath = Relocate(r.FilePath) }).ToList();

            Pages = Remap(Pages);
            for (int i = 0; i < _undo.Count; i++) _undo[i] = Remap(_undo[i]);
            for (int i = 0; i < _redo.Count; i++) _redo[i] = Remap(_redo[i]);
            Folder = folder;
            IsSession = false;
        }
        Persist();
        OnChanged();
    }

    /// <summary>Writes project.xml (called after every change for sessions).</summary>
    public void Persist()
    {
        Directory.CreateDirectory(Folder);
        var dto = new ProjectDto
        {
            Pages = Pages.Select(p => new PageDto { File = Path.GetRelativePath(Folder, p.FilePath), Label = p.Label }).ToList(),
        };
        string file = Path.Combine(Folder, ProjectFileName);
        string tmp = file + ".tmp";
        using (var fs = File.Create(tmp))
            new XmlSerializer(typeof(ProjectDto)).Serialize(fs, dto);
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>Deletes files in the pages folder referenced neither by the current page
    /// list nor by any undo / redo snapshot.</summary>
    public int CleanUnreferencedFiles()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in _undo.Concat(_redo).Append(Pages))
            foreach (PageRecord r in list) used.Add(Path.GetFullPath(r.FilePath));
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
        [XmlArray("Pages"), XmlArrayItem("Page")]
        public List<PageDto> Pages { get; set; } = new();
    }

    public sealed class PageDto
    {
        [XmlAttribute] public string File { get; set; } = "";
        [XmlAttribute] public string Label { get; set; } = "";
    }
}
