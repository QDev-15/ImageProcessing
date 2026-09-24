using System.Drawing;
using System.Runtime.InteropServices;

namespace ImageCoreService;

/// <summary>
/// WIA 2.0 scanning (fallback for machines / devices without a TWAIN driver) through the
/// Windows "WIA Automation Library" (wiaaut.dll, part of Windows since Vista) via late-bound
/// COM -- no interop assembly to ship. All WIA calls run on a dedicated STA thread so the UI
/// stays responsive; callbacks are marshalled back through the captured SynchronizationContext.
/// </summary>
public sealed class WiaScanner
{
    // WIA property IDs (wiadef.h)
    private const int WIA_DPS_DOCUMENT_HANDLING_SELECT = 3088;
    private const int WIA_DPS_PAGES = 3096;
    private const int WIA_IPS_XRES = 6147;
    private const int WIA_IPS_YRES = 6148;
    private const int WIA_IPS_CUR_INTENT = 6146;
    private const int FEEDER = 1, FLATBED = 2, DUPLEX = 4;
    private const int WIA_INTENT_COLOR = 1, WIA_INTENT_GRAY = 2, WIA_INTENT_TEXT = 4;
    private const string WiaFormatPng = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
    private const string WiaFormatBmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";

    // WIA HRESULTs
    private const int WIA_ERROR_PAPER_JAM = unchecked((int)0x80210002);
    private const int WIA_ERROR_PAPER_EMPTY = unchecked((int)0x80210003);
    private const int WIA_ERROR_BUSY = unchecked((int)0x80210006);
    private const int WIA_ERROR_OFFLINE = unchecked((int)0x80210005);
    private const int WIA_ERROR_COVER_OPEN = unchecked((int)0x80210016);
    private const int WIA_ERROR_DOUBLE_FEED = unchecked((int)0x8021000D);

    private volatile bool _cancelRequested;

    public sealed record Source(string Id, string Name);

    public static bool IsAvailable => Type.GetTypeFromProgID("WIA.DeviceManager") != null;

    public static IReadOnlyList<Source> GetSources()
    {
        var list = new List<Source>();
        RunSta(() =>
        {
            dynamic dm = CreateDeviceManager();
            foreach (dynamic info in dm.DeviceInfos)
            {
                if ((int)info.Type != 1) continue; // ScannerDeviceType
                list.Add(new Source((string)info.DeviceID, (string)GetProp(info.Properties, "Name")));
            }
        });
        return list;
    }

    public void Cancel() => _cancelRequested = true;

    /// <summary>
    /// Scans all pages (feeder until empty, or one flatbed page). Same callback contract as
    /// <see cref="TwainScanner.StartScan"/>: onPageScanned per page, onError for problems
    /// (a <see cref="ScanException"/> for jam / double feed...), onFinished exactly once.
    /// </summary>
    public void StartScan(Source source, ScanProfile profile, string destFolder,
        Action<string> onPageScanned, Action<Exception> onError, Action onFinished)
    {
        _cancelRequested = false;
        SynchronizationContext? ui = SynchronizationContext.Current;
        void Post(Action a) { if (ui != null) ui.Post(_ => a(), null); else a(); }
        Directory.CreateDirectory(destFolder);

        var thread = new Thread(() =>
        {
            try
            {
                dynamic dm = CreateDeviceManager();
                dynamic? info = null;
                foreach (dynamic i in dm.DeviceInfos)
                    if ((string)i.DeviceID == source.Id) { info = i; break; }
                if (info == null) throw new InvalidOperationException("Không tìm thấy máy scan WIA: " + source.Name);

                dynamic device = info.Connect();
                bool feeder = profile.UseFeeder;
                if (feeder)
                {
                    TrySetProp(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT, FEEDER | (profile.Duplex ? DUPLEX : 0));
                    TrySetProp(device.Properties, WIA_DPS_PAGES, 1);
                }
                else
                {
                    TrySetProp(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT, FLATBED);
                }

                int pageNo = 0;
                while (!_cancelRequested)
                {
                    dynamic item = device.Items[1];
                    TrySetProp(item.Properties, WIA_IPS_CUR_INTENT, profile.ColorMode switch
                    {
                        ScanColorMode.BlackAndWhite => WIA_INTENT_TEXT,
                        ScanColorMode.Gray => WIA_INTENT_GRAY,
                        _ => WIA_INTENT_COLOR,
                    });
                    TrySetProp(item.Properties, WIA_IPS_XRES, profile.Dpi);
                    TrySetProp(item.Properties, WIA_IPS_YRES, profile.Dpi);

                    dynamic image;
                    try
                    {
                        image = TransferLossless(item);
                    }
                    catch (COMException ex) when (ex.HResult == WIA_ERROR_PAPER_EMPTY)
                    {
                        if (pageNo == 0) throw new ScanException(ScanErrorKind.NoPaper, ScanException.Describe(ScanErrorKind.NoPaper), ex);
                        break; // feeder emptied: normal end of batch
                    }

                    byte[] bytes = (byte[])image.FileData.BinaryData;
                    using (var ms = new MemoryStream(bytes))
                    using (var bmp = new Bitmap(ms))
                    {
                        // WIA reports the resolution it actually used in the image object.
                        double dx = (double)image.HorizontalResolution, dy = (double)image.VerticalResolution;
                        bmp.SetResolution((float)(dx > 0 ? dx : profile.Dpi), (float)(dy > 0 ? dy : profile.Dpi));
                        string path = ImageUtils.SaveLossless(bmp, Path.Combine(destFolder, Guid.NewGuid().ToString("N")));
                        Post(() => onPageScanned(path));
                    }
                    pageNo++;
                    if (!feeder) break; // flatbed: one page per scan
                }
            }
            catch (Exception ex)
            {
                Exception reported = ex is COMException com ? Translate(com) : ex;
                Log.Error("WIA scan failed", ex);
                Post(() => onError(reported));
            }
            finally
            {
                Post(onFinished);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>PNG if the driver supports it, else BMP -- never JPEG.</summary>
    private static dynamic TransferLossless(dynamic item)
    {
        try { return item.Transfer(WiaFormatPng); }
        catch (COMException ex) when (ex.HResult != WIA_ERROR_PAPER_EMPTY && ex.HResult != WIA_ERROR_PAPER_JAM)
        {
            return item.Transfer(WiaFormatBmp);
        }
    }

    private static Exception Translate(COMException ex)
    {
        ScanErrorKind kind = ex.HResult switch
        {
            WIA_ERROR_PAPER_JAM => ScanErrorKind.PaperJam,
            WIA_ERROR_DOUBLE_FEED => ScanErrorKind.DoubleFeed,
            WIA_ERROR_PAPER_EMPTY => ScanErrorKind.NoPaper,
            WIA_ERROR_COVER_OPEN => ScanErrorKind.CoverOpen,
            WIA_ERROR_BUSY => ScanErrorKind.DeviceBusy,
            WIA_ERROR_OFFLINE => ScanErrorKind.DeviceOffline,
            _ => ScanErrorKind.Other,
        };
        return new ScanException(kind, ScanException.Describe(kind) + $" (WIA 0x{ex.HResult:X8})", ex);
    }

    private static dynamic CreateDeviceManager()
    {
        Type t = Type.GetTypeFromProgID("WIA.DeviceManager")
            ?? throw new InvalidOperationException("Máy này không có WIA (wiaaut.dll).");
        return Activator.CreateInstance(t)!;
    }

    private static object? GetProp(dynamic props, string name)
    {
        foreach (dynamic p in props)
            if ((string)p.Name == name) return p.Value;
        return null;
    }

    private static void TrySetProp(dynamic props, int id, object value)
    {
        try
        {
            foreach (dynamic p in props)
            {
                if ((int)p.PropertyID != id) continue;
                p.Value = value;
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"WIA: property {id} = {value} not accepted; using device default.", ex);
        }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) throw error;
    }
}
