namespace DocScanner.Core;

/// <summary>
/// How color / gray pages go into an exported PDF. The pages kept in the app stay at full quality
/// (up to A4 at 300 DPI, JPEG 94: good for viewing and re-editing); at export they are shrunk to
/// <see cref="Dpi"/> and re-encoded at <see cref="JpegQuality"/>. Black-and-white pages are always
/// embedded as they are (1-bit PNG: already small, and shrinking would blur the text).
/// </summary>
public sealed record PdfQuality(string Key, string Label, int Dpi, int JpegQuality)
{
    /// <summary>Email / chat apps: about 100-200 KB per color page.</summary>
    public static readonly PdfQuality Small = new("small", "Nhỏ · gửi Zalo, email (150 DPI)", 150, 60);

    /// <summary>The default: sharp on screen and fine to print, about 150-300 KB per cleaned color page.</summary>
    public static readonly PdfQuality Medium = new("medium", "Vừa · khuyên dùng (200 DPI)", 200, 72);

    /// <summary>Printing / archiving.</summary>
    public static readonly PdfQuality High = new("high", "Cao · in ấn (300 DPI)", 300, 90);

    public static IReadOnlyList<PdfQuality> All { get; } = [Small, Medium, High];

    public static PdfQuality FromKey(string? key) => All.FirstOrDefault(q => q.Key == key) ?? Medium;

    /// <summary>Long edge in pixels for a page at this resolution (the long side of an A4 sheet, 11.69 in).</summary>
    public int LongEdgePx => (int)Math.Round(11.69 * Dpi);
}
