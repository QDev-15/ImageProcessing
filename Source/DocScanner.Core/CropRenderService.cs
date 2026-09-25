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

        RgbImage flat = await Task.Run(() => PerspectiveWarp.Warp(region, source, plan.OutWidth, plan.OutHeight), ct);
        (int tw, int th) = ImageGeometry.FitLongEdge(flat.Width, flat.Height, ThumbEdge);
        RgbImage thumb = flat.Resize(tw, th);

        int revision = page.CroppedRevision + 1;
        string flatPath = store.CroppedPath(docId, pageId, revision);
        string thumbPath = store.CroppedThumbPath(docId, pageId, revision);
        await images.SaveJpegAsync(flat, flatPath, JpegQuality, ct);
        await images.SaveJpegAsync(thumb, thumbPath, ThumbQuality, ct);

        int previous = 0;
        bool kept = store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null) return;
            previous = p.CroppedRevision;
            p.CroppedRevision = revision;
            p.CroppedWidth = flat.Width;
            p.CroppedHeight = flat.Height;
            p.CroppedQuad = quadValues;
            p.CroppedRotation = rotation;
            p.CroppedFreeAspect = freeAspect;
            p.RenderError = null;
        });

        // Old renders are only garbage once the new one is recorded.
        if (kept && previous > 0) DeleteQuietly(store.CroppedPath(docId, pageId, previous), store.CroppedThumbPath(docId, pageId, previous));
        if (!kept) DeleteQuietly(flatPath, thumbPath); // the page was deleted meanwhile
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
