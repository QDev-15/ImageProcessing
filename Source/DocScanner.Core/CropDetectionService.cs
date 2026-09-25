using ImageCoreService;

namespace DocScanner.Core;

/// <summary>Runs the paper-edge detector on a page's proxy and records the result in the page.</summary>
public sealed class CropDetectionService(DocumentStore store, IImageService images, IEdgeDetector detector)
{
    /// <summary>Long edge of the picture handed to the detector.</summary>
    public const int AnalysisEdge = 480;

    /// <summary>Detects and saves the outline. Returns null when the page (or document) no longer
    /// exists or has no proxy yet. An outline the user has adjusted by hand is left alone unless
    /// <paramref name="overrideManual"/> is set (the "Tự động" button).</summary>
    public async Task<QuadDetection?> DetectAsync(string docId, string pageId, CancellationToken ct = default, bool overrideManual = false)
    {
        PageRecord? page = store.Pages(docId).FirstOrDefault(p => p.Id == pageId);
        if (page == null || page.State != PageState.Ready) return null;
        int rotation = page.UserRotation;

        RgbImage rgb = await images.LoadRgbAsync(store.ProxyPath(docId, page), AnalysisEdge, ct);
        QuadDetection result = await Task.Run(() => detector.Detect(rgb), ct);

        store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null) return; // deleted while we were working
            if (p.CropManual && !overrideManual) return; // the user got there first
            if (p.UserRotation != rotation) return;      // the page was turned meanwhile: this result is for the old orientation
            p.CropManual = false;
            p.CropQuad = result.Quad.ToValues();
            p.CropConfidence = result.Confidence;
            p.CropDetected = result.Detected;
        });
        return result;
    }
}
