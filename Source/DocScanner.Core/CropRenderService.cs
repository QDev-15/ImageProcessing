using ImageCoreService;

namespace DocScanner.Core;

/// <summary>
/// Makes the straightened page from the ORIGINAL photo (never from the screen proxy, so the result
/// keeps the photo's full resolution up to the A4 / 300 DPI cap). The outline is stored on the
/// upright picture; it is mapped back to the stored, possibly rotated file with the page's effective
/// orientation, only the part of the photo holding the page is decoded (at a power-of-two
/// shrink that still gives the output resolution), and that part is warped into a rectangle.
/// </summary>
public sealed class CropRenderService(DocumentStore store, IImageService images)
{
    public const int JpegQuality = 94;
    public const int ThumbEdge = 512;
    private const int ThumbQuality = 85;

    /// <summary>Renders the page and records the result. No-op for a page that is not ready or is gone.</summary>
    public async Task RenderAsync(string docId, string pageId, CancellationToken ct = default)
    {
        PageRecord? page = store.Pages(docId).FirstOrDefault(p => p.Id == pageId);
        if (page == null || page.State != PageState.Ready || page.RawWidth <= 0 || page.RawHeight <= 0) return;

        // What the render is for: remembered so a later edit is recognised as making it stale.
        double[] quadValues = page.CropQuad ?? Quad.Inset(0.03).ToValues();
        int rotation = page.UserRotation;
        int orientation = page.EffectiveOrientation;

        Quad upright = Quad.FromValues(quadValues);
        Quad stored = ImageGeometry.UprightToStored(upright, orientation).Scale(page.RawWidth, page.RawHeight);
        bool freeAspect = page.FreeAspect;
        CropPlan plan = CropPlanner.Plan(stored, page.RawWidth, page.RawHeight, freeAspect ? CropAspect.Free : CropAspect.A4);

        RgbImage region = await images.LoadRegionAsync(store.OriginalPath(docId, page),
            plan.RegionX, plan.RegionY, plan.RegionWidth, plan.RegionHeight, plan.Sample, ct);

        // The decoder may round the region size; use its real scale.
        double kx = (double)region.Width / plan.RegionWidth, ky = (double)region.Height / plan.RegionHeight;
        PointD Local(PointD p) => new((p.X - plan.RegionX) * kx, (p.Y - plan.RegionY) * ky);
        var source = new Quad(Local(stored.TopLeft), Local(stored.TopRight), Local(stored.BottomRight), Local(stored.BottomLeft));

        FilterOptions filter = page.Filter;
        FilteredPage result = await Task.Run(() =>
        {
            RgbImage flat = PerspectiveWarp.Warp(region, source, plan.OutWidth, plan.OutHeight);
            region = null!; // let the decoded photo go before the filter allocates
            return DocumentFilter.Apply(flat, filter, PageDpi(flat.Width, flat.Height));
        }, ct);

        int revision = page.CroppedRevision + 1;
        string extension = result.IsBilevel ? ".png" : ".jpg";
        string flatPath = store.CroppedPath(docId, pageId, revision, extension);
        string thumbPath = store.CroppedThumbPath(docId, pageId, revision);
        Directory.CreateDirectory(Path.GetDirectoryName(flatPath)!);
        if (result.Color != null)
            await images.SaveJpegAsync(result.Color, flatPath, JpegQuality, ct);
        else if (result.IsBilevel)
            await File.WriteAllBytesAsync(flatPath, PngWriter.EncodeBilevel(result.Gray!), ct); // lossless, tiny, embeds straight into PDF
        else
            await images.SaveJpegAsync(RgbImage.FromGray(result.Gray!), flatPath, JpegQuality, ct);
        await images.SaveJpegAsync(MakeThumb(result), thumbPath, ThumbQuality, ct);
        int outWidth = result.Width, outHeight = result.Height;

        int previous = 0;
        string previousExtension = ".jpg";
        bool kept = store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null) return;
            previous = p.CroppedRevision;
            previousExtension = p.CroppedExtension;
            p.CroppedRevision = revision;
            p.CroppedExtension = extension;
            p.CroppedWidth = outWidth;
            p.CroppedHeight = outHeight;
            p.CroppedQuad = quadValues;
            p.CroppedRotation = rotation;
            p.CroppedFreeAspect = freeAspect;
            p.CroppedColorMode = filter.Mode;
            p.CroppedBwDarkness = filter.Darkness;
            p.CroppedCleanBackground = filter.CleanBackground;
            p.RenderError = null;
        });

        // Old renders are only garbage once the new one is recorded.
        if (kept && previous > 0)
            DeleteQuietly(store.CroppedPath(docId, pageId, previous, previousExtension), store.CroppedThumbPath(docId, pageId, previous));
        if (!kept) DeleteQuietly(flatPath, thumbPath); // the page was deleted meanwhile
    }

    /// <summary>Resolution of a straightened page, taking its long side as an A4 sheet's (11.69 in). Exact
    /// for A4 renders; for free-aspect pages it only sizes the black-and-white window and specks, where
    /// being within a factor of two is plenty.</summary>
    public static int PageDpi(int width, int height) => Math.Max(50, (int)Math.Round(Math.Max(width, height) / 11.69));

    private static RgbImage MakeThumb(FilteredPage page)
    {
        (int tw, int th) = ImageGeometry.FitLongEdge(page.Width, page.Height, ThumbEdge);
        if (page.Color != null) return page.Color.Resize(tw, th);
        // Shrink the gray page first so only a small RGB copy is ever made.
        int factor = Math.Max(1, Math.Min(page.Width / tw, page.Height / th));
        return RgbImage.FromGray(page.Gray!.Downscale(factor)).Resize(tw, th);
    }

    private static void DeleteQuietly(params string[] paths)
    {
        foreach (string p in paths)
        {
            try { File.Delete(p); }
            catch (IOException) { }
        }
    }
}
