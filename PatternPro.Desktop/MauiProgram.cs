using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PatternPro.DataAccess;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace PatternPro.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{

		var builder = MauiApp.CreateBuilder();
		builder.AddPatternProConfiguration();

		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.AddPatternProBackend();
		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
		builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

		var app = builder.Build();
		app.Services.MigratePatternProDatabase();
		DesktopStartup.SeedAdminUser(app.Services, builder.Configuration);

		return app;
	}
}
