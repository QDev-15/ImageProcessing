using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocScanner.Core.Updates;

/// <summary>
/// What the update server publishes (a small JSON file next to the APK):
/// <code>{ "versionCode": 3, "versionName": "1.2", "apkUrl": "https://.../DocScanner-1.2.apk", "sha256": "…", "notes": "…" }</code>
/// <see cref="VersionCode"/> is the Android versionCode (csproj ApplicationVersion); an APK is only installed when it is
/// higher than the installed one and its SHA-256 matches.
/// </summary>
public sealed record UpdateManifest(int VersionCode, string VersionName, string ApkUrl, string? Sha256 = null, string? Notes = null);

public static class UpdatePlanner
{
    /// <summary>Hour of the day (local time) the automatic update runs: at night, when the phone is not in use.</summary>
    public const int UpdateHour = 1;

    /// <returns>Null when the text is not a usable manifest.</returns>
    public static UpdateManifest? Parse(string json)
    {
        try
        {
            UpdateManifest? m = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateManifest);
            if (m == null || m.VersionCode <= 0 || string.IsNullOrWhiteSpace(m.ApkUrl)) return null;
            if (!Uri.TryCreate(m.ApkUrl, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && !IsLocal(uri))) return null;
            return m;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Plain http only to this device itself (testing through <c>adb reverse</c>); anything else must be https.</summary>
    public static bool IsLocal(Uri uri) => uri.Scheme == "http" && (uri.Host == "127.0.0.1" || uri.Host == "localhost");

    public static bool IsNewer(UpdateManifest manifest, int installedVersionCode) => manifest.VersionCode > installedVersionCode;

    /// <summary>Time from <paramref name="nowLocal"/> to the next <see cref="UpdateHour"/>:00 (tomorrow if that hour has
    /// already begun today).</summary>
    public static TimeSpan DelayUntilNextRun(DateTime nowLocal)
    {
        DateTime next = nowLocal.Date.AddHours(UpdateHour);
        if (next <= nowLocal) next = next.AddDays(1);
        return next - nowLocal;
    }

    /// <summary>True when the file's SHA-256 is <paramref name="expectedHex"/> (any case). A manifest without a hash is
    /// accepted (the download is then only protected by https).</summary>
    public static bool HashMatches(string path, string? expectedHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex)) return true;
        using FileStream fs = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA256.HashData(fs));
        return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateManifest))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
