using Microsoft.Extensions.Configuration;

namespace PatternPro.Desktop;

internal static class DesktopPilot
{
    public static bool IsLocal(IConfiguration configuration)
    {
        if (DesktopPaths.FindRepoFile("Pattern.Web", "appsettings.Development.json") is not null)
            return true;

        var pg = configuration.GetConnectionString("Postgres") ?? "";
        return pg.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static (string UserName, string Password)? Credentials(IConfiguration configuration)
    {
        if (!IsLocal(configuration))
            return null;

        var userName = configuration["Auth:SeedAdminUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "admin";

        var password = configuration["Auth:SeedAdminPassword"]?.Trim();
        if (string.IsNullOrWhiteSpace(password))
            password = ResolveDevSeedPassword(configuration);

        return string.IsNullOrWhiteSpace(password) ? null : (userName, password);
    }

    public static string? ResolveDevSeedPassword(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")))
            return "Admin@123";

        return DesktopPaths.FindRepoFile("Pattern.Web", "appsettings.Development.json") is not null
            ? "Admin@123"
            : null;
    }
}
