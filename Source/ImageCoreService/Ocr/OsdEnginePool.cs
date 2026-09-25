using System.Collections.Concurrent;

namespace ImageCoreService;

/// <summary>
/// Orientation-detection engines for concurrent workers. A <see cref="OcrEngine"/> is not
/// thread-safe, and serializing every page behind one engine made auto-orient the slowest step
/// of an import; instead each worker <see cref="Rent"/>s its own engine (created on first use,
/// ~0.1 s, small: no OCR models) and <see cref="Return"/>s it afterwards.
/// </summary>
public sealed class OsdEnginePool : IDisposable
{
    private readonly ConcurrentBag<OcrEngine> _idle = new();
    private readonly List<OcrEngine> _all = new();
    private bool _disposed;
    private bool _unavailable;

    /// <summary>An engine for exclusive use, or null when orientation detection is unavailable
    /// (missing tessdata, engine failed to start); callers then skip auto-orient.</summary>
    public OcrEngine? Rent()
    {
        if (_disposed || _unavailable) return null;
        if (_idle.TryTake(out OcrEngine? engine)) return engine;
        try
        {
            engine = new OcrEngine(loadOcr: false);
            lock (_all) _all.Add(engine);
            return engine;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            Log.Warn("OSD engine unavailable; auto-orient disabled for this session", ex);
            return null;
        }
    }

    public void Return(OcrEngine engine)
    {
        if (_disposed) return;
        _idle.Add(engine);
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_all)
        {
            foreach (OcrEngine e in _all) e.Dispose();
            _all.Clear();
        }
    }
}
