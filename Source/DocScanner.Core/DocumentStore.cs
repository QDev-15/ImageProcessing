using System.Text.Json;

namespace DocScanner.Core;

/// <summary>
/// Documents on disk: <c>root/{docId}/doc.json</c> plus one folder per page
/// (<c>original.*</c>, <c>proxy.jpg</c>, <c>thumb.jpg</c>). doc.json is written atomically
/// (temp file + replace) so a crash or a killed app never leaves a half-written document.
///
/// Each document lives in memory exactly once: the UI, the import and the background pipeline
/// all get the same <see cref="DocumentRecord"/> instance, and every change goes through
/// <see cref="Update"/>, which serializes writers and saves. (Separate copies would overwrite
/// each other's progress.)
/// </summary>
public sealed class DocumentStore(string root)
{
    private const string DocFileName = "doc.json";
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentRecord> _cache = [];

    public string Root => root;

    public DocumentRecord Create(string? name = null)
    {
        var doc = new DocumentRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? $"Tài liệu {DateTime.Now:dd-MM-yyyy HH:mm}" : name.Trim(),
            CreatedUtc = DateTime.UtcNow,
        };
        lock (_gate)
        {
            _cache[doc.Id] = doc;
            SaveLocked(doc);
        }
        return doc;
    }

    /// <summary>All readable documents, newest first. A corrupt doc.json is skipped rather
    /// than hiding every other document.</summary>
    public IReadOnlyList<DocumentRecord> List()
    {
        lock (_gate)
        {
            if (!Directory.Exists(root)) return [];
            var result = new List<DocumentRecord>();
            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                DocumentRecord? doc = GetLocked(Path.GetFileName(dir));
                if (doc != null) result.Add(doc);
            }
            return result.OrderByDescending(d => d.CreatedUtc).ToList();
        }
    }

    /// <summary>The shared in-memory document, read from disk on first use.</summary>
    public DocumentRecord? Get(string id)
    {
        CheckId(id);
        lock (_gate) return GetLocked(id);
    }

    /// <summary>Snapshot of a document's pages that is safe to enumerate while the background
    /// pipeline changes the document. (The page objects are shared; only their fields change.)</summary>
    public IReadOnlyList<PageRecord> Pages(string docId)
    {
        CheckId(docId);
        lock (_gate) return GetLocked(docId)?.Pages.ToList() ?? [];
    }

    /// <summary>Applies <paramref name="mutate"/> to the document under the store lock and saves it.
    /// Returns false when the document no longer exists (e.g. deleted while a job was queued).</summary>
    public bool Update(string docId, Action<DocumentRecord> mutate)
    {
        CheckId(docId);
        lock (_gate)
        {
            DocumentRecord? doc = GetLocked(docId);
            if (doc == null) return false;
            mutate(doc);
            SaveLocked(doc);
            return true;
        }
    }

    public void Delete(string id)
    {
        CheckId(id);
        lock (_gate)
        {
            _cache.Remove(id);
            string folder = DocumentFolder(id);
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    public void DeletePage(string docId, string pageId)
    {
        CheckId(docId);
        CheckId(pageId);
        lock (_gate)
        {
            DocumentRecord? doc = GetLocked(docId);
            if (doc == null) return;
            doc.Pages.RemoveAll(p => p.Id == pageId);
            SaveLocked(doc);
            string folder = PageFolder(docId, pageId);
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Renames a document (blank names are ignored). Returns false when it no longer exists.</summary>
    public bool Rename(string docId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return Update(docId, d => d.Name = name.Trim());
    }

    /// <summary>Moves a page to <paramref name="newIndex"/> (clamped) in the document's order.</summary>
    public bool MovePage(string docId, string pageId, int newIndex)
    {
        bool moved = false;
        Update(docId, d =>
        {
            int from = d.Pages.FindIndex(p => p.Id == pageId);
            if (from < 0) return;
            int to = Math.Clamp(newIndex, 0, d.Pages.Count - 1);
            if (to == from) return;
            PageRecord page = d.Pages[from];
            d.Pages.RemoveAt(from);
            d.Pages.Insert(to, page);
            moved = true;
        });
        return moved;
    }

    /// <summary>The page <paramref name="delta"/> places after (+) or before (-) the given one, with its 0-based
    /// index and the page count; null at either end, or when the page is gone.</summary>
    public (string PageId, int Index, int Count)? Neighbor(string docId, string pageId, int delta)
    {
        IReadOnlyList<PageRecord> pages = Pages(docId);
        int i = pages.ToList().FindIndex(p => p.Id == pageId);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= pages.Count) return null;
        return (pages[j].Id, j, pages.Count);
    }

    /// <summary>Puts the pages in exactly this order (ids of the document's pages; unknown ids ignored,
    /// pages not listed keep their relative order at the end). Used to undo a move.</summary>
    public bool SetOrder(string docId, IReadOnlyList<string> pageIds) =>
        Update(docId, d =>
        {
            var byId = d.Pages.ToDictionary(p => p.Id);
            var ordered = pageIds.Where(byId.ContainsKey).Distinct().Select(id => byId[id]).ToList();
            ordered.AddRange(d.Pages.Where(p => !ordered.Contains(p)));
            d.Pages.Clear();
            d.Pages.AddRange(ordered);
        });

    /// <summary>Deletes a page but keeps it restorable: its folder moves to the document's trash and the
    /// record is returned (with its old position) for <see cref="RestorePage"/>. Null if not found.</summary>
    public DeletedPage? TrashPage(string docId, string pageId)
    {
        CheckId(docId);
        CheckId(pageId);
        lock (_gate)
        {
            DocumentRecord? doc = GetLocked(docId);
            int index = doc?.Pages.FindIndex(p => p.Id == pageId) ?? -1;
            if (doc == null || index < 0) return null;
            PageRecord page = doc.Pages[index];
            doc.Pages.RemoveAt(index);
            SaveLocked(doc);

            string folder = PageFolder(docId, pageId), trash = TrashFolder(docId, pageId);
            if (Directory.Exists(folder))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(trash)!);
                if (Directory.Exists(trash)) Directory.Delete(trash, recursive: true);
                Directory.Move(folder, trash);
            }
            return new DeletedPage(docId, page, index);
        }
    }

    /// <summary>Puts a trashed page back at its old position. False when its files are gone (trash emptied).</summary>
    public bool RestorePage(DeletedPage deleted)
    {
        lock (_gate)
        {
            DocumentRecord? doc = GetLocked(deleted.DocId);
            string trash = TrashFolder(deleted.DocId, deleted.Page.Id);
            if (doc == null || !Directory.Exists(trash) || doc.Pages.Any(p => p.Id == deleted.Page.Id)) return false;
            Directory.Move(trash, PageFolder(deleted.DocId, deleted.Page.Id));
            doc.Pages.Insert(Math.Clamp(deleted.Index, 0, doc.Pages.Count), deleted.Page);
            SaveLocked(doc);
            return true;
        }
    }

    /// <summary>Permanently removes trashed pages (called when leaving the document, and at start-up).</summary>
    public void EmptyTrash(string docId)
    {
        lock (_gate)
        {
            string folder = Path.Combine(DocumentFolder(docId), TrashName);
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
            catch (IOException) { } // a file still open: try again next time
        }
    }

    private const string TrashName = ".trash";

    private string TrashFolder(string docId, string pageId) => Path.Combine(DocumentFolder(docId), TrashName, pageId);

    public string DocumentFolder(string docId)
    {
        CheckId(docId);
        return Path.Combine(root, docId);
    }

    public string PageFolder(string docId, string pageId)
    {
        CheckId(pageId);
        return Path.Combine(DocumentFolder(docId), pageId);
    }

    public string OriginalPath(string docId, PageRecord page) =>
        Path.Combine(PageFolder(docId, page.Id), "original" + page.OriginalExtension);

    public string ProxyPath(string docId, PageRecord page) => Path.Combine(PageFolder(docId, page.Id), "proxy.jpg");

    public string ThumbPath(string docId, PageRecord page) => Path.Combine(PageFolder(docId, page.Id), "thumb.jpg");

    /// <summary>The straightened page of the given revision (".jpg" for color / gray, ".png" for black and white).</summary>
    public string CroppedPath(string docId, string pageId, int revision, string extension = ".jpg") =>
        Path.Combine(PageFolder(docId, pageId), $"cropped_{revision}{extension}");

    /// <summary>The page's current straightened render.</summary>
    public string CroppedPath(string docId, PageRecord page) =>
        CroppedPath(docId, page.Id, page.CroppedRevision, page.CroppedExtension);

    public string CroppedThumbPath(string docId, string pageId, int revision) =>
        Path.Combine(PageFolder(docId, pageId), $"cropped_thumb_{revision}.jpg");

    private DocumentRecord? GetLocked(string id)
    {
        if (_cache.TryGetValue(id, out DocumentRecord? cached)) return cached;
        DocumentRecord? doc = TryRead(Path.Combine(DocumentFolder(id), DocFileName));
        if (doc == null || doc.Id != id) return null;
        _cache[id] = doc;
        return doc;
    }

    private void SaveLocked(DocumentRecord doc)
    {
        string folder = DocumentFolder(doc.Id);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, DocFileName);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(doc, DocumentJsonContext.Default.DocumentRecord));
        File.Move(tmp, path, overwrite: true);
    }

    private static DocumentRecord? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize(File.ReadAllText(path), DocumentJsonContext.Default.DocumentRecord);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Ids become folder names, so accept only what this store generates.</summary>
    private static void CheckId(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException($"Invalid id '{id}'.", nameof(id));
    }
}
