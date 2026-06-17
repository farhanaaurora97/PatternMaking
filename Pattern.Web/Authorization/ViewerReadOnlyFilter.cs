using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pattern.Core.Model;

namespace Pattern.Web.Authorization;

/// <summary>Blocks POST requests for Viewer role (read-only users).</summary>
public sealed class ViewerReadOnlyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();

        var isAllowedViewerPost =
            (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "Register", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(controller, "User", StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase));

        if (!isAllowedViewerPost
            && http.User.Identity?.IsAuthenticated == true
            && http.User.IsInRole(AppRoles.Viewer)
            && string.Equals(http.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
