using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternPro.Core.IServices;

namespace PatternPro.Desktop;

internal static class DesktopStartup
{
    public static void SeedAdminUser(IServiceProvider services, IConfiguration configuration)
    {
        var userName = configuration["Auth:SeedAdminUserName"]?.Trim();
        var password = configuration["Auth:SeedAdminPassword"]?.Trim();

        if (string.IsNullOrWhiteSpace(userName))
            userName = "admin";

        if (string.IsNullOrWhiteSpace(password))
            password = DesktopPilot.ResolveDevSeedPassword(configuration);

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("[PatternPro Desktop] Auth: no seed admin password — skipping.");
            return;
        }

        var users = services.GetRequiredService<IUserService>();
        users.EnsureSeedAdmin(userName, password);

        if (DesktopPilot.IsLocal(configuration))
            EnsureDevAdminCanLogin(users, userName, password);

        Console.WriteLine($"[PatternPro Desktop] Auth: seed admin '{userName}' ensured.");
    }

    private static void EnsureDevAdminCanLogin(IUserService users, string userName, string password)
    {
        if (users.ValidateLogin(userName, password) is not null)
        {
            Console.WriteLine("[PatternPro Desktop] Auth: login probe OK.");
            return;
        }

        var user = users.GetAll().FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            Console.WriteLine("[PatternPro Desktop] Auth: login probe failed — no admin user to repair.");
            return;
        }

        if (!user.IsActive)
        {
            Console.WriteLine("[PatternPro Desktop] Auth: reactivating disabled admin for dev.");
            users.UpdateUser(user.Id, user.EmployeeId, user.DisplayName, user.Role, isActive: true);
        }

        users.ResetPassword(user.Id, password);
        var repaired = users.ValidateLogin(userName, password);
        Console.WriteLine(repaired is null
            ? "[PatternPro Desktop] Auth: login probe still failing after password reset."
            : "[PatternPro Desktop] Auth: dev admin password repaired.");
    }
}
