using ImageCoreService;

namespace DocScanner.Core;

/// <summary>User edits of a page: the paper outline and the rotation. Changes go through the store
/// (saved at once) and, for a rotation, back through the background pipeline that remakes the
/// thumbnail and proxy.</summary>
public sealed class PageEditService(DocumentStore store, PageIngestQueue queue, CropDetectionService detection)
{
    /// <summary>Records an outline chosen by hand (normalized, TL TR BR BL). Automatic detection will
    /// not replace it afterwards.</summary>
    public bool SetCrop(string docId, string pageId, Quad quad) =>
        Change(docId, pageId, p =>
        {
            p.CropQuad = quad.ToValues();
            p.CropManual = true;
        });

    /// <summary>Chooses the shape of the straightened page: A4 (default) or the outline's own proportions.
    /// The page then needs a new render.</summary>
    public bool SetFreeAspect(string docId, string pageId, bool free) =>
        Change(docId, pageId, p => p.FreeAspect = free);

    /// <summary>The outline is the whole picture.</summary>
    public bool UseFullImage(string docId, string pageId) => SetCrop(docId, pageId, Quad.Full);

    /// <summary>Runs the detector again and replaces the outline, even a hand-made one.</summary>
    public Task<QuadDetection?> RedetectAsync(string docId, string pageId, CancellationToken ct = default) =>
        detection.DetectAsync(docId, pageId, ct, overrideManual: true);

    /// <summary>Turns the page clockwise by a multiple of 90 degrees. The outline is turned with it;
    /// the thumbnail and proxy are rebuilt in the background (the page is Pending until then).
    /// Only a finished page can be rotated: returns false otherwise.</summary>
    public bool Rotate(string docId, string pageId, int clockwiseDegrees = 90)
    {
        bool rotated = false;
        store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null || p.State != PageState.Ready) return;

            int turns = (((clockwiseDegrees / 90) % 4) + 4) % 4;
            if (turns == 0) return;
            for (int i = 0; i < turns; i++)
            {
                if (p.CropQuad != null) p.CropQuad = ImageGeometry.RotateQuadClockwise(Quad.FromValues(p.CropQuad)).ToValues();
                (p.ProxyWidth, p.ProxyHeight) = (p.ProxyHeight, p.ProxyWidth);
            }
            p.UserRotation = (p.UserRotation + turns * 90) % 360;
            p.State = PageState.Pending;
            rotated = true;
        });
        if (rotated) queue.Enqueue(docId, pageId);
        return rotated;
    }

    private bool Change(string docId, string pageId, Action<PageRecord> change)
    {
        bool found = false;
        store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null) return;
            change(p);
            found = true;
        });
        return found;
    }
}
