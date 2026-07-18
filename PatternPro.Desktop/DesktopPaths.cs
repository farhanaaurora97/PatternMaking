namespace PatternPro.Desktop;

internal static class DesktopPaths
{
    /// <summary>
    /// JSON store directory. Prefers PATTERNPRO_APP_DATA, then Pattern.Web/App_Data in dev, else local app data.
    /// </summary>
    public static string ResolveAppDataDirectory()
    {
        var env = Environment.GetEnvironmentVariable("PATTERNPRO_APP_DATA");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var full = Path.GetFullPath(env);
            Directory.CreateDirectory(full);
            return full;
        }

        var webAppData = FindRepoPath("Pattern.Web", "App_Data");
        if (webAppData is not null && Directory.Exists(webAppData))
            return webAppData;

        var local = Path.Combine(FileSystem.AppDataDirectory, "App_Data");
        Directory.CreateDirectory(local);
        return local;
    }

    /// <summary>Locates a file under the repo by walking up from the app base directory.</summary>
    public static string? FindRepoFile(params string[] relativeParts)
    {
        var path = FindRepoPath(relativeParts);
        return path is not null && File.Exists(path) ? path : null;
    }

    private static string? FindRepoPath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (Directory.Exists(candidate) || File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
