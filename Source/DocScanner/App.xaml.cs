namespace DocScanner;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Core.Perf.Log("startup: CreateWindow");
		// The start screen first; it builds the shell (main screen) while it animates, then hands over.
		return new Window(new Views.SplashPage(() =>
		{
			var shell = new AppShell();
			Core.Perf.Log("startup: AppShell created");
			return shell;
		}));
	}

	protected override void OnStart()
	{
		base.OnStart();
		// The nightly update check (re)scheduled at every start: survives settings changes and app updates.
		try { _services.GetService<Services.IAppUpdater>()?.Schedule(); }
		catch (Exception ex) { Core.Perf.Log("update schedule failed: " + ex.Message); }
	}
}
