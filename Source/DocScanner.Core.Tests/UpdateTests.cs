using DocScanner.Core.Updates;

namespace DocScanner.Core.Tests;

public class UpdateTests
{
    [Fact]
    public void A_manifest_is_read_and_compared_by_version_code()
    {
        UpdateManifest m = UpdatePlanner.Parse("""{ "versionCode": 3, "versionName": "1.2", "apkUrl": "https://example.com/a.apk", "sha256": "AB" }""")!;
        Assert.Equal((3, "1.2", "https://example.com/a.apk"), (m.VersionCode, m.VersionName, m.ApkUrl));
        Assert.True(UpdatePlanner.IsNewer(m, 2));
        Assert.False(UpdatePlanner.IsNewer(m, 3));
    }

    [Theory]
    [InlineData("""{ "versionCode": 3, "apkUrl": "http://example.com/a.apk" }""")]   // plain http from the internet
    [InlineData("""{ "versionCode": 0, "apkUrl": "https://example.com/a.apk" }""")]
    [InlineData("""{ "versionCode": 3 }""")]
    [InlineData("not json")]
    public void Bad_manifests_are_refused(string json) => Assert.Null(UpdatePlanner.Parse(json));

    [Fact]
    public void Plain_http_is_allowed_only_to_the_device_itself() =>
        Assert.NotNull(UpdatePlanner.Parse("""{ "versionCode": 3, "versionName": "t", "apkUrl": "http://127.0.0.1:8080/a.apk" }"""));

    [Theory]
    [InlineData("2026-09-27 20:00", 5.0)]   // evening: tonight at 01:00
    [InlineData("2026-09-28 00:30", 0.5)]
    [InlineData("2026-09-28 01:00", 24.0)]  // the hour has begun: tomorrow
    [InlineData("2026-09-28 09:15", 15.75)]
    public void The_update_runs_at_one_in_the_night(string now, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), UpdatePlanner.DelayUntilNextRun(DateTime.Parse(now)));

    [Fact]
    public void The_download_must_match_its_hash()
    {
        using var root = new TempRoot();
        Directory.CreateDirectory(root.Path);
        string file = Path.Combine(root.Path, "a.apk");
        File.WriteAllText(file, "abc");
        const string sha = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"; // SHA-256("abc")
        Assert.True(UpdatePlanner.HashMatches(file, sha.ToLowerInvariant()));
        Assert.False(UpdatePlanner.HashMatches(file, "00" + sha[2..]));
        Assert.True(UpdatePlanner.HashMatches(file, null));
    }
}
