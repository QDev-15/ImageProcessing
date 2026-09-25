using DocScanner.Views;

namespace DocScanner;

public partial class AppShell : Shell
{
	public static class Routes
	{
		public const string Document = "document";
		public const string Crop = "crop";
		public const string Result = "result";
		public const string Export = "export";
	}

	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(Routes.Document, typeof(DocumentPage));
		Routing.RegisterRoute(Routes.Crop, typeof(CropPage));
		Routing.RegisterRoute(Routes.Result, typeof(ResultPage));
		Routing.RegisterRoute(Routes.Export, typeof(ExportPage));
	}
}
