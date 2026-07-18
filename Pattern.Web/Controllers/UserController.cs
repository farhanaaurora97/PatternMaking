using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

[Authorize]
public class UserController(IUserService userService) : Controller
{
    public IActionResult Index()
    {
        var user = CurrentUser();
        if (user is null) return Challenge();

        SetLayout("User", "My account");
        return View(new UserProfileViewModel
        {
            Id = user.Id,
            EmployeeId = user.EmployeeId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            RoleLabel = AppRoles.Label(user.Role),
            IsActive = user.IsActive,
            PendingApproval = !user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        });
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        SetLayout("User", "Change password");
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetLayout("User", "Change password");
            return View(model);
        }

        var id = CurrentUserId();
        if (id <= 0) return Challenge();

        try
        {
            userService.ChangePassword(id, model.CurrentPassword, model.NewPassword);
            TempData["UserMessage"] = "Password updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            SetLayout("User", "Change password");
            return View(model);
        }
    }

    private AppUser? CurrentUser()
    {
        var id = CurrentUserId();
        return id > 0 ? userService.GetById(id) : null;
    }

    private int CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : 0;
    }

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle = title,
        };
}
