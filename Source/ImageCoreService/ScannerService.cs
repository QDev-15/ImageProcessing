using NTwain.Data;

namespace ImageCoreService;

/// <summary>A scan source from either driver model.</summary>
public sealed class ScanSource
{
    public required string Name { get; init; }
    public required ScanDriverPreference Driver { get; init; } // Twain or Wia
    internal TWIdentityWrapper? Twain { get; init; }
    internal WiaScanner.Source? Wia { get; init; }

    public override string ToString() => $"{Name} ({(Driver == ScanDriverPreference.Twain ? "TWAIN" : "WIA")})";
}

/// <summary>
/// One entry point for scanning: lists TWAIN and / or WIA sources according to the
/// preference (Auto = TWAIN, falling back to WIA when no TWAIN driver / source exists),
/// starts a scan with a <see cref="ScanProfile"/>, cancels it. Must be created and used on
/// the UI thread (TWAIN callbacks are marshalled to the SynchronizationContext it captures).
/// </summary>
public sealed class ScannerService : IDisposable
{
    private TwainScanner? _twain;
    private bool _twainUnavailable;
    private WiaScanner? _activeWia;
    private TwainScanner? _activeTwain;

    public bool IsScanning { get; private set; }

    public IReadOnlyList<ScanSource> GetSources(ScanDriverPreference preference)
    {
        var result = new List<ScanSource>();
        if (preference is ScanDriverPreference.Auto or ScanDriverPreference.Twain)
            result.AddRange(GetTwainSources());
        if (preference == ScanDriverPreference.Wia || (preference == ScanDriverPreference.Auto && result.Count == 0))
            result.AddRange(GetWiaSources());
        return result;
    }

    private IEnumerable<ScanSource> GetTwainSources()
    {
        try
        {
            TwainScanner? t = EnsureTwain();
            if (t == null) return Array.Empty<ScanSource>();
            return t.GetSources().Select(s => new ScanSource { Name = s.ProductName.ToString(), Driver = ScanDriverPreference.Twain, Twain = s }).ToList();
        }
        catch (Exception ex)
        {
            Log.Warn("Listing TWAIN sources failed", ex);
            return Array.Empty<ScanSource>();
        }
    }

    private static IEnumerable<ScanSource> GetWiaSources()
    {
        try
        {
            if (!WiaScanner.IsAvailable) return Array.Empty<ScanSource>();
            return WiaScanner.GetSources().Select(s => new ScanSource { Name = s.Name, Driver = ScanDriverPreference.Wia, Wia = s }).ToList();
        }
        catch (Exception ex)
        {
            Log.Warn("Listing WIA sources failed", ex);
            return Array.Empty<ScanSource>();
        }
    }

    /// <summary>Lazily opens the TWAIN Data Source Manager (fails quietly when no TWAIN
    /// DSM is installed -- WIA then takes over in Auto mode).</summary>
    private TwainScanner? EnsureTwain()
    {
        if (_twain != null || _twainUnavailable) return _twain;
        var t = new TwainScanner();
        STS sts = t.OpenDsm();
        if (!sts.IsSuccess)
        {
            Log.Warn($"TWAIN DSM unavailable ({sts.RC}); WIA fallback will be used.");
            t.Dispose();
            _twainUnavailable = true;
            return null;
        }
        _twain = t;
        return t;
    }

    public void StartScan(ScanSource source, ScanProfile profile, string destFolder,
        Action<string> onPageScanned, Action<Exception> onError, Action onFinished)
    {
        if (IsScanning) throw new InvalidOperationException("Đang scan.");
        IsScanning = true;
        Log.Info($"Scan start: {source} dpi={profile.Dpi} color={profile.ColorMode} duplex={profile.Duplex} feeder={profile.UseFeeder}");
        void Finished()
        {
            IsScanning = false;
            _activeTwain = null;
            _activeWia = null;
            Log.Info("Scan finished");
            onFinished();
        }

        if (source.Driver == ScanDriverPreference.Twain && source.Twain != null && _twain != null)
        {
            _activeTwain = _twain;
            _twain.StartScan(source.Twain, profile, destFolder, onPageScanned, onError, Finished);
        }
        else if (source.Wia != null)
        {
            _activeWia = new WiaScanner();
            _activeWia.StartScan(source.Wia, profile, destFolder, onPageScanned, onError, Finished);
        }
        else
        {
            IsScanning = false;
            throw new InvalidOperationException("Nguồn scan không hợp lệ.");
        }
    }

    public void Cancel()
    {
        Log.Info("Scan cancel requested");
        _activeTwain?.Cancel();
        _activeWia?.Cancel();
    }

    public void Dispose() => _twain?.Dispose();
}
