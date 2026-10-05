using System.Drawing;
using System.Globalization;

namespace ImageCoreService;

public enum PageState
{
    /// <summary>Usable: can be shown, edited and exported.</summary>
    Ready,
    /// <summary>Still being ingested / analysed in the background.</summary>
    Pending,
    /// <summary>Ingest failed (<see cref="PageRecord.Error"/> says why); skipped by export.</summary>
    Failed,
}

/// <summary>Where a page's pixels come from: a raster file, or one page of a PDF
/// (<see cref="PdfPage"/> is 0-based; -1 for a raster file). <see cref="NativeDpi"/> is the
/// finest density a PDF page really contains (0 = unknown): rendering above it only adds pixels.</summary>
public sealed record PageSource(string File, int PdfPage = -1, double NativeDpi = 0)
{
    public bool IsPdf => PdfPage >= 0;
}

/// <summary>
/// Non-destructive edits applied on top of the source, always in this order:
/// crop -> (limit to the target DPI) -> deskew -> rotate. All values are independent of pixel
/// resolution (crop is a fraction of the source, angles are degrees), so the same ops
/// apply to a thumbnail, a proxy, or the full-size export render.
/// </summary>
public sealed record PageOps(int Rotate = 0, double Deskew = 0, RectangleF? Crop = null)
{
    public static readonly PageOps None = new();

    public bool IsIdentity => Rotate % 360 == 0 && Deskew == 0 && Crop == null;

    /// <summary>Adds a quarter-turn rotation (any multiple of 90, either direction).</summary>
    public PageOps RotatedBy(int degreesClockwise) => this with { Rotate = (((Rotate + degreesClockwise) % 360) + 360) % 360 };

    /// <summary>Stable text form (cache keys).</summary>
    public string Signature =>
        $"r{Rotate % 360}|d{Deskew.ToString("0.###", CultureInfo.InvariantCulture)}|c{FormatCrop(Crop)}";

    public static string FormatCrop(RectangleF? c) => c is RectangleF r
        ? string.Join(",", new[] { r.X, r.Y, r.Width, r.Height }.Select(v => v.ToString("0.#####", CultureInfo.InvariantCulture)))
        : "";

    public static RectangleF? ParseCrop(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] p = s.Split(',');
        if (p.Length != 4) return null;
        var v = new float[4];
        for (int i = 0; i < 4; i++)
            if (!float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return null;
        return new RectangleF(v[0], v[1], v[2], v[3]);
    }
}

/// <summary>
/// One page of a project. Immutable: an edit produces a new record with the same
/// <see cref="Id"/>, which is what keeps undo a simple snapshot swap. The pixels are never
/// rewritten by an edit; <see cref="PageRenderer"/> applies <see cref="Ops"/> to
/// <see cref="Source"/> on demand.
/// </summary>
public sealed record PageRecord(string Id, PageSource Source, PageOps Ops, PageState State, string Label, string? Error = null)
{
    /// <summary>The file holding the source pixels.</summary>
    public string FilePath => Source.File;

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    public static PageRecord FromFile(string file, string label, int pdfPage = -1) =>
        new(NewId(), new PageSource(file, pdfPage), PageOps.None, PageState.Ready, label);


    /// <summary>Changes whenever what the page looks like changes (thumbnail / preview cache key).</summary>
    public string ViewKey => $"{Id}|{Source.File}|{Source.PdfPage}|{Ops.Signature}";
}
