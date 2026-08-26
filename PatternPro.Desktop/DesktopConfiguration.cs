using Microsoft.Extensions.Configuration;

namespace PatternPro.Desktop;

internal static class DesktopConfiguration
{
    public static void AddPatternProConfiguration(this MauiAppBuilder builder)
    {
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#if DEBUG
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
#endif
            .AddJsonFile("appsettings.Team.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        if (!HasPostgres(builder.Configuration))
            TryLoadPatternWebDevSettings(builder.Configuration);

        LogActivePostgres(builder.Configuration);
    }

    private static void LogActivePostgres(IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Console.WriteLine("[PatternPro Desktop] Config: no PostgreSQL — using local JSON under App_Data.");
            return;
        }

        var host = ParseConnPart(conn, "Host") ?? "?";
        var port = ParseConnPart(conn, "Port") ?? "5432";
        var db = ParseConnPart(conn, "Database") ?? "patternpro";
        var mode = host.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || host is "127.0.0.1" or "::1"
            ? "local"
            : "team (shared server)";
        Console.WriteLine($"[PatternPro Desktop] Config: PostgreSQL {db} @ {host}:{port} ({mode}).");
    }

    private static string? ParseConnPart(string conn, string key)
    {
        foreach (var part in conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            if (part[..eq].Equals(key, StringComparison.OrdinalIgnoreCase))
                return part[(eq + 1)..].Trim();
        }

        return null;
    }

    private static bool HasPostgres(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config.GetConnectionString("Postgres"));

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
