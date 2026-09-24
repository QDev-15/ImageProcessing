using System.Windows.Forms;
using ImageCoreService;

namespace ImageOptimizerTool;

internal static class Program
{
    /// <summary>
    /// Command line:
    ///   [FILES...]                      start the app (and import the given images / PDFs)
    ///   --write-default-settings PATH   write built-in default settings XML to PATH and exit
    ///                                   (source of Config\settings.default.xml)
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--write-default-settings")
        {
            var defaults = new AppSettings();
            defaults.Normalize();
            SettingsStore.Write(defaults, args[1]);
            return 0;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) Log.Error("Unhandled (fatal)", ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("Unobserved task exception", e.Exception); e.SetObserved(); };

        ApplicationConfiguration.Initialize();
        Log.Info($"App start {typeof(Program).Assembly.GetName().Version} on {Environment.OSVersion}");

        AppSettings settings = SettingsStore.Load();
        Application.Run(new MainForm(settings, args));
        return 0;
    }

    private static void ReportCrash(Exception ex)
    {
        Log.Error("Unhandled UI exception", ex);
        MessageBox.Show($"Đã xảy ra lỗi không mong muốn:\n{ex.Message}\n\nChi tiết đã được ghi vào log:\n{Log.CurrentFile}",
            "Image Optimizer Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
