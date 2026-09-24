using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ImageCoreService;

/// <summary>
/// Replaces a saved PDF's document XMP metadata by appending an incremental update (new
/// metadata stream + new revision of the catalog pointing at it + xref section + trailer
/// with /Prev). Incremental updates are explicitly allowed by PDF/A-2, and this avoids
/// depending on PDFsharp internals: PDFsharp always writes its own XMP on save and, in its
/// PDF/A mode, declares PDF/A-1A -- not what we produce.
///
/// Works on classic cross-reference tables with an uncompressed catalog, which is what
/// PDFsharp 6 writes.
/// </summary>
internal static class PdfIncrementalXmp
{
    public static void Attach(string pdfPath, byte[] xmp)
    {
        byte[] original = File.ReadAllBytes(pdfPath);
        string text = Encoding.Latin1.GetString(original); // 1:1 byte <-> char

        int trailerPos = text.LastIndexOf("trailer", StringComparison.Ordinal);
        int startxrefPos = text.LastIndexOf("startxref", StringComparison.Ordinal);
        if (trailerPos < 0 || startxrefPos < trailerPos)
            throw new InvalidDataException("PDF has no classic trailer; cannot append XMP update.");

        string trailer = text[trailerPos..startxrefPos];
        int size = Int(trailer, @"/Size\s+(\d+)");
        int root = Int(trailer, @"/Root\s+(\d+)\s+0\s+R");
        Match info = Regex.Match(trailer, @"/Info\s+\d+\s+0\s+R");
        Match id = Regex.Match(trailer, @"/ID\s*\[[^\]]*\]");
        long prevXref = long.Parse(Regex.Match(text[startxrefPos..], @"startxref\s+(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);

        // Latest revision of the catalog object ("N 0 obj ... endobj", no stream inside).
        MatchCollection catalogs = Regex.Matches(text, $@"(?<=^|[\r\n]){root}\s+0\s+obj(.*?)endobj", RegexOptions.Singleline);
        if (catalogs.Count == 0) throw new InvalidDataException("Catalog object not found.");
        string catalog = catalogs[^1].Groups[1].Value.Trim();

        int metaObj = size;
        string metaRef = $"/Metadata {metaObj} 0 R";
        catalog = Regex.IsMatch(catalog, @"/Metadata\s+\d+\s+\d+\s+R")
            ? Regex.Replace(catalog, @"/Metadata\s+\d+\s+\d+\s+R", metaRef)
            : catalog.Insert(catalog.LastIndexOf(">>", StringComparison.Ordinal), metaRef);

        using var ms = new MemoryStream();
        ms.Write(original);
        void W(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        if (original[^1] != '\n') W("\n");

        long catalogOffset = ms.Position;
        W($"{root} 0 obj\n{catalog}\nendobj\n");

        long metaOffset = ms.Position;
        W($"{metaObj} 0 obj\n<</Type/Metadata/Subtype/XML/Length {xmp.Length}>>\nstream\n");
        ms.Write(xmp);
        W("\nendstream\nendobj\n");

        long xrefOffset = ms.Position;
        // Two one-entry subsections (ascending: the catalog number is always below Size).
        W("xref\n0 1\n0000000000 65535 f\r\n");
        W($"{root} 1\n{catalogOffset:D10} 00000 n\r\n");
        W($"{metaObj} 1\n{metaOffset:D10} 00000 n\r\n");
        W($"trailer\n<</Size {metaObj + 1}/Root {root} 0 R{(info.Success ? info.Value : "")}{(id.Success ? id.Value : "")}/Prev {prevXref}>>\n");
        W($"startxref\n{xrefOffset}\n%%EOF\n");

        File.WriteAllBytes(pdfPath, ms.ToArray());
    }

    private static int Int(string s, string pattern)
    {
        Match m = Regex.Match(s, pattern);
        if (!m.Success) throw new InvalidDataException("PDF trailer is missing " + pattern);
        return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
