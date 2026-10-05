using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// Turns a <see cref="PageRecord"/> into pixels: loads the source and applies the page's
/// <see cref="PageOps"/>. This is the single seam between the project model and everything
/// that needs an image (preview, thumbnails, export, splitting), so the storage of a page
/// can change without touching those consumers.
/// </summary>
public static class PageRenderer
{
    /// <summary>
    /// Full-quality bitmap of the page with every op applied. <paramref name="targetDpi"/> &gt; 0
    /// limits the resolution (never upscales). <paramref name="modified"/> is false when the
    /// result is pixel-for-pixel the source (a raw JPEG can then be embedded untouched).
    /// The caller disposes the bitmap.
    /// </summary>
    public static Bitmap RenderFull(PageRecord page, int targetDpi, out bool modified) =>
        ApplyOps(LoadSource(page.Source, targetDpi), page.Ops, targetDpi, out modified);

    /// <summary>
    /// Applies <paramref name="ops"/> to <paramref name="source"/> (crop, limit to
    /// <paramref name="targetDpi"/>, deskew, rotate). Takes ownership of <paramref name="source"/>:
    /// the returned bitmap is either a new one or the same instance when nothing had to change;
    /// the caller disposes the result and must not touch <paramref name="source"/> again.
    /// Works at any resolution -- ops are resolution independent -- so it serves thumbnails
    /// and previews (on a small proxy) exactly as it serves the export render.
    /// </summary>
    public static Bitmap ApplyOps(Bitmap source, PageOps ops, int targetDpi, out bool modified)
    {
        modified = false;
        Bitmap current = source;
        try
        {
            (int dx, int dy) = ImageUtils.ResolveDpiXY(current);
            if (Math.Abs(current.HorizontalResolution - dx) > 0.5f || Math.Abs(current.VerticalResolution - dy) > 0.5f)
                current.SetResolution(dx, dy); // pin an inferred DPI so every later step sees it
            bool compact = current.PixelFormat is PixelFormat.Format1bppIndexed or PixelFormat.Format8bppIndexed;

            if (ops.Crop is RectangleF c)
            {
                var r = Rectangle.Intersect(new Rectangle(0, 0, current.Width, current.Height), new Rectangle(
                    (int)Math.Round(c.X * current.Width), (int)Math.Round(c.Y * current.Height),
                    Math.Max(1, (int)Math.Round(c.Width * current.Width)), Math.Max(1, (int)Math.Round(c.Height * current.Height))));
                if (r.Width > 0 && r.Height > 0 && (r.Width < current.Width || r.Height < current.Height))
                {
                    Replace(ref current, BitmapTransforms.Crop(current, r));
                    modified = true;
                }
            }

            if (targetDpi > 0)
            {
                Bitmap? reduced = ResolutionLimiter.ResampleToDpi(current, targetDpi);
                if (reduced != null) { Replace(ref current, reduced); modified = true; }
            }

            if (ops.Deskew != 0)
            {
                Replace(ref current, BitmapTransforms.RotateArbitrary(current, -ops.Deskew));
                modified = true;
            }

            if (ops.Rotate % 360 != 0)
            {
                Replace(ref current, BitmapTransforms.RotateRight(current, ops.Rotate));
                modified = true;
            }

            // Crop / arbitrary rotation produce 24bpp; there is no color to preserve in a
            // bitonal / gray source, so keep it compact.
            if (modified && compact && current.PixelFormat is not (PixelFormat.Format8bppIndexed or PixelFormat.Format1bppIndexed))
            {
                (int nx, int ny) = ImageUtils.ResolveDpiXY(current);
                Replace(ref current, GdiGray.FromBitmap(current).ToBitmap8bpp(nx, ny));
            }
            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    public static Bitmap RenderFull(PageRecord page, int targetDpi = 0) => RenderFull(page, targetDpi, out _);

    /// <summary>True when the page is an untouched JPEG file whose bytes can go into a PDF as they are.</summary>
    public static bool IsRawJpeg(PageRecord page)
    {
        if (page.Source.IsPdf || !page.Ops.IsIdentity) return false;
        string ext = Path.GetExtension(page.Source.File).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".jpe";
    }

    /// <summary>Writes the page with its ops applied into <paramref name="folder"/> (lossless) and
    /// returns a record of that new file with no ops left (same Id, label and state).</summary>
    public static PageRecord Bake(PageRecord page, string folder)
    {
        if (!page.Source.IsPdf && page.Ops.IsIdentity) return page;
        using Bitmap bmp = RenderFull(page, 0);
        Directory.CreateDirectory(folder);
        string path = ImageUtils.SaveLossless(bmp, Path.Combine(folder, Guid.NewGuid().ToString("N")));
        return page with { Source = new PageSource(path), Ops = PageOps.None };
    }

    /// <summary>The source pixels. A PDF page is rendered here, directly at the density that will
    /// be used (the target DPI, or 300, but never above what the page really contains), so a
    /// high-resolution intermediate is never produced only to be scaled down.</summary>
    public static Bitmap LoadSource(PageSource source, int targetDpi = 0)
    {
        if (!source.IsPdf) return ImageUtils.Load(source.File);
        int dpi = targetDpi > 0 ? targetDpi : PdfPageRenderer.DefaultDpi;
        if (source.NativeDpi > 0 && source.NativeDpi < dpi) dpi = Math.Max(36, (int)Math.Round(source.NativeDpi));
        return PdfPageRenderer.Render(source.File, source.PdfPage, dpi);
    }

    /// <summary>Pixel size and DPI of the source as it will be rendered, without decoding it: read
    /// from the image header, or computed from the PDF page size.</summary>
    public static (int Width, int Height, int DpiX, int DpiY) ReadInfo(PageSource source, int targetDpi = 0)
    {
        if (!source.IsPdf) return ImageUtils.ReadInfo(source.File);
        var pt = PdfPageRenderer.PageSize(source.File, source.PdfPage);
        int dpi = targetDpi > 0 ? targetDpi : PdfPageRenderer.DefaultDpi;
        if (source.NativeDpi > 0 && source.NativeDpi < dpi) dpi = (int)Math.Round(source.NativeDpi);
        int w = (int)Math.Round(pt.Width / 72.0 * dpi), h = (int)Math.Round(pt.Height / 72.0 * dpi);
        return (w, h, dpi, dpi);
    }

    private static void Replace(ref Bitmap current, Bitmap next)
    {
        current.Dispose();
        current = next;
    }
}
