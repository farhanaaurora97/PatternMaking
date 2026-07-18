using System.ComponentModel.DataAnnotations;
using Pattern.Core.Model;

namespace Pattern.Web.Model;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username or employee ID")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public bool RegistrationEnabled { get; set; }
}

public class RegisterViewModel
{
    [Required]
    [StringLength(32, MinimumLength = 2)]
    [Display(Name = "Employee ID")]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Display(Name = "Full name")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(64, MinimumLength = 2)]
    [Display(Name = "Username (optional)")]
    public string? UserName { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UserProfileViewModel
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool PendingApproval { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminUsersViewModel
{
    public IReadOnlyList<AdminUserRowViewModel> Users { get; set; } = [];
}

public class AdminUserRowViewModel
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminUserFormViewModel
{
    public int? Id { get; set; }
    public bool IsEdit { get; set; }

    [Required]
    [StringLength(32, MinimumLength = 2)]
    [Display(Name = "Employee ID")]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 2)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = AppRoles.Designer;

    public bool IsActive { get; set; } = true;

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
