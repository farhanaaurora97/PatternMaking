using Microsoft.AspNetCore.Components;
using PatternPro.Desktop.Services;

namespace PatternPro.Desktop.Helpers;

internal static class DesktopShellNavigation
{
    private static readonly (string Prefix, string Title)[] Routes =
    [
        ("module/export", "Export / DXF"),
        ("module/canvas", "Canvas Editor"),
        ("module/nest", "Graded Nest"),
        ("module/pieces", "Piece library"),
        ("module/grading", "Grading"),
        ("module/block-generator", "Block Generator"),
        ("module/size-chart", "Size Chart"),
        ("module/style-sheet", "Style Sheet"),
        ("module/library", "Pattern library"),
        ("admin", "Admin panel"),
        ("account", "My account"),
        ("", "Dashboard"),
    ];

    public static string PageTitle(NavigationManager nav)
    {
        var path = nav.ToBaseRelativePath(nav.Uri).Split('?', '#')[0].TrimEnd('/');
        foreach (var (prefix, title) in Routes)
        {
            if (prefix == "" && string.IsNullOrEmpty(path))
                return title;
            if (!string.IsNullOrEmpty(prefix)
                && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return title;
        }

        return "PatternPro";
    }

    public static string StyleFromUri(NavigationManager nav) =>
        FitStyleKeys.Normalize(GetQueryValue(nav, "style"));

    public static int? PatternIdFromUri(NavigationManager nav)
    {
        var raw = GetQueryValue(nav, "patternId");
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    public static bool LockStyleSwitcher(NavigationManager nav)
    {
        if (PatternIdFromUri(nav) is not int pid || pid <= 0)
            return false;
        var path = nav.ToBaseRelativePath(nav.Uri).Split('?', '#')[0];
        return path.StartsWith("module/export", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("module/canvas", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("module/pieces", StringComparison.OrdinalIgnoreCase);
    }

    public static void NavigateWithStyle(NavigationManager nav, string styleKey)
    {
        var path = nav.ToBaseRelativePath(nav.Uri).Split('#')[0];
        var basePath = path.Split('?')[0];
        var query = ParseQuery(path);
        query["style"] = FitStyleKeys.Normalize(styleKey);
        nav.NavigateTo(AppendQuery(basePath, query));
    }

    public static bool IsCanvasRoute(NavigationManager nav)
    {
        var path = nav.ToBaseRelativePath(nav.Uri).Split('?', '#')[0];
        return path.StartsWith("module/canvas", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("module/nest", StringComparison.OrdinalIgnoreCase);
    }

    public const string NewPatternQueryFlag = "newPattern=1";

    public static bool UriRequestsNewPattern(NavigationManager nav) =>
        nav.Uri.Contains(NewPatternQueryFlag, StringComparison.OrdinalIgnoreCase);

    public static void ClearNewPatternQuery(NavigationManager nav)
    {
        if (!UriRequestsNewPattern(nav))
            return;
        nav.NavigateTo("", replace: true);
    }

    public static bool IsDashboardRoute(NavigationManager nav)
    {
        var path = nav.ToBaseRelativePath(nav.Uri).Split('?', '#')[0].TrimEnd('/');
        return string.IsNullOrEmpty(path);
    }

    /// <summary>Top bar "+ New Pattern" — open create on dashboard from any module.</summary>
    public static void RequestNewPattern(NavigationManager nav, DesktopShellEvents shellEvents)
    {
        if (IsDashboardRoute(nav))
        {
            shellEvents.RequestNewPattern();
            return;
        }

        nav.NavigateTo($"/?{NewPatternQueryFlag}");
    }

    public static string HrefWithStyle(string path, string styleKey) =>
        AppendQuery(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["style"] = FitStyleKeys.Normalize(styleKey),
        });

    public static string HrefWithStyleAndPattern(string path, string styleKey, int patternId) =>
        AppendQuery(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["style"] = FitStyleKeys.Normalize(styleKey),
            ["patternId"] = patternId.ToString(),
        });

    private static string? GetQueryValue(NavigationManager nav, string key)
    {
        var path = nav.ToBaseRelativePath(nav.Uri).Split('#')[0];
        var query = ParseQuery(path);
        return query.TryGetValue(key, out var value) ? value : null;
    }

    private static Dictionary<string, string> ParseQuery(string relativePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var qIndex = relativePath.IndexOf('?');
        if (qIndex < 0 || qIndex >= relativePath.Length - 1)
            return result;

        foreach (var part in relativePath[(qIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var k = Uri.UnescapeDataString(part[..eq]);
            var v = Uri.UnescapeDataString(part[(eq + 1)..]);
            result[k] = v;
        }

        return result;
    }

    private static string AppendQuery(string path, Dictionary<string, string> query)
    {
        if (query.Count == 0) return path;
        var qs = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{path}?{qs}";
    }
}
