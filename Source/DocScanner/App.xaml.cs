namespace DocScanner;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Core.Perf.Log("startup: CreateWindow");
		var shell = new AppShell();
		Core.Perf.Log("startup: AppShell created");
		return new Window(shell);
	}
}
