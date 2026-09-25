namespace ImageCoreService;

/// <summary>
/// Reads the pages of the project with Tesseract in the background, at low priority, while the
/// user works, and stores the words in the project's <see cref="OcrCache"/>. Exporting then only
/// has to assemble the PDF. It works only while nothing else is busy (<c>canRun</c>), waits a
/// moment after every change so a burst of edits does not start a pass, skips pages already in the
/// cache, and stops after the page in hand when paused (an export is starting) or when the page
/// list / settings change under it.
/// </summary>
public sealed class BackgroundOcr : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    private readonly Func<ScanProject?> _project;
    private readonly Func<AppSettings> _settings;
    private readonly Func<string?> _profile;
    private readonly Func<bool> _canRun;
    private readonly SemaphoreSlim _wake = new(0);
    private readonly Thread _thread;
    private volatile bool _paused, _disposed;
    private long _lastSignal = Environment.TickCount64;

    /// <summary>Pages read so far / pages this pass has to read. (0, 0) when idle.</summary>
    public event Action<int, int>? ProgressChanged;

    /// <param name="project">The project to read; may return null while none is open yet (the app
    /// can be sitting in a startup dialog when the first pass wakes up).</param>
    public BackgroundOcr(Func<ScanProject?> project, Func<AppSettings> settings, Func<string?> profile, Func<bool> canRun)
    {
        _project = project;
        _settings = settings;
        _profile = profile;
        _canRun = canRun;
        _thread = new Thread(Loop) { IsBackground = true, Name = "ocr-background", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    /// <summary>Something changed (pages, ops, settings): look for unread pages soon.</summary>
    public void Signal()
    {
        Interlocked.Exchange(ref _lastSignal, Environment.TickCount64);
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    /// <summary>Stop reading (after the current page) until <see cref="Resume"/>.</summary>
    public void Pause() => _paused = true;

    public void Resume()
    {
        _paused = false;
        Signal();
    }

    private void Loop()
    {
        while (!_disposed)
        {
            _wake.Wait(TimeSpan.FromSeconds(5)); // also re-checks periodically (canRun may have turned true)
            if (_disposed) return;
            while (!_disposed && Environment.TickCount64 - Interlocked.Read(ref _lastSignal) < Settle.TotalMilliseconds)
                Thread.Sleep(200); // let a burst of edits settle first
            try { Pass(); }
            catch (Exception ex) { Log.Warn("Background OCR pass failed", ex); }
        }
    }

    private void Pass()
    {
        ScanProject? project = _project();
        if (project == null) return;
        AppSettings settings = _settings();
        if (!settings.Ocr || _paused || !_canRun()) return;
        if (!OcrEngine.IsLanguageAvailable(settings.OcrLanguages)) return;

        OcrCache cache = project.OcrCache;
        ExportOptions options = ExportOptions.FromSettings(settings, _profile());
        List<PageRecord> todo = project.Pages
            .Where(p => p.State == PageState.Ready && !cache.Contains(OcrCache.Key(p, options)))
            .ToList();
        if (todo.Count == 0) return;

        long stamp = Interlocked.Read(ref _lastSignal);
        OcrEngine? engine = null;
        Log.Info($"Background OCR: {todo.Count} page(s) to read");
        try
        {
            int done = 0;
            ProgressChanged?.Invoke(0, todo.Count);
            foreach (PageRecord page in todo)
            {
                // A newer change means the todo list may be stale: start a fresh pass instead.
                if (_disposed || _paused || !_canRun() || Interlocked.Read(ref _lastSignal) != stamp)
                {
                    Log.Info($"Background OCR: pass stopped after {done}/{todo.Count} (paused={_paused}, canRun={_canRun()}, changed={Interlocked.Read(ref _lastSignal) != stamp})");
                    break;
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                engine ??= new OcrEngine(settings.OcrLanguages);
                Log.Info($"Background OCR: reading '{page.Label}' (engine ready after {sw.ElapsedMilliseconds} ms)");
                try
                {
                    using (Perf.Scope("ocr.background"))
                        cache.Put(OcrCache.Key(page, options), DocumentExporter.RecognizePage(page, options, engine));
                }
                catch (Exception ex)
                {
                    Log.Warn("Background OCR failed for " + page.Label, ex);
                }
                Log.Info($"Background OCR: '{page.Label}' done in {sw.ElapsedMilliseconds} ms");
                ProgressChanged?.Invoke(++done, todo.Count);
            }
        }
        finally
        {
            engine?.Dispose();
            ProgressChanged?.Invoke(0, 0);
        }
        if (Interlocked.Read(ref _lastSignal) != stamp) _wake.Release(); // something changed meanwhile
    }

    public void Dispose()
    {
        _disposed = true;
        _wake.Release();
    }
}
