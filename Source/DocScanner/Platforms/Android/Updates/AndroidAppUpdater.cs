using Android.App;
using Android.App.Job;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using DocScanner.Core.Updates;
using Application = Android.App.Application;

namespace DocScanner.Services;

/// <summary>
/// Updates the app from its own server (the app is installed from an APK, not from Google Play):
/// <list type="number">
/// <item>read the manifest (<see cref="UpdateManifest"/>) at <see cref="ServerUrl"/>;</item>
/// <item>if its versionCode is higher than the installed one, download the APK and check its SHA-256;</item>
/// <item>install it with PackageInstaller. On Android 12+ an app may update itself without asking once it is the
/// "installer of record" of the installed version, i.e. from the second self-update on (the first one, over an APK
/// installed another way, shows the system's install confirmation; also the one-time "allow installing apps from this
/// source").</item>
/// </list>
/// Automatic updates run every night at <see cref="UpdatePlanner.UpdateHour"/>:00 (JobScheduler, any network, survives
/// reboots). Installed from Google Play, updating is left to Play (its policy forbids apps updating themselves).
/// </summary>
public sealed class AndroidAppUpdater : IAppUpdater
{
	private const string UrlKey = "update_url", AutoKey = "update_auto", StatusKey = "update_status";
	private const int JobId = 0x5550;
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

	private static Context Context => Application.Context;

	public string? UnsupportedReason
	{
		get
		{
			try
			{
				string? installer = Build.VERSION.SdkInt >= BuildVersionCodes.R
					? Context.PackageManager!.GetInstallSourceInfo(Context.PackageName!).InstallingPackageName
#pragma warning disable CA1422
					: Context.PackageManager!.GetInstallerPackageName(Context.PackageName!);
#pragma warning restore CA1422
				return installer == "com.android.vending" ? "Ứng dụng cài từ Google Play: Google Play tự cập nhật." : null;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public string ServerUrl
	{
		get => Preferences.Default.Get(UrlKey, "");
		set => Preferences.Default.Set(UrlKey, value.Trim());
	}

	public bool AutoUpdate
	{
		get => Preferences.Default.Get(AutoKey, true);
		set => Preferences.Default.Set(AutoKey, value);
	}

	public string LastStatus => Preferences.Default.Get(StatusKey, "");

	public Task<string> CheckAsync(bool install) => RunAsync(install, CancellationToken.None);

	public void Schedule(bool replace = false) => ScheduleNight(replace);

	/// <summary>Installed versionCode.</summary>
	public static long InstalledVersionCode()
	{
		PackageInfo info = Context.PackageManager!.GetPackageInfo(Context.PackageName!, 0)!;
		return Build.VERSION.SdkInt >= BuildVersionCodes.P ? info.LongVersionCode :
#pragma warning disable CA1422
			info.VersionCode;
#pragma warning restore CA1422
	}

	/// <summary>One check (and install). Used by the settings screen and by the nightly job.</summary>
	public static async Task<string> RunAsync(bool install, CancellationToken ct)
	{
		string status = await RunCoreAsync(install, ct);
		Preferences.Default.Set(StatusKey, $"{DateTime.Now:dd/MM/yyyy HH:mm} · {status}");
		Android.Util.Log.Info("DocScanPerf", "update: " + status);
		return status;
	}

	private static async Task<string> RunCoreAsync(bool install, CancellationToken ct)
	{
		var self = new AndroidAppUpdater();
		if (self.UnsupportedReason is { } reason) return reason;
		string url = self.ServerUrl;
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != "https" && !UpdatePlanner.IsLocal(uri)))
			return "Chưa cấu hình địa chỉ cập nhật (https).";

		UpdateManifest? manifest;
		try
		{
			manifest = UpdatePlanner.Parse(await Http.GetStringAsync(uri, ct));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return "Không kết nối được máy chủ cập nhật: " + ex.Message;
		}
		if (manifest == null) return "Tệp cập nhật trên máy chủ không hợp lệ.";
		long installed = InstalledVersionCode();
		if (!UpdatePlanner.IsNewer(manifest, (int)installed)) return $"Đang dùng bản mới nhất ({AppInfo.Current.VersionString}).";
		if (!install) return $"Có bản mới {manifest.VersionName}.";

		string folder = Path.Combine(Context.CacheDir!.AbsolutePath, "update");
		Directory.CreateDirectory(folder);
		foreach (string old in Directory.EnumerateFiles(folder)) TryDelete(old);
		string apk = Path.Combine(folder, $"DocScanner-{manifest.VersionCode}.apk");
		try
		{
			using HttpResponseMessage response = await Http.GetAsync(manifest.ApkUrl, HttpCompletionOption.ResponseHeadersRead, ct);
			response.EnsureSuccessStatusCode();
			await using (FileStream fs = File.Create(apk))
				await response.Content.CopyToAsync(fs, ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
		{
			TryDelete(apk);
			return "Không tải được bản cập nhật: " + ex.Message;
		}
		if (!UpdatePlanner.HashMatches(apk, manifest.Sha256))
		{
			TryDelete(apk);
			return "Bản cập nhật tải về bị lỗi (sai mã SHA-256), đã huỷ.";
		}

		Install(apk);
		return $"Đang cài bản {manifest.VersionName}...";
	}

	/// <summary>Hands the APK to PackageInstaller; the outcome arrives at <see cref="UpdateStatusReceiver"/>.</summary>
	private static void Install(string apk)
	{
		PackageInstaller installer = Context.PackageManager!.PackageInstaller;
		var parameters = new PackageInstaller.SessionParams(PackageInstallMode.FullInstall);
		parameters.SetAppPackageName(Context.PackageName);
		if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
			parameters.SetRequireUserAction((int)PackageInstallUserAction.NotRequired);
		int id = installer.CreateSession(parameters);
		using PackageInstaller.Session session = installer.OpenSession(id);
		long length = new FileInfo(apk).Length;
		using (Stream output = session.OpenWrite("base.apk", 0, length)!)
		using (FileStream input = File.OpenRead(apk))
		{
			input.CopyTo(output);
			session.Fsync(output);
		}
		var intent = new Intent(Context, typeof(UpdateStatusReceiver));
		PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0);
		PendingIntent pending = PendingIntent.GetBroadcast(Context, id, intent, flags)!;
		session.Commit(pending.IntentSender!);
	}

	/// <summary>The nightly job at the next 01:00 (a job already waiting is kept unless <paramref name="replace"/>), or none
	/// when automatic updates are off.</summary>
	public static void ScheduleNight(bool replace = false)
	{
		var scheduler = (JobScheduler)Context.GetSystemService(Context.JobSchedulerService)!;
		var self = new AndroidAppUpdater();
		if (!self.AutoUpdate || self.UnsupportedReason != null || string.IsNullOrWhiteSpace(self.ServerUrl))
		{
			scheduler.Cancel(JobId);
			return;
		}
		// Rescheduling an id replaces the job, and stops it if it is running: at every app start, keep a job already
		// waiting (or running right now) unless the settings changed (replace).
		if (!replace && scheduler.GetPendingJob(JobId) != null) return;
		long delay = (long)UpdatePlanner.DelayUntilNextRun(DateTime.Now).TotalMilliseconds;
		JobInfo job = new JobInfo.Builder(JobId, new ComponentName(Context, Java.Lang.Class.FromType(typeof(UpdateJobService))))
			.SetMinimumLatency(delay)
			.SetOverrideDeadline(delay + (long)TimeSpan.FromHours(3).TotalMilliseconds)
			.SetRequiredNetworkType(NetworkType.Any)
			.SetPersisted(true)
			.Build()!;
		scheduler.Schedule(job);
	}

	private static void TryDelete(string path)
	{
		try { File.Delete(path); } catch (IOException) { }
	}
}

/// <summary>The nightly update check (JobScheduler). Schedules the next night when done.</summary>
[Service(Name = "btk.docscanner.UpdateJobService", Permission = "android.permission.BIND_JOB_SERVICE", Exported = true)]
public sealed class UpdateJobService : JobService
{
	private CancellationTokenSource? _cts;

	public override bool OnStartJob(JobParameters? parameters)
	{
		_cts = new CancellationTokenSource();
		CancellationToken ct = _cts.Token;
		_ = Task.Run(async () =>
		{
			try
			{
				await AndroidAppUpdater.RunAsync(install: true, ct);
			}
			catch (Exception ex)
			{
				Android.Util.Log.Warn("DocScanPerf", "update job: " + ex);
			}
			finally
			{
				JobFinished(parameters, false);
				AndroidAppUpdater.ScheduleNight(replace: true); // tomorrow night (this job has finished)
			}
		});
		return true; // working in the background
	}

	public override bool OnStopJob(JobParameters? parameters)
	{
		_cts?.Cancel();
		return false; // tomorrow night instead (scheduled in the finally above)
	}
}

/// <summary>Result of an update install. If Android asks the user to confirm (the first self-update, or "allow this
/// source"), the confirmation is shown, or offered as a notification when the app is in the background.</summary>
[BroadcastReceiver(Exported = false)]
public sealed class UpdateStatusReceiver : BroadcastReceiver
{
	private const string Channel = "updates";

	public override void OnReceive(Context? context, Intent? intent)
	{
		if (context == null || intent == null) return;
		var status = (PackageInstallStatus)intent.GetIntExtra(PackageInstaller.ExtraStatus, -999);
		string message = intent.GetStringExtra(PackageInstaller.ExtraStatusMessage) ?? "";
		switch (status)
		{
			case PackageInstallStatus.PendingUserAction:
#pragma warning disable CA1422
				var confirm = intent.GetParcelableExtra(Intent.ExtraIntent) as Intent;
#pragma warning restore CA1422
				if (confirm == null) return;
				confirm.AddFlags(ActivityFlags.NewTask);
				Notify(context, confirm);
				try { context.StartActivity(confirm); } catch (Exception) { } // may be refused from the background
				Record("Chờ xác nhận cài bản cập nhật.");
				break;
			case PackageInstallStatus.Success:
				Record("Đã cài bản cập nhật.");
				break;
			default:
				Record("Không cài được bản cập nhật: " + message);
				break;
		}
	}

	private static void Record(string status) =>
		Preferences.Default.Set("update_status", $"{DateTime.Now:dd/MM/yyyy HH:mm} · {status}");

	private static void Notify(Context context, Intent confirm)
	{
		var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
		if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
			manager.CreateNotificationChannel(new NotificationChannel(Channel, "Cập nhật ứng dụng", NotificationImportance.Default));
		PendingIntent open = PendingIntent.GetActivity(context, 0, confirm, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
#pragma warning disable CA1422
		Notification notification = (Build.VERSION.SdkInt >= BuildVersionCodes.O ? new Notification.Builder(context, Channel) : new Notification.Builder(context))
#pragma warning restore CA1422
			.SetSmallIcon(Android.Resource.Drawable.StatSysDownloadDone)
			.SetContentTitle("Có bản cập nhật Doc Scanner")
			.SetContentText("Chạm để cài đặt.")
			.SetContentIntent(open)
			.SetAutoCancel(true)
			.Build()!;
		manager.Notify(0x5551, notification);
	}
}
