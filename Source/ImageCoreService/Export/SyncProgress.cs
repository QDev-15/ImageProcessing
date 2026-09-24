namespace ImageCoreService;

/// <summary>IProgress that invokes the handler synchronously on the reporting thread
/// (unlike Progress&lt;T&gt;, which posts and can reorder reports from a worker thread).
/// Use it to adapt / forward progress; the UI end marshals to its own thread.</summary>
public sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
