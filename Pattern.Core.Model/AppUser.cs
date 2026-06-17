namespace Pattern.Core.Model;

public class AppUser
{
    public int Id { get; set; }

    /// <summary>Factory employee / staff ID (unique).</summary>
    public string EmployeeId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Viewer;
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class UsersStore
{
    public int NextId { get; set; } = 1;
    public List<AppUser> Users { get; set; } = [];
}
