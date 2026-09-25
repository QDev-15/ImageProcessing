using ImageCoreService;

namespace ImageOptimizerTool;

/// <summary>
/// Background thumbnail producer for the virtual page list. The list asks for a thumbnail every
/// time it paints an item that has none yet; a request that is not repeated for a couple of
/// seconds (the item scrolled out of view) is dropped without being rendered, so the visible
/// items are always served first however long the list is. Finished thumbnails are handed to
/// <c>ready</c> on the UI thread in batches.
/// </summary>
internal sealed class ThumbnailLoader : IDisposable
{
    private const long StaleAfterMs = 2500;

    private sealed class Job(string key, PageRecord page, Size box)
    {
        public string Key { get; } = key;
        public PageRecord Page { get; } = page;
        public Size Box { get; } = box;
        public long Requested { get; set; } = Environment.TickCount64;
    }

    private readonly Func<PageRecord, Size, Bitmap> _render;
    private readonly Action<string, Bitmap> _ready;
    private readonly Control _owner;
    private readonly object _gate = new();
    private readonly LinkedList<Job> _queue = new();
    private readonly Dictionary<string, Job> _jobs = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Key, Bitmap Image)> _done = new();
    private int _drainQueued;
    private bool _disposed;

    public ThumbnailLoader(Func<PageRecord, Size, Bitmap> render, Action<string, Bitmap> ready, Control owner, int workers = 2)
    {
        _render = render;
        _ready = ready;
        _owner = owner;
        for (int i = 0; i < workers; i++)
            new Thread(Work) { IsBackground = true, Name = "thumbs-" + i, Priority = ThreadPriority.BelowNormal }.Start();
    }

    public void Request(string key, PageRecord page, Size box)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_jobs.TryGetValue(key, out Job? existing)) { existing.Requested = Environment.TickCount64; return; }
            var job = new Job(key, page, box);
            _jobs[key] = job;
            _queue.AddLast(job);
            Monitor.Pulse(_gate);
        }
    }

    /// <summary>Forgets everything still queued (thumbnail size changed, project switched).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _queue.Clear();
            _jobs.Clear();
        }
    }

    private void Work()
    {
        while (true)
        {
            Job job;
            lock (_gate)
            {
                while (_queue.Count == 0)
                {
                    if (_disposed) return;
                    Monitor.Wait(_gate);
                }
                if (_disposed) return;
                job = _queue.First!.Value;
                _queue.RemoveFirst();
                if (Environment.TickCount64 - job.Requested > StaleAfterMs)
                {
                    _jobs.Remove(job.Key); // scrolled away: it will be asked for again if it comes back
                    continue;
                }
            }

            try
            {
                Bitmap bmp = _render(job.Page, job.Box);
                _done.Enqueue((job.Key, bmp));
                ScheduleDrain();
            }
            catch (Exception ex)
            {
                Log.Warn("Thumbnail failed: " + job.Page.Source.File, ex);
            }
            finally
            {
                lock (_gate) _jobs.Remove(job.Key);
            }
        }
    }

    private void ScheduleDrain()
    {
        if (Interlocked.Exchange(ref _drainQueued, 1) != 0) return;
        try
        {
            if (_owner.IsDisposed || !_owner.IsHandleCreated) return;
            _owner.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _drainQueued, 0);
                while (_done.TryDequeue(out var item)) _ready(item.Key, item.Image);
            });
        }
        catch (InvalidOperationException) { /* form closing */ }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _queue.Clear();
            Monitor.PulseAll(_gate);
        }
        while (_done.TryDequeue(out var item)) item.Image.Dispose();
    }
}
