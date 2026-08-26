using Microsoft.Extensions.Configuration;

namespace PatternPro.Desktop;

internal static class DesktopTeamSetup
{
    public static bool UsesTeamServer(IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(conn)) return false;

        var host = ParseConnPart(conn, "Host");
        if (string.IsNullOrWhiteSpace(host)) return false;

        return !host.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            && host is not "127.0.0.1" and not "::1";
    }

    /// <summary>Remove stale offline JSON cache so team clients never show old local demo data.</summary>
    public static void ClearStaleLocalJsonCache(IConfiguration configuration)
    {
        if (!UsesTeamServer(configuration)) return;

        var local = Path.Combine(FileSystem.AppDataDirectory, "App_Data");
        if (!Directory.Exists(local)) return;

        try
        {
            Directory.Delete(local, recursive: true);
            Console.WriteLine("[PatternPro Desktop] Cleared old offline data (team mode uses shared server).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PatternPro Desktop] Note: could not clear offline cache: {ex.Message}");
        }
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
}
