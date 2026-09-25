using DocScanner.Services;
using DocScanner.ViewModels;
using DocScanner.Views;
using Microsoft.Extensions.Logging;

namespace DocScanner;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<PermissionService>();

		builder.Services.AddTransient<HomeViewModel>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<PageViewerPage>();
		builder.Services.AddTransient<CropPage>();
		builder.Services.AddTransient<ExportPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
