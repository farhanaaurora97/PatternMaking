using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(IUserService userService) : Controller
{
    public IActionResult Index()
    {
        var users = userService.GetAll()
            .Select(u => new AdminUserRowViewModel
            {
                Id = u.Id,
                EmployeeId = u.EmployeeId,
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                Role = u.Role,
                RoleLabel = AppRoles.Label(u.Role),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
            })
            .ToList();

        SetLayout("Admin", "Admin panel");
        return View(new AdminUsersViewModel { Users = users });
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetLayout("Admin", "Admin / New user");
        return View(new AdminUserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AdminUserFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetLayout("Admin", "Admin / New user");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        if (model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
        if (!ModelState.IsValid)
        {
            SetLayout("Admin", "Admin / New user");
            return View(model);
        }

        try
        {
            userService.CreateUser(
                model.EmployeeId,
                model.UserName,
                model.DisplayName,
                model.Role,
                model.Password);
            TempData["AdminMessage"] = $"User '{model.UserName}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            SetLayout("Admin", "Admin / New user");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var user = userService.GetById(id);
        if (user is null) return NotFound();

        SetLayout("Admin", "Admin / Edit user");
        return View(new AdminUserFormViewModel
        {
            Id = user.Id,
            EmployeeId = user.EmployeeId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEdit = true,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(AdminUserFormViewModel model)
    {
        if (model.Id is null or <= 0) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.IsEdit = true;
            SetLayout("Admin", "Admin / Edit user");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
        if (!ModelState.IsValid)
        {
            model.IsEdit = true;
            SetLayout("Admin", "Admin / Edit user");
            return View(model);
        }

        try
        {
            if (!model.IsActive && model.Id == CurrentUserId())
            {
                ModelState.AddModelError(string.Empty, "You cannot disable your own account.");
                model.IsEdit = true;
                SetLayout("Admin", "Admin / Edit user");
                return View(model);
            }

            var updated = userService.UpdateUser(
                model.Id.Value,
                model.EmployeeId,
                model.DisplayName,
                model.Role,
                model.IsActive);
            if (updated is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(model.Password))
                userService.ResetPassword(model.Id.Value, model.Password);

            TempData["AdminMessage"] = $"User '{updated.UserName}' updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.IsEdit = true;
            SetLayout("Admin", "Admin / Edit user");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleActive(int id, string active)
    {
        var enable = string.Equals(active, "true", StringComparison.OrdinalIgnoreCase);
        var user = userService.GetById(id);
        if (user is null) return NotFound();

        try
        {
            if (!enable && id == CurrentUserId())
            {
                TempData["AdminError"] = "You cannot disable your own account.";
                return RedirectToAction(nameof(Index));
            }

            userService.UpdateUser(id, user.EmployeeId, user.DisplayName, user.Role, enable);
            TempData["AdminMessage"] = enable
                ? $"User '{user.UserName}' approved and enabled."
                : $"User '{user.UserName}' disabled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            if (!userService.DeleteUser(id))
                return NotFound();
            TempData["AdminMessage"] = "User deleted.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
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
