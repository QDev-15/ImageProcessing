using System.Xml;
using System.Xml.Serialization;

namespace ImageCoreService;

/// <summary>Per-user data locations.</summary>
public static class AppPaths
{
    public static string DataFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageOptimizerTool");

    public static string SettingsFile => Path.Combine(DataFolder, "settings.xml");
    public static string LogFolder => Path.Combine(DataFolder, "logs");

    /// <summary>Shipped next to the exe; copied to <see cref="SettingsFile"/> on first run.</summary>
    public static string DefaultSettingsFile => Path.Combine(AppContext.BaseDirectory, "Config", "settings.default.xml");
}

/// <summary>
/// Loads / saves <see cref="AppSettings"/> as XML. The user file lives in
/// %LocalAppData% (the install folder may be read-only); on first run it is seeded from
/// Config\settings.default.xml shipped with the app, or from built-in defaults if that is
/// missing. A corrupt user file is backed up and replaced instead of crashing the app.
/// </summary>
public static class SettingsStore
{
    private static readonly XmlSerializer Serializer = new(typeof(AppSettings));

    public static AppSettings Load()
    {
        string user = AppPaths.SettingsFile;
        if (File.Exists(user))
        {
            try
            {
                return Read(user);
            }
            catch (Exception ex)
            {
                Log.Error($"Settings file is unreadable, falling back to defaults: {user}", ex);
                try { File.Copy(user, user + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { /* best effort */ }
            }
        }

        AppSettings settings = LoadDefaults();
        Save(settings);
        Log.Info("Settings initialized from defaults -> " + user);
        return settings;
    }

    public static AppSettings LoadDefaults()
    {
        string def = AppPaths.DefaultSettingsFile;
        if (File.Exists(def))
        {
            try { return Read(def); }
            catch (Exception ex) { Log.Error("Default settings file is unreadable: " + def, ex); }
        }
        var s = new AppSettings();
        s.Normalize();
        return s;
    }

    public static void Save(AppSettings settings) => Write(settings, AppPaths.SettingsFile);

    public static void Write(AppSettings settings, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        using (var w = XmlWriter.Create(tmp, new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) }))
            Serializer.Serialize(w, settings);
        File.Move(tmp, path, overwrite: true); // atomic-ish: never leave a half-written settings file
    }

    private static AppSettings Read(string path)
    {
        using var r = XmlReader.Create(path);
        var s = (AppSettings)(Serializer.Deserialize(r) ?? throw new InvalidDataException("Empty settings file."));
        s.Normalize();
        return s;
    }
}
