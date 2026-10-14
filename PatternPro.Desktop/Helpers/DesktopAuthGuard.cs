using System.Security.Claims;
using Pattern.Core.Model;

namespace PatternPro.Desktop.Helpers;

internal static class DesktopAuthGuard
{
    public static bool CanEdit(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Designer);

    public static bool IsViewerOnly(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && user.IsInRole(AppRoles.Viewer)
        && !CanEdit(user);
}
