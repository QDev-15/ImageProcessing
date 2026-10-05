using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace ImageCoreService;

/// <summary>
/// Disposable derived images of a project, kept in its cache folder (safe to delete; rebuilt on
/// demand):
///   proxy\   the raw page scaled to <see cref="ProxyEdge"/> px on its long edge (JPEG). Ops
///            (rotate, crop, deskew) are applied on top at view time, so editing never
///            re-decodes the full-size source;
///   thumbs\  the list thumbnails, one per (page look, size).
/// Everything is keyed by the source file's identity (path + size + timestamp), so replacing
/// a page's source can never show a stale image.
/// </summary>
public sealed class PageCache(string folder)
{
    public const int ProxyEdge = 1600;
    private const long JpegQuality = 85;

    private readonly ConcurrentDictionary<string, object> _locks = new();

    public string Folder { get; } = folder;

    /// <summary>The page as it looks (all ops applied) at proxy resolution. Caller disposes.</summary>
    public Bitmap RenderPreview(PageRecord page)
    {
        Bitmap proxy = GetProxy(page.Source);
        return PageRenderer.ApplyOps(proxy, page.Ops, 0, out _);
    }

    /// <summary>The proxy as stored (no ops), created from the source if it does not exist yet.
    /// Caller disposes.</summary>
    public Bitmap GetProxy(PageSource source)
    {
        string path = ProxyPath(source);
        Bitmap? cached = TryLoad(path);
        if (cached != null) return cached;

        lock (_locks.GetOrAdd(path, _ => new object()))
        {
            cached = TryLoad(path);
            if (cached != null) return cached;
            using (Perf.Scope("cache.proxy"))
            {
                // A PDF page is rendered straight at proxy size; a raster file is decoded once.
                Bitmap proxy;
                if (source.IsPdf)
                    proxy = PdfPageRenderer.RenderFit(source.File, source.PdfPage, ProxyEdge);
                else
                {
                    using Bitmap full = PageRenderer.LoadSource(source);
                    proxy = Downscale(full, ProxyEdge);
                }
                Save(proxy, path, JpegQuality);
                return proxy;
            }
        }
    }

    /// <summary>List thumbnail for the page's current look. Caller disposes.</summary>
    public Bitmap GetThumbnail(PageRecord page, Size box)
    {
        string path = Path.Combine(Folder, "thumbs", $"{Hash(page.Source, page.Ops.Signature)}_{box.Width}x{box.Height}.jpg");
        Bitmap? cached = TryLoad(path);
        if (cached != null) return cached;

        using (Perf.Scope("cache.thumb"))
        {
            using Bitmap preview = RenderPreview(page);
            Bitmap thumb = ImageUtils.MakeThumbnail(preview, box.Width, box.Height);
            Save(thumb, path, 80);
            return thumb;
        }
    }

    /// <summary>Removes every cached image (they are rebuilt on demand).</summary>
    public void Clear()
    {
        try { if (Directory.Exists(Folder)) Directory.Delete(Folder, true); } catch (Exception ex) { Log.Warn("Cache clear failed", ex); }
    }

    private string ProxyPath(PageSource source) => Path.Combine(Folder, "proxy", Hash(source, "") + ".jpg");

    private static string Hash(PageSource source, string extra) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{SourceStamp(source)}|{extra}")))[..20].ToLowerInvariant();

    /// <summary>Identity of a source's pixels: path, page, size and timestamp of the file. Any
    /// derived data (proxy, thumbnail, OCR words) is keyed on it, so replacing the file
    /// invalidates them.</summary>
    public static string SourceStamp(PageSource source)
    {
        var fi = new FileInfo(source.File);
        return $"{Path.GetFullPath(source.File).ToLowerInvariant()}|{source.PdfPage}|{(fi.Exists ? fi.Length : 0)}|{(fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0)}";
    }

    private static Bitmap? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try { return ImageUtils.Load(path); }
        catch { try { File.Delete(path); } catch { } return null; } // truncated by a crash: rebuild
    }

    private static Bitmap Downscale(Bitmap src, int maxEdge)
    {
        (int dx, int dy) = ImageUtils.ResolveDpiXY(src);
        double scale = Math.Min(1.0, (double)maxEdge / Math.Max(src.Width, src.Height));
        int w = Math.Max(1, (int)Math.Round(src.Width * scale)), h = Math.Max(1, (int)Math.Round(src.Height * scale));
        var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        dst.SetResolution((float)(dx * scale), (float)(dy * scale));
        using Graphics g = Graphics.FromImage(dst);
        g.Clear(Color.White);
        g.InterpolationMode = scale < 1 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var attrs = new ImageAttributes();
        attrs.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        return dst;
    }

    private static void Save(Bitmap bmp, string path, long quality)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + "." + Environment.CurrentManagedThreadId + ".tmp";
            ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(tmp, jpeg, ep);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warn("Cache write failed: " + path, ex); // the in-memory image is still valid
        }
    }
}
