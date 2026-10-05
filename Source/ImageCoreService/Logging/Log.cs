using System.Text;

namespace ImageCoreService;

/// <summary>
/// Minimal thread-safe file logger: one file per day in %LocalAppData%\ImageOptimizerTool\logs,
/// files older than 30 days removed at startup. Never throws -- logging must not break scanning.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static bool _cleaned;

    public static string CurrentFile => Path.Combine(AppPaths.LogFolder, $"app-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO ", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN ", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ').Append(level)
              .Append(" [").Append(Environment.CurrentManagedThreadId).Append("] ").Append(message);
            if (ex != null) sb.AppendLine().Append(ex);
            sb.AppendLine();

            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogFolder);
                if (!_cleaned) { _cleaned = true; CleanOld(); }
                File.AppendAllText(CurrentFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging is best effort.
        }
    }

    private static void CleanOld()
    {
        foreach (string f in Directory.EnumerateFiles(AppPaths.LogFolder, "app-*.log"))
        {
            try { if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30)) File.Delete(f); } catch { /* ignore */ }
        }
    }
}
