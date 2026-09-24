using System.Text;
using System.Text.RegularExpressions;

namespace ImageCoreService;

/// <summary>
/// Output file naming from a pattern, e.g. "Scan_{date}_{time}_{counter}".
/// Tokens: {date}=yyyyMMdd {time}=HHmmss {yyyy} {MM} {dd} {HH} {mm} {ss}
/// {counter} (001, 002... per export batch; {counter:0000} for another width)
/// {barcode} (separator barcode, empty if none) {profile} (scan profile name).
/// Unknown tokens are left as typed; characters illegal in Windows file names are
/// replaced with '_'.
/// </summary>
public static partial class FileNamer
{
    [GeneratedRegex(@"\{(\w+)(?::([^}]*))?\}")]
    private static partial Regex TokenRegex();

    public static string Build(string pattern, DateTime time, int counter, string? barcode = null, string? profile = null)
    {
        string name = TokenRegex().Replace(pattern, m =>
        {
            string fmt = m.Groups[2].Success ? m.Groups[2].Value : "";
            return m.Groups[1].Value.ToLowerInvariant() switch
            {
                "date" => time.ToString(fmt.Length > 0 ? fmt : "yyyyMMdd"),
                "time" => time.ToString(fmt.Length > 0 ? fmt : "HHmmss"),
                "yyyy" => time.ToString("yyyy"),
                "mm" when m.Groups[1].Value == "MM" => time.ToString("MM"),
                "dd" => time.ToString("dd"),
                "hh" => time.ToString("HH"),
                "mm" => time.ToString("mm"),
                "ss" => time.ToString("ss"),
                "counter" => counter.ToString(fmt.Length > 0 ? fmt : "000"),
                "barcode" => barcode ?? "",
                "profile" => profile ?? "",
                _ => m.Value,
            };
        });
        return Sanitize(name);
    }

    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        string s = sb.ToString().Trim().TrimEnd('.');
        return s.Length == 0 ? "Scan" : s;
    }

    /// <summary>folder\name.ext, or folder\name_2.ext, _3... if taken.</summary>
    public static string UniquePath(string folder, string name, string extension)
    {
        string path = Path.Combine(folder, name + extension);
        for (int i = 2; File.Exists(path); i++)
            path = Path.Combine(folder, $"{name}_{i}{extension}");
        return path;
    }
}
