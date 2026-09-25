using System.Text.Json.Serialization;
using ImageCoreService;

namespace DocScanner.Core;

/// <summary>
/// Where a page is in the background pipeline. The page shows up in the UI the moment it is
/// added; the expensive work follows in stages, each one visible as soon as it is done.
/// </summary>
public enum PageState
{
    /// <summary>Original copied into the document; nothing derived yet.</summary>
    Pending,
    /// <summary>Thumbnail exists (size and EXIF are known); the screen proxy is still being made.</summary>
    Preview,
    /// <summary>Proxy exists: the page can be opened. (The paper outline may still be coming.)</summary>
    Ready,
    /// <summary>The photo could not be decoded (see <see cref="PageRecord.Error"/>).</summary>
    Failed,
}

/// <summary>
/// One scanned page. The original photo is kept byte-for-byte on disk (full resolution,
/// EXIF untouched); everything shown on screen comes from the small derived files.
/// </summary>
public sealed class PageRecord
{
    public string Id { get; set; } = "";

    /// <summary>Documents written before this field existed had only finished pages, hence the default.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PageState>))]
    public PageState State { get; set; } = PageState.Ready;

    /// <summary>Why the page is <see cref="PageState.Failed"/>.</summary>
    public string? Error { get; set; }

    /// <summary>File extension of the stored original (".jpg", ".png", ".heic"...).</summary>
    public string OriginalExtension { get; set; } = ".jpg";

    /// <summary>Pixel size of the original exactly as stored in the file, i.e. BEFORE the
    /// EXIF orientation is applied.</summary>
    public int RawWidth { get; set; }
    public int RawHeight { get; set; }

    /// <summary>EXIF orientation tag 1..8 of the original file (1 = as stored).</summary>
    public int ExifOrientation { get; set; } = 1;

    /// <summary>Extra rotation chosen by the user, in degrees clockwise (0, 90, 180 or 270), for
    /// photos whose EXIF orientation is wrong or missing.</summary>
    public int UserRotation { get; set; }

    /// <summary>The orientation that makes the page upright: the file's EXIF tag combined with
    /// <see cref="UserRotation"/>. Everything that reads the original at full resolution (the
    /// perspective crop) must use this, not <see cref="ExifOrientation"/>.</summary>
    [JsonIgnore]
    public int EffectiveOrientation => ImageGeometry.ComposeRotation(ExifOrientation, UserRotation);

    /// <summary>Size of the upright (orientation applied) screen proxy.</summary>
    public int ProxyWidth { get; set; }
    public int ProxyHeight { get; set; }

    /// <summary>Paper outline as 8 numbers x0,y0..x3,y3 (TL, TR, BR, BL) normalized to 0..1 of
    /// the upright image, so it applies to the proxy and to the full-size original alike.
    /// Null until detection has run.</summary>
    public double[]? CropQuad { get; set; }

    /// <summary>0..1 score of the automatic detection.</summary>
    public double CropConfidence { get; set; }

    /// <summary>True once the user has moved the outline by hand: automatic detection then leaves it alone.</summary>
    public bool CropManual { get; set; }

    /// <summary>True when the detector really found a sheet; false when <see cref="CropQuad"/>
    /// is just the whole frame (the fallback).</summary>
    public bool CropDetected { get; set; }

    /// <summary>Revision of the straightened page made from the original (0 = none yet). The files are
    /// named after it, so a new render never shows a stale cached picture.</summary>
    public int CroppedRevision { get; set; }

    public int CroppedWidth { get; set; }
    public int CroppedHeight { get; set; }

    /// <summary>The outline and rotation the current render was made from.</summary>
    public double[]? CroppedQuad { get; set; }
    public int CroppedRotation { get; set; }

    /// <summary>False (default) = the straightened page is an A4 sheet; true = it keeps the proportions of the outline.</summary>
    public bool FreeAspect { get; set; }

    /// <summary>The aspect the current render was made with.</summary>
    public bool CroppedFreeAspect { get; set; }

    /// <summary>Why the last render failed, if it did.</summary>
    public string? RenderError { get; set; }

    /// <summary>True when there is no render yet, or the outline / rotation changed since the last one.</summary>
    [JsonIgnore]
    public bool NeedsRender =>
        CroppedRevision == 0
        || CroppedRotation != UserRotation
        || CroppedFreeAspect != FreeAspect
        || (CropQuad != null && (CroppedQuad == null || !CropQuad.AsSpan().SequenceEqual(CroppedQuad)));

    /// <summary>Upright size of the original, in pixels.
    [JsonIgnore]
    public (int Width, int Height) UprightSize => ImageGeometry.UprightSize(RawWidth, RawHeight, EffectiveOrientation);
}

public sealed class DocumentRecord
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<PageRecord> Pages { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DocumentRecord))]
internal sealed partial class DocumentJsonContext : JsonSerializerContext;
