using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PatternPro.DataAccess.Persistence;

/// <summary>Used by <c>dotnet ef</c> when the startup project does not build the context.</summary>
public class PatternProDbContextFactory : IDesignTimeDbContextFactory<PatternProDbContext>
{
    public PatternProDbContext CreateDbContext(string[] args)
    {
        var conn = ResolveConnectionString();

        var opts = new DbContextOptionsBuilder<PatternProDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new PatternProDbContext(opts);
    }

    private static string ResolveConnectionString()
    {
        var envConn = Environment.GetEnvironmentVariable("PATTERNPRO_PG");
        if (!string.IsNullOrWhiteSpace(envConn))
            return envConn;

        var envName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            "Development";

        foreach (var root in CandidateRoots())
        {
            var conn = ReadConnectionStringFrom(root, envName);
            if (!string.IsNullOrWhiteSpace(conn))
                return conn;
        }

        return "Host=127.0.0.1;Port=5432;Database=patternpro;Username=postgres;Password=postgres";
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var cwd = Directory.GetCurrentDirectory();
        var baseDir = AppContext.BaseDirectory;

        return new[]
        {
            cwd,
            Path.Combine(cwd, "Pattern.Web"),
            Path.Combine(cwd, "PatternPro.Web"),
            Path.GetFullPath(Path.Combine(cwd, "..", "Pattern.Web")),
            Path.GetFullPath(Path.Combine(cwd, "..", "PatternPro.Web")),
            baseDir,
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Pattern.Web")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PatternPro.Web")),
        }.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadConnectionStringFrom(string root, string envName)
    {
        var baseFile = Path.Combine(root, "appsettings.json");
        var envFile = Path.Combine(root, $"appsettings.{envName}.json");

        var fromEnv = ReadPostgresConnectionString(envFile);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        return ReadPostgresConnectionString(baseFile);
    }

    private static string? ReadPostgresConnectionString(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs))
                return null;
            if (!cs.TryGetProperty("Postgres", out var pg))
                return null;

            var value = pg.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
