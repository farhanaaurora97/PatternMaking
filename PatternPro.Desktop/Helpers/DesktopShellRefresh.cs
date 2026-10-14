using Microsoft.AspNetCore.Components;
using PatternPro.Desktop.Services;

namespace PatternPro.Desktop.Helpers;

/// <summary>
/// Reloads singleton data from the store and remounts the current Blazor route.
/// </summary>
internal static class DesktopShellRefresh
{
    public static void ReloadCurrentRoute(
        NavigationManager navigation,
        DesktopDataRefreshService dataRefresh,
        DesktopCanvasHost canvasHost,
        DesktopToastService toast)
    {
        canvasHost.HideOverlayForNavigation();
        dataRefresh.ReloadAllFromStore();
        toast.Show(
            "Refreshing",
            "Loading latest data from the shared database…",
            DesktopToastType.Info,
            "↻");

        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var path = relative.Split('#', 2)[0];
        navigation.NavigateTo(string.IsNullOrWhiteSpace(path) ? "" : path, forceLoad: true);
    }
}
