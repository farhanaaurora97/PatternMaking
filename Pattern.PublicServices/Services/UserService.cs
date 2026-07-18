using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class UserService(IUserRepository users, IConfiguration configuration) : IUserService
{
    private static readonly PasswordHasher<AppUser> Hasher = new();

    public IReadOnlyList<AppUser> GetAll() => users.GetAll();

    public AppUser? GetById(int id) => users.GetById(id);

    public AppUser? ValidateLogin(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = users.GetByUserName(userName.Trim())
            ?? users.GetByEmployeeId(userName.Trim());
        if (user is null || !user.IsActive)
            return null;

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Failed ? null : user;
    }

    public AppUser CreateUser(string employeeId, string userName, string displayName, string role, string password)
    {
        var empId = NormalizeEmployeeId(employeeId);
        userName = NormalizeUserName(userName);
        displayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim();
        role = NormalizeRole(role);

        if (users.GetByEmployeeId(empId) is not null)
            throw new InvalidOperationException($"Employee ID '{empId}' is already registered.");
        if (users.GetByUserName(userName) is not null)
            throw new InvalidOperationException($"Username '{userName}' is already taken.");

        ValidatePassword(password);

        var user = new AppUser
        {
            EmployeeId = empId,
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            PasswordHash = HashPassword(password),
        };

        return users.Save(user);
    }

    public AppUser RegisterUser(string employeeId, string displayName, string userName, string password)
    {
        if (!IsRegistrationEnabled())
            throw new InvalidOperationException("Registration is disabled. Contact your administrator.");

        var empId = NormalizeEmployeeId(employeeId);
        userName = string.IsNullOrWhiteSpace(userName) ? empId : NormalizeUserName(userName);
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? userName
            : displayName.Trim();

        if (users.GetByEmployeeId(empId) is not null)
            throw new InvalidOperationException($"Employee ID '{empId}' is already registered.");
        if (users.GetByUserName(userName) is not null)
            throw new InvalidOperationException($"Username '{userName}' is already taken.");

        ValidatePassword(password);

        var user = new AppUser
        {
            EmployeeId = empId,
            UserName = userName,
            DisplayName = displayName,
            Role = AppRoles.Viewer,
            IsActive = !RequiresAdminApproval(),
            PasswordHash = HashPassword(password),
        };

        return users.Save(user);
    }

    public AppUser? UpdateUser(int id, string employeeId, string displayName, string role, bool isActive)
    {
        var user = users.GetById(id);
        if (user is null) return null;

        var empId = NormalizeEmployeeId(employeeId);
        var existingEmp = users.GetByEmployeeId(empId);
        if (existingEmp is not null && existingEmp.Id != id)
            throw new InvalidOperationException($"Employee ID '{empId}' is already used by another user.");

        var newRole = NormalizeRole(role);
        GuardLastActiveAdmin(user, newRole, isActive);

        user.EmployeeId = empId;
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.UserName : displayName.Trim();
        user.Role = newRole;
        user.IsActive = isActive;
        return users.Save(user);
    }

    public bool ResetPassword(int id, string newPassword)
    {
        var user = users.GetById(id);
        if (user is null) return false;

        ValidatePassword(newPassword);
        user.PasswordHash = HashPassword(newPassword);
        users.Save(user);
        return true;
    }

    public bool ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = users.GetById(userId);
        if (user is null) return false;

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (result is PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Current password is incorrect.");

        ValidatePassword(newPassword);
        user.PasswordHash = HashPassword(newPassword);
        users.Save(user);
        return true;
    }

    public bool DeleteUser(int id)
    {
        var user = users.GetById(id);
        if (user is null) return false;

        if (string.Equals(user.Role, AppRoles.Admin, StringComparison.Ordinal)
            && user.IsActive)
        {
            EnsureAnotherActiveAdminExists();
        }

        users.Delete(id);
        return true;
    }

    public void RecordLogin(int id)
    {
        var user = users.GetById(id);
        if (user is null) return;
        user.LastLoginAt = DateTime.UtcNow;
        users.Save(user);
    }

    public void EnsureSeedAdmin(string userName, string password)
    {
        if (users.AnyUsers())
            return;

        userName = NormalizeUserName(userName);
        ValidatePassword(password);

        users.Save(new AppUser
        {
            EmployeeId = "ADMIN",
            UserName = userName,
            DisplayName = "Administrator",
            Role = AppRoles.Admin,
            IsActive = true,
            PasswordHash = HashPassword(password),
        });
    }

    public bool IsRegistrationEnabled() =>
        configuration.GetValue("Auth:RegistrationEnabled", true);

    public bool RequiresAdminApproval() =>
        configuration.GetValue("Auth:RequireAdminApproval", true);

    private static string HashPassword(string password) =>
        Hasher.HashPassword(new AppUser(), password);

    private static string NormalizeEmployeeId(string employeeId)
    {
        var id = employeeId.Trim().ToUpperInvariant();
        if (id.Length < 2)
            throw new InvalidOperationException("Employee ID must be at least 2 characters.");
        return id;
    }

    private static string NormalizeUserName(string userName)
    {
        var u = userName.Trim();
        if (u.Length < 2)
            throw new InvalidOperationException("Username must be at least 2 characters.");
        return u;
    }

    private static string NormalizeRole(string role)
    {
        var r = string.IsNullOrWhiteSpace(role) ? AppRoles.Viewer : role.Trim();
        return AppRoles.All.Contains(r, StringComparer.Ordinal) ? r : AppRoles.Viewer;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");
    }

    private void GuardLastActiveAdmin(AppUser user, string newRole, bool isActive)
    {
        var wasActiveAdmin = user.IsActive
            && string.Equals(user.Role, AppRoles.Admin, StringComparison.Ordinal);
        var willBeActiveAdmin = isActive
            && string.Equals(newRole, AppRoles.Admin, StringComparison.Ordinal);
        if (wasActiveAdmin && !willBeActiveAdmin)
            EnsureAnotherActiveAdminExists();
    }

    private void EnsureAnotherActiveAdminExists()
    {
        var adminCount = users.GetAll().Count(u =>
            u.IsActive && string.Equals(u.Role, AppRoles.Admin, StringComparison.Ordinal));
        if (adminCount <= 1)
            throw new InvalidOperationException("Cannot remove or disable the last active administrator.");
    }
}
