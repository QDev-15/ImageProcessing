namespace DocScanner.Core;

/// <summary>Something about a page changed (state, thumbnail, outline...); the UI re-reads it.</summary>
public sealed record PageUpdate(string DocId, string PageId);

/// <summary>
/// Background pipeline that turns freshly added pages (state <see cref="PageState.Pending"/>) into
/// finished ones, in three stages of falling urgency:
///  1. thumbnail (a heavily sub-sampled decode, tens of ms): every page of a batch gets its
///     thumbnail before anything else happens, so the screen fills in almost at once;
///  2. screen proxy (a bigger decode): the page becomes openable;
///  3. paper-outline detection (the slowest): pages get their outline last.
/// A stage is only started when no page is waiting for an earlier one. Two workers keep a
/// multi-core phone busy without holding many decoded bitmaps in RAM at once. Progress is saved
/// in doc.json after every step, so <see cref="ResumePending"/> can continue after the app was
/// killed mid-batch.
/// </summary>
public sealed class PageIngestQueue
{
    public const int ThumbEdge = 512;
    public const int ProxyEdge = 1600;

    private enum Stage { Render, Thumb, Proxy, Detect }

    private readonly record struct Job(string DocId, string PageId, Stage Stage);

    private readonly DocumentStore _store;
    private readonly IImageService _images;
    private readonly CropDetectionService? _detection;
    private readonly CropRenderService? _render;
    private readonly int _maxWorkers;

    private readonly object _lock = new();
    private readonly Queue<Job> _renders = new(), _thumbs = new(), _proxies = new(), _detects = new();
    private readonly Dictionary<string, int> _jobsPerPage = [];
    private int _running;
    private int _outstanding;
    private TaskCompletionSource _idle = NewCompleted();

    public PageIngestQueue(DocumentStore store, IImageService images, CropDetectionService? detection = null, int workers = 2, CropRenderService? render = null)
    {
        _store = store;
        _images = images;
        _detection = detection;
        _render = render;
        _maxWorkers = Math.Max(1, workers);
    }

    /// <summary>Raised on a worker thread after any change to a page.</summary>
    public event Action<PageUpdate>? PageUpdated;

    /// <summary>Queued plus running jobs.</summary>
    public int Outstanding
    {
        get { lock (_lock) return _outstanding; }
    }

    /// <summary>True while any job for the page is queued or running.</summary>
    public bool IsBusy(string pageId)
    {
        lock (_lock) return _jobsPerPage.ContainsKey(pageId);
    }

    /// <summary>Completes when nothing is queued or running (mainly for tests).</summary>
    public Task WaitIdleAsync()
    {
        lock (_lock) return _idle.Task;
    }

    /// <summary>Straightens the page from the original photo. The user is waiting for it (they just
    /// pressed Done), so it goes ahead of everything else in the queue.</summary>
    public void EnqueueRender(string docId, string pageId) => Add(new Job(docId, pageId, Stage.Render));

    /// <summary>Starts the pipeline for a page that was just added.
    public void Enqueue(string docId, string pageId) => Add(new Job(docId, pageId, Stage.Thumb));

    /// <summary>Picks up pages left unfinished by an earlier run (app killed, crash): pending ones start
    /// over, half-done ones continue, and finished pages that never got an outline get one.</summary>
    public void ResumePending()
    {
        foreach (DocumentRecord doc in _store.List())
        {
            foreach (PageRecord page in _store.Pages(doc.Id))
            {
                if (IsBusy(page.Id)) continue;
                switch (page.State)
                {
                    case PageState.Pending: Add(new Job(doc.Id, page.Id, Stage.Thumb)); break;
                    case PageState.Preview: Add(new Job(doc.Id, page.Id, Stage.Proxy)); break;
                    case PageState.Ready when page.CropQuad == null && _detection != null:
                        Add(new Job(doc.Id, page.Id, Stage.Detect));
                        break;
                }
            }
        }
    }

    private void Add(Job job)
    {
        lock (_lock)
        {
            if (_outstanding == 0) _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _outstanding++;
            _jobsPerPage[job.PageId] = _jobsPerPage.GetValueOrDefault(job.PageId) + 1;
            (job.Stage switch { Stage.Render => _renders, Stage.Thumb => _thumbs, Stage.Proxy => _proxies, _ => _detects }).Enqueue(job);

            if (_running < _maxWorkers)
            {
                _running++;
                _ = Task.Run(WorkerAsync);
            }
        }
    }

    private bool TryDequeue(out Job job)
    {
        // Strict priority: nothing starts a later stage while an earlier one has work waiting.
        foreach (Queue<Job> q in new[] { _renders, _thumbs, _proxies, _detects })
            if (q.TryDequeue(out job)) return true;
        job = default;
        return false;
    }

    private async Task WorkerAsync()
    {
        while (true)
        {
            Job job;
            lock (_lock)
            {
                if (!TryDequeue(out job))
                {
                    _running--;
                    return;
                }
            }

            try
            {
                await RunAsync(job);
            }
            catch (Exception ex)
            {
                // RunAsync handles the expected failures itself; this only keeps the worker alive.
                Fail(job, ex);
            }
            finally
            {
                lock (_lock)
                {
                    if (--_jobsPerPage[job.PageId] == 0) _jobsPerPage.Remove(job.PageId);
                    if (--_outstanding == 0) _idle.TrySetResult();
                }
            }
        }
    }

    private async Task RunAsync(Job job)
    {
        PageRecord? page = _store.Pages(job.DocId).FirstOrDefault(p => p.Id == job.PageId);
        if (page == null) return; // deleted while queued

        switch (job.Stage)
        {
            case Stage.Thumb when page.State == PageState.Pending:
            {
                ImageInfo info = await _images.CreateThumbAsync(
                    _store.OriginalPath(job.DocId, page), _store.ThumbPath(job.DocId, page), ThumbEdge, page.UserRotation, CancellationToken.None);
                int orientation = ImageGeometry.ComposeRotation(info.ExifOrientation, page.UserRotation);
                (int uw, int uh) = ImageGeometry.UprightSize(info.RawWidth, info.RawHeight, orientation);
                (int pw, int ph) = ImageGeometry.FitLongEdge(uw, uh, ProxyEdge);
                bool kept = Change(job, p =>
                {
                    p.RawWidth = info.RawWidth;
                    p.RawHeight = info.RawHeight;
                    p.ExifOrientation = info.ExifOrientation;
                    p.ProxyWidth = pw;
                    p.ProxyHeight = ph;
                    p.State = PageState.Preview;
                });
                if (kept) Add(new Job(job.DocId, job.PageId, Stage.Proxy));
                break;
            }

            case Stage.Proxy when page.State == PageState.Preview:
            {
                await _images.CreateProxyAsync(_store.OriginalPath(job.DocId, page), _store.ProxyPath(job.DocId, page),
                    ProxyEdge, page.EffectiveOrientation, CancellationToken.None);
                bool kept = Change(job, p => p.State = PageState.Ready);
                // A page that already has an outline (rotated by the user, or edited by hand) keeps it.
                if (kept && _detection != null && page.CropQuad == null) Add(new Job(job.DocId, job.PageId, Stage.Detect));
                break;
            }

            case Stage.Render when _render != null && page.State == PageState.Ready && page.NeedsRender:
                try
                {
                    await _render.RenderAsync(job.DocId, job.PageId);
                }
                catch (Exception ex)
                {
                    Change(job, p => p.RenderError = ex.Message);
                    break;
                }
                Raise(job);
                break;

            case Stage.Detect when _detection != null && page.State == PageState.Ready && page.CropQuad == null:
                try
                {
                    await _detection.DetectAsync(job.DocId, job.PageId);
                }
                catch (Exception)
                {
                    // The outline is a convenience: the page works without it and gets it again when opened.
                }
                Raise(job);
                break;
        }
    }

    /// <summary>Applies a change to the page under the store lock; false when the page is gone.</summary>
    private bool Change(Job job, Action<PageRecord> change)
    {
        bool found = false;
        bool docExists = _store.Update(job.DocId, d =>
        {
            PageRecord? p = d.Pages.FirstOrDefault(x => x.Id == job.PageId);
            if (p == null) return;
            found = true;
            change(p);
        });
        if (docExists && found) Raise(job);
        return docExists && found;
    }

    private void Fail(Job job, Exception ex) =>
        Change(job, p =>
        {
            // Only the decode stages can fail a page; a failed detection just leaves it without an outline.
            if (job.Stage == Stage.Detect) return;
            if (job.Stage == Stage.Render) { p.RenderError = ex.Message; return; }
            p.State = PageState.Failed;
            p.Error = ex.Message;
        });

    private void Raise(Job job) => PageUpdated?.Invoke(new PageUpdate(job.DocId, job.PageId));

    private static TaskCompletionSource NewCompleted()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }
}
