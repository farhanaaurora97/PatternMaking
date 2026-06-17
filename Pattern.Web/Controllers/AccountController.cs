using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

[AllowAnonymous]
public class AccountController(IUserService userService) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectAfterLogin(User);

        if (TempData["RegisterSuccess"] is string msg)
            ViewData["RegisterSuccess"] = msg;

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            RegistrationEnabled = userService.IsRegistrationEnabled(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.RegistrationEnabled = userService.IsRegistrationEnabled();
            return View(model);
        }

        var user = userService.ValidateLogin(model.UserName, model.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty,
                "Invalid username/employee ID or password, or your account is disabled or pending approval.");
            model.RegistrationEnabled = userService.IsRegistrationEnabled();
            return View(model);
        }

        userService.RecordLogin(user.Id);
        await SignInUser(user, model.RememberMe);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectAfterLogin(user);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectAfterLogin(User);

        if (!userService.IsRegistrationEnabled())
        {
            TempData["LoginMessage"] = "Registration is disabled. Contact your administrator.";
            return RedirectToAction(nameof(Login));
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!userService.IsRegistrationEnabled())
        {
            TempData["LoginMessage"] = "Registration is disabled.";
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            userService.RegisterUser(
                model.EmployeeId,
                model.DisplayName,
                model.UserName ?? string.Empty,
                model.Password);

            var msg = userService.RequiresAdminApproval()
                ? "Registration submitted. An administrator must approve your account before you can sign in."
                : "Account created. You can sign in now.";
            TempData["RegisterSuccess"] = msg;
            return RedirectToAction(nameof(Login));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult AccessDenied() => View();

    private async Task SignInUser(AppUser user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName),
            new("EmployeeId", user.EmployeeId),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(rememberMe ? 72 : 12),
            });
    }

    private IActionResult RedirectAfterLogin(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(AppRoles.Admin))
            return RedirectToAction("Index", "Admin");
        return RedirectToAction("Index", "User");
    }

    private static IActionResult RedirectAfterLogin(AppUser user)
    {
        if (string.Equals(user.Role, AppRoles.Admin, StringComparison.Ordinal))
            return new RedirectToActionResult("Index", "Admin", null);
        return new RedirectToActionResult("Index", "User", null);
    }
}
