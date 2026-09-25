using DocScanner.Views;

namespace DocScanner;

public partial class AppShell : Shell
{
	public static class Routes
	{
		public const string Viewer = "viewer";
		public const string Crop = "crop";
		public const string Export = "export";
	}

	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(Routes.Viewer, typeof(PageViewerPage));
		Routing.RegisterRoute(Routes.Crop, typeof(CropPage));
		Routing.RegisterRoute(Routes.Export, typeof(ExportPage));
	}
}
