using System.Drawing;
using System.Drawing.Imaging;
using NTwain;
using NTwain.Data;
using NTwain.Events;

namespace ImageCoreService;

public enum ScanColorMode { BlackAndWhite, Gray, Color }

/// <summary>
/// Thin wrapper around NTwain (v4 branch -- the same underlying TWAIN library the main
/// app's IMIP.Imaging.Capture.dll is itself built on) for real scanner testing in this
/// tool. TwainAppSession runs its own internal Win32 message pump on a dedicated STA
/// thread, so no IMessageFilter/message-loop integration is needed here (unlike NTwain
/// 3.x).
///
/// DPI handling follows the lesson from the real scan-pipeline bug found and fixed in
/// the main app 2026-09-24 (Goal.md): NEVER guess/re-derive DPI from a saved file's own
/// tags after the fact. Here that means reading TW_IMAGEINFO.XResolution/YResolution --
/// the value the DRIVER actually used for this specific transfer, reported at transfer
/// time -- and setting it explicitly on the saved Bitmap, rather than trusting whatever
/// GDI+ happens to auto-detect from the native transfer buffer (which may or may not
/// carry a resolution tag depending on the driver).
/// </summary>
public sealed class TwainScanner : IDisposable
{
    private readonly TwainAppSession _twain;
    private string _destFolder = "";
    private Action<string>? _onPageScanned;
    private Action<Exception>? _onError;
    private Action? _onFinished;

    public event Action<STATE>? StateChanged;

    public TwainScanner()
    {
        _twain = new TwainAppSession(appThreadContext: SynchronizationContext.Current);
        _twain.TransferReady += OnTransferReady;
        _twain.Transferred += OnTransferred;
        _twain.TransferError += OnTransferError;
        _twain.SourceDisabled += OnSourceDisabled;
        _twain.StateChanged += (s, state) => StateChanged?.Invoke(state);
    }

    public STS OpenDsm() => _twain.OpenDsm();

    public IReadOnlyList<TWIdentityWrapper> GetSources() => _twain.GetSources().ToList();

    /// <summary>
    /// Opens <paramref name="source"/>, negotiates resolution + color mode, and starts a
    /// scan. Each captured page is saved as its own file into <paramref name="destFolder"/>
    /// (TIFF for B&amp;W/gray, JPEG for color) and reported via <paramref name="onPageScanned"/>.
    /// All callbacks run on the UI thread (via the SynchronizationContext captured in the
    /// constructor). A feeder (ADF) scan may call <paramref name="onPageScanned"/> multiple
    /// times before <paramref name="onFinished"/> fires.
    /// </summary>
    public void StartScan(TWIdentityWrapper source, int dpi, ScanColorMode colorMode, bool duplex, bool showDriverUi,
        string destFolder, Action<string> onPageScanned, Action<Exception> onError, Action onFinished)
    {
        _destFolder = destFolder;
        _onPageScanned = onPageScanned;
        _onError = onError;
        _onFinished = onFinished;

        _twain.TryStepdown(STATE.S3);

        STS openSts = _twain.OpenSource(source);
        if (!openSts.IsSuccess)
        {
            onError(new InvalidOperationException($"Could not open scan source: {openSts.RC}"));
            return;
        }

        // Negotiate resolution + color mode BEFORE enabling (state 4, per TWAIN spec --
        // capability negotiation happens between OpenSource and EnableSource).
        // NTwain's SetCap<TValue> infers TValue from the argument's STATIC type, before
        // any implicit conversion runs -- passing a bare `double` resolves TValue=double,
        // which ValueWriter.GetItemType doesn't support (throws NotSupportedException:
        // "Double is not supported for writing."). Must pass TW_FIX32 explicitly so
        // TValue is inferred as TW_FIX32 (which IS a supported TWTY.FIX32 container type).
        _twain.SetCap(CAP.ICAP_XRESOLUTION, (TW_FIX32)(double)dpi);
        _twain.SetCap(CAP.ICAP_YRESOLUTION, (TW_FIX32)(double)dpi);
        _twain.SetCap(CAP.ICAP_PIXELTYPE, colorMode switch
        {
            ScanColorMode.BlackAndWhite => TWPT.BW,
            ScanColorMode.Gray => TWPT.GRAY,
            _ => TWPT.RGB,
        });

        // Explicit, not left to the driver's own default: found via a real test where a
        // 3-sheet simplex feed produced 6 images because the scanner's default was
        // duplex ON, with no way in this tool to tell it otherwise (2026-09-24).
        // CAP_DUPLEXENABLED is TW_BOOL -- an enum, not TW_FIX32 -- so this one already
        // resolves via typeof(TW_BOOL) in ValueWriter.GetItemType without needing an
        // explicit-cast workaround the way the resolution FIX32 caps did.
        _twain.SetCap(CAP.CAP_DUPLEXENABLED, duplex ? TW_BOOL.True : TW_BOOL.False);

        STS enableSts = _twain.EnableSource(showDriverUi ? SourceEnableOption.ShowUI : SourceEnableOption.NoUI);
        if (!enableSts.IsSuccess)
            onError(new InvalidOperationException($"Could not start scan: {enableSts.RC}"));
    }

    public void StopScan() => _twain.DisableSource();

    private void OnTransferReady(TwainAppSession sender, TransferReadyEventArgs e)
    {
        // No cancellation logic needed for this tool -- accept every pending page.
    }

    private void OnTransferred(TwainAppSession sender, TransferredEventArgs e)
    {
        if (e.Data == null)
            return;

        try
        {
            // Authoritative DPI for THIS transfer -- what the driver actually used, not
            // what we merely requested (a driver may snap to its nearest supported
            // value). See the class doc comment for why this matters.
            double dpiX = e.ImageInfo.XResolution;
            double dpiY = e.ImageInfo.YResolution;
            if (dpiX <= 0) dpiX = 200;
            if (dpiY <= 0) dpiY = 200;

            using var img = (Bitmap)Image.FromStream(e.Data.AsStream());
            img.SetResolution((float)dpiX, (float)dpiY);

            bool bitonalOrGray = img.PixelFormat == PixelFormat.Format1bppIndexed
                              || img.PixelFormat == PixelFormat.Format8bppIndexed;
            string ext = bitonalOrGray ? ".tif" : ".jpg";
            string path = Path.Combine(_destFolder, Guid.NewGuid().ToString("N") + ext);

            if (bitonalOrGray)
                img.Save(path, ImageFormat.Tiff);
            else
                img.Save(path, ImageFormat.Jpeg);

            _onPageScanned?.Invoke(path);
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
        // NOT disposing e.Data here: TransferredEventArgs auto-disposes it once this
        // handler returns, unless TakeDataOwnership() was called (it wasn't -- this
        // handler processes synchronously).
    }

    private void OnTransferError(TwainAppSession sender, TransferErrorEventArgs e)
    {
        _onError?.Invoke(e.Exception ?? new InvalidOperationException($"Transfer error: {e.Code}"));
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
