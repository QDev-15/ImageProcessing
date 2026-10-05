using System.Diagnostics;

namespace ImageCoreService;

/// <summary>
/// Stage timing: <c>using (Perf.Scope("import.render")) { ... }</c> logs "PERF import.render 123 ms"
/// and adds to a per-name total that the benchmark (Source/Bench) prints as a summary.
/// Cheap enough to leave on: a Stopwatch and one dictionary update per stage.
/// </summary>
public static class Perf
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (long Ms, int Count)> Totals = new();

    /// <summary>Also write each measurement to the log file (off by default: hundreds of lines per import).</summary>
    public static bool LogEachScope { get; set; }

    public static IDisposable Scope(string name) => new Timer(name);

    public static T Measure<T>(string name, Func<T> body)
    {
        using (Scope(name)) return body();
    }

    public static void Measure(string name, Action body)
    {
        using (Scope(name)) body();
    }

    public static IReadOnlyDictionary<string, (long Ms, int Count)> Snapshot()
    {
        lock (Gate) return new Dictionary<string, (long, int)>(Totals);
    }

    public static void Reset()
    {
        lock (Gate) Totals.Clear();
    }

    private sealed class Timer(string name) : IDisposable
    {
        private readonly long _start = Stopwatch.GetTimestamp();

        public void Dispose()
        {
            long ms = (long)Stopwatch.GetElapsedTime(_start).TotalMilliseconds;
            lock (Gate)
            {
                Totals.TryGetValue(name, out var t);
                Totals[name] = (t.Ms + ms, t.Count + 1);
            }
            if (LogEachScope) Log.Info($"PERF {name} {ms} ms");
        }
    }
}
