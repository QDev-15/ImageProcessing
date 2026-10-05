using System.Collections.Concurrent;

namespace ImageCoreService;

/// <summary>Progress of the background ingest queue: <see cref="Done"/> of <see cref="Total"/> pages.</summary>
public readonly record struct IngestProgress(int Done, int Total);

/// <summary>Outcome of one busy period of the ingest queue (raised when it drains).</summary>
public readonly record struct IngestSummary(int Total, int Blank, int Failed);

/// <summary>
/// Brings files and scanned pages into a <see cref="ScanProject"/> without making the user wait:
/// every page appears in the list at once as a <see cref="PageState.Pending"/> placeholder, and
/// background workers then analyse it (blank / crop / deskew / orientation, recorded as ops on
/// the page: no pixels are rewritten) and flip it to <see cref="PageState.Ready"/> -- or remove it if it turned out blank, or mark it
/// <see cref="PageState.Failed"/>. Results are written with <see cref="ScanProject.Update"/>,
/// so they never create undo steps and cannot resurrect a half-processed page.
/// </summary>
public sealed class PageIngestor : IDisposable
{
    private sealed record Work(string PageId, string Label);

    private readonly ScanProject _project;
    private readonly Func<AppSettings> _settings;
    private readonly Func<string?> _profileName;
    private readonly OsdEnginePool? _osd;
    private readonly BlockingCollection<Work> _queue = new();
    private CancellationTokenSource _cts = new();
    private int _pending, _total, _blank, _failed, _importing;

    public event Action<IngestProgress>? ProgressChanged;
    public event Action<IngestSummary>? Idle;

    /// <param name="osd">Orientation-detection engines, one per worker; null disables auto-orient.</param>
    public PageIngestor(ScanProject project, Func<AppSettings> settings, Func<string?> profileName,
        OsdEnginePool? osd, int degree = 1)
    {
        _project = project;
        _settings = settings;
        _profileName = profileName;
        _osd = osd;
        for (int i = 0; i < Math.Max(1, degree); i++)
            new Thread(WorkerLoop) { IsBackground = true, Name = "ingest-" + i, Priority = ThreadPriority.BelowNormal }.Start();
    }

    /// <summary>Pages queued or being processed right now.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>
    /// Reads the files and adds one placeholder per page as soon as each exists (a PDF renders
    /// page by page, so the first pages show up while the rest are still rendering). Blocks until
    /// every file has been read; processing continues in the background afterwards.
    /// Returns the number of pages added. Run it on a worker thread.
    /// </summary>
    public int Import(IReadOnlyList<string> files, int? insertAt, IProgress<WorkProgress>? progress, CancellationToken cancel)
    {
        Interlocked.Increment(ref _importing); // keeps the queue from reporting "idle" between two files
        try { return ImportCore(files, insertAt, progress, cancel); }
        finally
        {
            Interlocked.Decrement(ref _importing);
            RaiseIdleIfDrained();
        }
    }

    private int ImportCore(IReadOnlyList<string> files, int? insertAt, IProgress<WorkProgress>? progress, CancellationToken cancel)
    {
        int added = 0;
        bool first = true;
        int? at = insertAt;

        for (int f = 0; f < files.Count; f++)
        {
            cancel.ThrowIfCancellationRequested();
            string name = Path.GetFileName(files[f]);
            bool isPdf = string.Equals(Path.GetExtension(files[f]), ".pdf", StringComparison.OrdinalIgnoreCase);
            progress?.Report(new WorkProgress(f, files.Count, $"Đọc {name}..."));

            if (isPdf)
            {
                // A PDF is not rendered at import: it is copied into the project once and every
                // page becomes a source of its own (file + page number), rendered on demand.
                List<PageRecord> pdfPages = AddPdf(files[f], name, at, first, cancel);
                first = false;
                if (at != null) at += pdfPages.Count;
                added += pdfPages.Count;
                foreach (PageRecord rec in pdfPages) Enqueue(rec);
                continue;
            }

            int n = 0;
            PageRecord? firstOfFile = null;
            PageImporter.Import(files[f], _project.PagesFolder, onPage: path =>
            {
                cancel.ThrowIfCancellationRequested();
                n++;
                string label = isPdf || n > 1 ? $"{name} #{n}" : name;
                if (n == 2 && firstOfFile != null) // a multi-frame file: number its first page too
                    _project.Update(firstOfFile.Id, r => r with { Label = $"{name} #1" });
                PageRecord rec = PageRecord.FromFile(path, label) with { State = PageState.Pending };
                _project.AddPage(rec, at, undoable: first);
                first = false;
                if (at != null) at++;
                if (n == 1) firstOfFile = rec;
                added++;
                Enqueue(rec);
                progress?.Report(new WorkProgress(f, files.Count, $"Đọc {name} (trang {n})..."));
            });
        }
        return added;
    }

    /// <summary>Adds one scanned page (a file the scanner just wrote) and queues it.</summary>
    public void AddScanned(string path, string label)
    {
        PageRecord rec = PageRecord.FromFile(path, label) with { State = PageState.Pending };
        _project.AddPage(rec, null, undoable: true);
        Enqueue(rec);
    }

    /// <summary>Drops every page still waiting for processing (the user cancelled the import).</summary>
    public void CancelPending() => _cts.Cancel();

    private void Enqueue(PageRecord rec)
    {
        Interlocked.Increment(ref _total);
        Interlocked.Increment(ref _pending);
        _queue.Add(new Work(rec.Id, rec.Label));
        ProgressChanged?.Invoke(new IngestProgress(_total - Pending, _total));
    }

    private void WorkerLoop()
    {
        foreach (Work w in _queue.GetConsumingEnumerable())
        {
            try { ProcessOne(w); }
            catch (Exception ex) { Log.Error("Ingest worker failed for " + w.Label, ex); }
            finally { Finished(); }
        }
    }

    private void Finished()
    {
        int left = Interlocked.Decrement(ref _pending);
        ProgressChanged?.Invoke(new IngestProgress(_total - left, _total));
        RaiseIdleIfDrained();
    }

    private readonly object _idleGate = new();

    /// <summary>Raises <see cref="Idle"/> once the queue is empty AND no import is still feeding it.</summary>
    private void RaiseIdleIfDrained()
    {
        IngestSummary summary;
        lock (_idleGate)
        {
            if (Volatile.Read(ref _pending) != 0 || Volatile.Read(ref _importing) != 0 || _total == 0) return;
            summary = new IngestSummary(_total, _blank, _failed);
            _total = _blank = _failed = 0;
            if (_cts.IsCancellationRequested) _cts = new CancellationTokenSource();
        }
        Idle?.Invoke(summary);
    }

    private List<PageRecord> AddPdf(string pdf, string name, int? at, bool undoable, CancellationToken cancel)
    {
        string copy = Path.Combine(_project.PagesFolder, Guid.NewGuid().ToString("N") + ".pdf");
        File.Copy(pdf, copy);
        try
        {
            int count = PdfPageRenderer.PageCount(copy);
            double?[] native = PdfPageRenderer.NativeDpis(copy);
            var pages = new List<PageRecord>(count);
            for (int i = 0; i < count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                double dpi = i < native.Length ? native[i] ?? 0 : 0;
                pages.Add(new PageRecord(PageRecord.NewId(), new PageSource(copy, i, dpi), PageOps.None, PageState.Pending, $"{name} #{i + 1}"));
            }
            _project.AddPages(pages, at, undoable);
            return pages;
        }
        catch
        {
            try { File.Delete(copy); } catch { /* best effort */ }
            throw;
        }
    }

    private void ProcessOne(Work w)
    {
        PageRecord? rec = _project.Find(w.PageId);
        if (rec == null) return; // deleted while queued

        if (_cts.IsCancellationRequested)
        {
            _project.Discard(new[] { w.PageId });
            return;
        }

        using var perf = Perf.Scope("ingest.page");
        try
        {
            // A scanned page arrives in pages\_incoming: make it a regular project file first.
            PageSource source = rec.Source with { File = MoveOutOfIncoming(rec.Source.File) };

            AppSettings s = _settings();
            AnalysisResult result = new(false, PageOps.None, "");
            if (s.AutoProcessOnImport)
            {
                var options = PageProcessingOptions.FromSettings(s);
                OcrEngine? osd = s.AutoOrient ? _osd?.Rent() : null;
                try { result = PageAnalysis.Analyze(_project.Cache, source, options, osd); }
                finally { if (osd != null) _osd!.Return(osd); }
            }

            if (result.IsBlank)
            {
                Interlocked.Increment(ref _blank);
                Log.Info($"Blank page skipped: {w.Label}");
                _project.Discard(new[] { w.PageId });
                return;
            }
            string label = result.Summary.Length > 0 ? $"{w.Label} ({result.Summary})" : w.Label;
            _project.Update(w.PageId, r => r with { Source = source, Ops = result.Ops, Label = label, State = PageState.Ready, Error = null });
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);
            Log.Error("Processing page failed: " + w.Label, ex);
            // The page keeps its (unprocessed) source, so it can still be looked at and retried.
            _project.Update(w.PageId, r => r with { State = PageState.Failed, Error = ex.Message });
        }
    }

    /// <summary>Scanner output lands in pages\_incoming; a finished page belongs in pages\.</summary>
    private string MoveOutOfIncoming(string file)
    {
        if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "_incoming", StringComparison.OrdinalIgnoreCase)) return file;
        string final = Path.Combine(_project.PagesFolder, Path.GetFileName(file));
        File.Move(file, final, overwrite: true);
        return final;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.CompleteAdding();
    }
}
