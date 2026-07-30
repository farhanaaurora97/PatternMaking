using Microsoft.Extensions.Configuration;

namespace PatternPro.Desktop;

internal static class DesktopConfiguration
{
    public static void AddPatternProConfiguration(this MauiAppBuilder builder)
    {
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Team.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Empty local Postgres → try shared Team DB, then repo Pattern.Web settings.
        if (!HasPostgres(builder.Configuration))
            TryLoadBesideExe("appsettings.Team.json", builder.Configuration);

        if (!HasPostgres(builder.Configuration))
            TryLoadPatternWebDevSettings(builder.Configuration);
    }

    private static bool HasPostgres(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config.GetConnectionString("Postgres"));

    private static void TryLoadBesideExe(string fileName, ConfigurationManager config)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(path))
            return;

        config.AddJsonFile(path, optional: true, reloadOnChange: true);
        Console.WriteLine($"[PatternPro Desktop] Config: loaded {path}");
    }

    private static void TryLoadPatternWebDevSettings(ConfigurationManager config)
    {
        var webDev = DesktopPaths.FindRepoFile("Pattern.Web", "appsettings.Development.json");
        if (webDev is null)
        {
            Console.WriteLine("[PatternPro Desktop] Config: Pattern.Web dev settings not found.");
            return;
        }

        config.AddJsonFile(webDev, optional: true, reloadOnChange: true);
        Console.WriteLine($"[PatternPro Desktop] Config: loaded {webDev}");
    }
}
