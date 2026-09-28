namespace DocScanner.Services;

/// <summary>Checking for and installing a newer version of the app from the update server (see
/// <c>DocScanner.Core.Updates.UpdateManifest</c>), by hand from the settings or automatically at 01:00.</summary>
public interface IAppUpdater
{
	/// <summary>Why updating is not possible on this install (installed from Google Play, which updates it itself), or null.</summary>
	string? UnsupportedReason { get; }

	string ServerUrl { get; set; }

	bool AutoUpdate { get; set; }

	/// <summary>The last check / install, for the settings screen ("" before the first).</summary>
	string LastStatus { get; }

	/// <summary>Checks now; when <paramref name="install"/>, downloads and installs a newer version. Returns what happened.</summary>
	Task<string> CheckAsync(bool install);

	/// <summary>Schedules the nightly check (keeping one already waiting unless <paramref name="replace"/>: settings changed),
	/// or cancels it when automatic updates are off.</summary>
	void Schedule(bool replace = false);
}
