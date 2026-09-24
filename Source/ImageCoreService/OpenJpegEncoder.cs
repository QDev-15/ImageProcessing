using System.Diagnostics;

namespace ImageCoreService;

/// <summary>
/// Wraps opj_compress.exe (see tools/README.md for provenance). Unlike CoreJ2K (the
/// .NET library the main app currently uses for JPEG2000), OpenJPEG's -r flag is a
/// REAL rate-distortion-optimized target compression ratio -- CoreJ2K's equivalent
/// (WithBitrate) was found to be a no-op this session (see Jp2Encoder.cs's remarks
/// in the main OpenImaging project). This lets you target a size directly instead of
/// hunting for a quantization step size that happens to land near it.
/// </summary>
public static class OpenJpegEncoder
{
    private static readonly string ExePath = Path.Combine(AppContext.BaseDirectory, "tools", "openjpeg", "opj_compress.exe");

    /// <summary>
    /// Encodes to JP2. <paramref name="compressionRatio"/>: e.g. 10 means "compress
    /// to roughly 1/10th of the raw/lossless size" (opj_compress's own -r semantics).
    /// Pass null for lossless.
    /// </summary>
    public static byte[] Encode(string inputImagePath, double? compressionRatio)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "openjpeg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            string outPath = Path.Combine(workDir, "out.jp2");
            var args = new List<string> { "-i", inputImagePath, "-o", outPath };
            if (compressionRatio.HasValue)
                args.AddRange(["-r", compressionRatio.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);

            var psi = new ProcessStartInfo(ExePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string a in args) psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start opj_compress.exe");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0 || !File.Exists(outPath))
                throw new InvalidOperationException($"opj_compress.exe exited {proc.ExitCode}: {stdout}\n{stderr}");

            return File.ReadAllBytes(outPath);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { /* best effort */ }
        }
    }
}
