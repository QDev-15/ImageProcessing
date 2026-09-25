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

    /// <summary>The straightened page of the given revision.</summary>
    public string CroppedPath(string docId, string pageId, int revision) =>
        Path.Combine(PageFolder(docId, pageId), $"cropped_{revision}.jpg");

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
