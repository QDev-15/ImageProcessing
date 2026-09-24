using System.Drawing;
using NTwain;
using NTwain.Data;
using NTwain.Events;

namespace ImageCoreService;

public enum ScanColorMode { BlackAndWhite, Gray, Color }

public enum ScanErrorKind { Other, PaperJam, DoubleFeed, NoPaper, CoverOpen, DeviceBusy, DeviceOffline }

/// <summary>Scan failure with a user-actionable category (paper jam, double feed...).</summary>
public sealed class ScanException(ScanErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public ScanErrorKind Kind { get; } = kind;

    public static string Describe(ScanErrorKind kind) => kind switch
    {
        ScanErrorKind.PaperJam => "Kẹt giấy. Gỡ giấy bị kẹt khỏi máy scan rồi scan tiếp các trang còn lại.",
        ScanErrorKind.DoubleFeed => "Máy scan kéo 2 tờ cùng lúc (double feed). Kiểm tra lại tờ vừa scan và scan lại.",
        ScanErrorKind.NoPaper => "Khay nạp giấy trống. Đặt giấy vào khay rồi scan lại.",
        ScanErrorKind.CoverOpen => "Nắp máy scan đang mở.",
        ScanErrorKind.DeviceBusy => "Máy scan đang bận.",
        ScanErrorKind.DeviceOffline => "Máy scan không kết nối / đang tắt.",
        _ => "Lỗi khi scan.",
    };
}

/// <summary>
/// Thin wrapper around NTwain (v4 branch -- the same underlying TWAIN library the main
/// app's IMIP.Imaging.Capture.dll is itself built on). TwainAppSession runs its own
/// internal Win32 message pump on a dedicated STA thread, so no message-loop integration
/// is needed here (unlike NTwain 3.x).
///
/// DPI: taken from TW_IMAGEINFO.XResolution/YResolution -- what the DRIVER actually used
/// for this transfer -- never guessed from the saved file afterwards (see the 2026-09-24
/// scan-pipeline DPI bug in the main app).
///
/// Quality: every page is saved LOSSLESSLY (1bpp -> TIFF G4, gray / color -> PNG). The only
/// lossy step is the export codec, so color scans are never JPEG-compressed twice.
/// </summary>
public sealed class TwainScanner : IDisposable
{
    private readonly TwainAppSession _twain;
    private string _destFolder = "";
    private Action<string>? _onPageScanned;
    private Action<Exception>? _onError;
    private Action? _onFinished;
    private volatile bool _cancelRequested;
    private ScanErrorKind? _deviceProblem;

    public event Action<STATE>? StateChanged;

    public TwainScanner()
    {
        _twain = new TwainAppSession(appThreadContext: SynchronizationContext.Current);
        _twain.TransferReady += OnTransferReady;
        _twain.Transferred += OnTransferred;
        _twain.TransferError += OnTransferError;
        _twain.SourceDisabled += OnSourceDisabled;
        _twain.DeviceEvent += (_, e) => OnDeviceEvent(e);
        _twain.StateChanged += (s, state) => StateChanged?.Invoke(state);
    }

    public STS OpenDsm() => _twain.OpenDsm();

    public IReadOnlyList<TWIdentityWrapper> GetSources() => _twain.GetSources().ToList();

    /// <summary>
    /// Opens <paramref name="source"/>, negotiates the profile's capabilities and starts a
    /// scan. Each page is saved as its own file into <paramref name="destFolder"/> and
    /// reported via <paramref name="onPageScanned"/>. All callbacks run on the UI thread
    /// (SynchronizationContext captured in the constructor). A feeder scan calls
    /// <paramref name="onPageScanned"/> several times before <paramref name="onFinished"/>.
    /// Errors are reported as <see cref="ScanException"/> where the cause is known.
    /// </summary>
    public void StartScan(TWIdentityWrapper source, ScanProfile profile, string destFolder,
        Action<string> onPageScanned, Action<Exception> onError, Action onFinished)
    {
        _destFolder = destFolder;
        _onPageScanned = onPageScanned;
        _onError = onError;
        _onFinished = onFinished;
        _cancelRequested = false;
        _deviceProblem = null;
        Directory.CreateDirectory(destFolder);

        _twain.TryStepdown(STATE.S3);

        STS openSts = _twain.OpenSource(source);
        if (!openSts.IsSuccess)
        {
            onError(new InvalidOperationException($"Không mở được máy scan: {openSts.RC}"));
            onFinished();
            return;
        }

        // Capability negotiation happens in state 4 (between OpenSource and EnableSource).
        // NTwain's SetCap<TValue> infers TValue from the argument's STATIC type: resolution
        // must be passed as TW_FIX32 explicitly (a bare double throws NotSupportedException).
        TrySetCap("resolution X", () => _twain.SetCap(CAP.ICAP_XRESOLUTION, (TW_FIX32)(double)profile.Dpi));
        TrySetCap("resolution Y", () => _twain.SetCap(CAP.ICAP_YRESOLUTION, (TW_FIX32)(double)profile.Dpi));
        TrySetCap("pixel type", () => _twain.SetCap(CAP.ICAP_PIXELTYPE, profile.ColorMode switch
        {
            ScanColorMode.BlackAndWhite => TWPT.BW,
            ScanColorMode.Gray => TWPT.GRAY,
            _ => TWPT.RGB,
        }));
        // Explicit, never left to the driver default: a 3-sheet simplex feed once produced
        // 6 images because the scanner defaulted to duplex ON (2026-09-24).
        TrySetCap("duplex", () => _twain.SetCap(CAP.CAP_DUPLEXENABLED, profile.Duplex ? TW_BOOL.True : TW_BOOL.False));
        TrySetCap("feeder", () => _twain.SetCap(CAP.CAP_FEEDERENABLED, profile.UseFeeder ? TW_BOOL.True : TW_BOOL.False));
        if (profile.UseFeeder)
            TrySetCap("autofeed", () => _twain.SetCap(CAP.CAP_AUTOFEED, TW_BOOL.True));

        STS enableSts = _twain.EnableSource(profile.ShowDriverUi ? SourceEnableOption.ShowUI : SourceEnableOption.NoUI);
        if (!enableSts.IsSuccess)
        {
            onError(new InvalidOperationException($"Không bắt đầu scan được: {enableSts.RC}"));
            _twain.CloseSource();
            onFinished();
        }
    }

    /// <summary>Stops after the page currently being transferred (pending pages are discarded).</summary>
    public void Cancel()
    {
        _cancelRequested = true;
        try { _twain.DisableSource(); } catch (Exception ex) { Log.Warn("TWAIN DisableSource during cancel failed", ex); }
    }

    private static void TrySetCap(string what, Func<STS> set)
    {
        try
        {
            STS sts = set();
            if (!sts.IsSuccess) Log.Warn($"TWAIN: driver refused {what} ({sts.RC}/{sts.ConditionCode}); using driver default.");
        }
        catch (Exception ex)
        {
            Log.Warn($"TWAIN: setting {what} failed; using driver default.", ex);
        }
    }

    private void OnTransferReady(TwainAppSession sender, TransferReadyEventArgs e)
    {
        if (_cancelRequested) e.Cancel = CancelType.EndNow;
    }

    private void OnTransferred(TwainAppSession sender, TransferredEventArgs e)
    {
        if (e.Data == null) return;
        try
        {
            // Authoritative DPI for THIS transfer (a driver may snap to its nearest supported value).
            double dpiX = e.ImageInfo.XResolution;
            double dpiY = e.ImageInfo.YResolution;
            if (dpiX <= 0) dpiX = 200;
            if (dpiY <= 0) dpiY = dpiX;

            using var img = (Bitmap)Image.FromStream(e.Data.AsStream());
            img.SetResolution((float)dpiX, (float)dpiY);
            string path = ImageUtils.SaveLossless(img, Path.Combine(_destFolder, Guid.NewGuid().ToString("N")));
            _onPageScanned?.Invoke(path);
        }
        catch (Exception ex)
        {
            Log.Error("TWAIN: saving a transferred page failed", ex);
            _onError?.Invoke(ex);
        }
        // NOT disposing e.Data: TransferredEventArgs disposes it after this handler returns.
    }

    private void OnTransferError(TwainAppSession sender, TransferErrorEventArgs e)
    {
        string code = e.Code is STS s ? $"{s.RC}/{s.ConditionCode}" : "n/a";
        ScanErrorKind kind = _deviceProblem ?? Classify(code);
        Log.Error($"TWAIN transfer error: {code} ({kind})", e.Exception);
        _onError?.Invoke(kind == ScanErrorKind.Other && e.Exception != null
            ? e.Exception
            : new ScanException(kind, ScanException.Describe(kind) + $" ({code})", e.Exception));
    }

    /// <summary>Device events (paper jam, double feed...) arrive as TWDE values.</summary>
    private void OnDeviceEvent(object? e)
    {
        string text = e?.GetType().GetProperty("Event")?.GetValue(e)?.ToString() ?? e?.ToString() ?? "";
        ScanErrorKind kind = Classify(text);
        Log.Info("TWAIN device event: " + text);
        if (kind != ScanErrorKind.Other) _deviceProblem = kind;
    }

    private static ScanErrorKind Classify(string code)
    {
        code = code.ToUpperInvariant();
        if (code.Contains("PAPERJAM")) return ScanErrorKind.PaperJam;
        if (code.Contains("DOUBLEFEED")) return ScanErrorKind.DoubleFeed;
        if (code.Contains("NOMEDIA") || code.Contains("PAPEREMPTY") || code.Contains("NOPAPER")) return ScanErrorKind.NoPaper;
        if (code.Contains("COVER")) return ScanErrorKind.CoverOpen;
        if (code.Contains("BUSY")) return ScanErrorKind.DeviceBusy;
        if (code.Contains("OFFLINE") || code.Contains("NOTONLINE") || code.Contains("POWER")) return ScanErrorKind.DeviceOffline;
        return ScanErrorKind.Other;
    }

    private void OnSourceDisabled(TwainAppSession sender, TWIdentityWrapper e)
    {
        _twain.CloseSource();
        _onFinished?.Invoke();
    }

    public void Dispose()
    {
        _twain.TryStepdown(STATE.S2);
        _twain.Dispose();
    }
}
