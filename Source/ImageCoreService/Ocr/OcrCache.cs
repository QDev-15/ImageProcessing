using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ImageCoreService;

/// <summary>
/// Recognized words per page, kept in the project's cache folder so an export (or a second
/// export) does not run Tesseract again for pages already read, whether by an earlier export or
/// by <see cref="BackgroundOcr"/>. The key covers everything that changes what the OCR engine
/// sees: the source file identity, the page's ops, the render resolution, the binarization
/// settings and the languages -- so editing a page or a setting simply misses the cache.
/// </summary>
public sealed class OcrCache(string folder)
{
    public string Folder { get; } = folder;

    public static string Key(PageRecord page, ExportOptions o)
    {
        string id = string.Join("|", PageCache.SourceStamp(page.Source), page.Ops.Signature, o.TargetDpi, o.ColorMode,
            o.Binarization, o.Despeckle, o.SauvolaK.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), o.OcrLanguages);
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(id)))[..24].ToLowerInvariant();
    }

    public bool Contains(string key) => File.Exists(PathFor(key));

    public IReadOnlyList<OcrWord>? TryGet(string key)
    {
        string path = PathFor(key);
        if (!File.Exists(path)) return null;
        try
        {
            using FileStream fs = File.OpenRead(path);
            return JsonSerializer.Deserialize<List<OcrWord>>(fs);
        }
        catch (Exception ex)
        {
            Log.Warn("OCR cache entry unreadable, ignored: " + path, ex);
            return null;
        }
    }

    public void Put(string key, IReadOnlyList<OcrWord> words)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = PathFor(key);
            string tmp = path + "." + Environment.CurrentManagedThreadId + ".tmp";
            using (FileStream fs = File.Create(tmp))
                JsonSerializer.Serialize(fs, words);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn("OCR cache write failed", ex); // the words are still used for this export
        }
    }

    private string PathFor(string key) => Path.Combine(Folder, key + ".json");
}
