using System.Diagnostics;

namespace ImageCoreService;

/// <summary>
/// Wraps jbig2.exe (see tools/README.md for provenance). Two coding modes:
///   - Generic: arithmetic-coded bitonal region, no symbol reuse. Single output stream.
///   - Symbol (-s): recognizes repeated glyph shapes, stores each once in a shared
///     "globals" dictionary, then references them per page -- this is JBIG2's real
///     advantage over CCITT G4 for scanned text. Produces two streams (globals + page),
///     which map directly onto PDF's /JBIG2Decode + /DecodeParms/JBIG2Globals convention.
/// </summary>
public static class JBig2Encoder
{
    private static readonly string ExePath = Path.Combine(AppContext.BaseDirectory, "tools", "jbig2enc", "jbig2.exe");

    public sealed record Result(byte[] PageStream, byte[]? GlobalsStream);

    /// <summary>Generic region coding: one JBIG2 stream, no shared symbol dictionary.</summary>
    public static Result EncodeGeneric(string inputImagePath)
    {
        byte[] stream = RunCapturingStdout(["-p", inputImagePath]);
        return new Result(stream, null);
    }

    /// <summary>
    /// Symbol/text-region coding: recognizes repeated glyphs. threshold (0..1, default
    /// 0.92 in jbig2enc) controls how similar two marks must be to count as the same
    /// symbol -- lower is more aggressive reuse (smaller, more lossy-ish substitution
    /// risk on odd glyphs), higher is more conservative.
    /// </summary>
    public static Result EncodeSymbol(string inputImagePath, double threshold = 0.92) =>
        EncodeSymbolMultiPage([inputImagePath], threshold)[0];

    /// <summary>
    /// Symbol/text-region coding across MULTIPLE pages in one jbig2.exe invocation:
    /// glyph shapes repeated ACROSS pages (not just within one) are recognized once
    /// and shared via a single globals dictionary -- this is the real advantage of
    /// batching a whole document instead of encoding each page independently, and
    /// is exactly what a real multi-page scan/PDF benefits from. Returns one Result
    /// per input page, in order; every Result.GlobalsStream is the SAME shared bytes
    /// (embed the globals object once in the PDF and reference it from every page).
    /// </summary>
    public static Result[] EncodeSymbolMultiPage(IReadOnlyList<string> inputImagePaths, double threshold = 0.92)
    {
        if (inputImagePaths.Count == 0) throw new ArgumentException("No input pages.", nameof(inputImagePaths));

        // jbig2.exe exits 10 for a threshold outside this range (also guards NaN/stale settings).
        threshold = double.IsNaN(threshold) ? 0.92 : Math.Clamp(threshold, 0.4, 0.97);

        string workDir = CreateTempDir();
        try
        {
            string baseName = Path.Combine(workDir, "out");
            var args = new List<string>
            {
                "-s", "-p",
                "-t", threshold.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                "-b", baseName,
            };
            args.AddRange(inputImagePaths);
            RunToFiles(args.ToArray(), workDir);

            string globalsPath = baseName + ".sym";
            byte[]? globals = File.Exists(globalsPath) ? File.ReadAllBytes(globalsPath) : null;

            var results = new Result[inputImagePaths.Count];
            for (int i = 0; i < inputImagePaths.Count; i++)
            {
                string pagePath = baseName + "." + i.ToString("0000");
                if (!File.Exists(pagePath))
                    throw new InvalidOperationException($"jbig2.exe did not produce the expected page file: {pagePath}");
                results[i] = new Result(File.ReadAllBytes(pagePath), globals);
            }
            return results;
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* best effort */ }
        }
    }

    private static byte[] RunCapturingStdout(string[] args)
    {
        var psi = new ProcessStartInfo(ExePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start jbig2.exe");
        using var stdout = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(stdout);
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"jbig2.exe exited {proc.ExitCode}: {stderr}");
        return stdout.ToArray();
    }

    private static void RunToFiles(string[] args, string workDir)
    {
        var psi = new ProcessStartInfo(ExePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workDir,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start jbig2.exe");
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"jbig2.exe exited {proc.ExitCode}: {stderr}");
    }

    private static string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "jbig2enc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
