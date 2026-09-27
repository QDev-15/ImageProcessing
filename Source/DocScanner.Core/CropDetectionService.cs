using ImageCoreService;

namespace DocScanner.Core;

/// <summary>Runs the paper-edge detector on a page's proxy and records the result in the page.</summary>
public sealed class CropDetectionService(DocumentStore store, IImageService images, IEdgeDetector detector)
{
    /// <summary>Long edge of the picture handed to the detector.</summary>
    public const int AnalysisEdge = 480;

    /// <summary>Long edge of the picture the outline is refined on (the whole screen proxy): corners within a pixel or
    /// two of the photo's own detail instead of ~1/480 of it, and the bulge of a page that is not flat.</summary>
    public const int RefineEdge = 1600;

    /// <summary>Detects and saves the outline. Returns null when the page (or document) no longer
    /// exists or has no proxy yet. An outline the user has adjusted by hand is left alone unless
    /// <paramref name="overrideManual"/> is set (the "Tự động" button).</summary>
    public async Task<QuadDetection?> DetectAsync(string docId, string pageId, CancellationToken ct = default, bool overrideManual = false)
    {
        PageRecord? page = store.Pages(docId).FirstOrDefault(p => p.Id == pageId);
        if (page == null || page.State != PageState.Ready) return null;
        int rotation = page.UserRotation;

        RgbImage rgb;
        using (Perf.Measure("detect: load proxy"))
            rgb = await images.LoadRgbAsync(store.ProxyPath(docId, page), AnalysisEdge, ct);
        QuadDetection result = await Task.Run(() =>
        {
            using (Perf.Measure("detect: detector"))
                return detector.Detect(rgb);
        }, ct);

        // Refine a found outline on the full proxy: exact border, curved sides (PageOutlineRefiner).
        Quad outline = result.Quad;
        double[]? bends = null;
        if (result.Detected)
        {
            RgbImage proxy;
            using (Perf.Measure("detect: load proxy for refining"))
                proxy = await images.LoadRgbAsync(store.ProxyPath(docId, page), RefineEdge, ct);
            PageOutlineRefiner.Result refined = await Task.Run(() =>
            {
                using (Perf.Measure("detect: refine outline"))
                    return PageOutlineRefiner.Refine(proxy, result.Quad.Scale(proxy.Width, proxy.Height));
            }, ct);
            if (refined.SidesRefined > 0)
            {
                outline = refined.Outline.Scale(1.0 / proxy.Width, 1.0 / proxy.Height);
                bends = refined.Bends?.ToValues();
            }
        }

        store.Update(docId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == pageId);
            if (p == null) return; // deleted while we were working
            if (p.CropManual && !overrideManual) return; // the user got there first
            if (p.UserRotation != rotation) return;      // the page was turned meanwhile: this result is for the old orientation
            p.CropManual = false;
            p.CropQuad = outline.ToValues();
            p.CropBend = bends;
            p.CropConfidence = result.Confidence;
            p.CropDetected = result.Detected;
        });
        return result with { Quad = outline }; // what the screen shows: the refined outline
    }
}
