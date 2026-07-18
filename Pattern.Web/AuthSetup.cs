using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Pattern.Core.Model;
using Pattern.Web.Authorization;

namespace Pattern.Web;

public static class AuthSetup
{
    public const string CanEditPolicy = "CanEdit";
    public const string CanExportFactoryPolicy = "CanExportFactory";

    private static readonly HashSet<string> WeakSeedPasswords = new(StringComparer.Ordinal)
    {
        "Admin@123",
        "admin",
        "password",
        "Password1",
        "CHANGE_ME",
    };

    public static IServiceCollection AddPatternProAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(CanEditPolicy, p =>
                p.RequireRole(AppRoles.Admin, AppRoles.Designer));
            options.AddPolicy(CanExportFactoryPolicy, p =>
                p.RequireRole(AppRoles.Admin, AppRoles.Designer));
        });

        services.AddScoped<ViewerReadOnlyFilter>();

        return services;
    }

    public static IMvcBuilder AddPatternProGlobalAuth(this IMvcBuilder mvc)
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        mvc.AddMvcOptions(options =>
        {
            options.Filters.Add(new AuthorizeFilter(policy));
            options.Filters.AddService<ViewerReadOnlyFilter>();
        });

        return mvc;
    }

    public static void SeedAdminUser(this WebApplication app)
    {
        var config = app.Configuration.GetSection("Auth");
        var userName = config["SeedAdminUserName"];
        var password = config["SeedAdminPassword"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("[PatternPro] Auth: no SeedAdminUserName/SeedAdminPassword — skipping admin seed.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<PatternPro.Core.IServices.IUserService>();
        users.EnsureSeedAdmin(userName.Trim(), password);
        Console.WriteLine($"[PatternPro] Auth: seed admin '{userName.Trim()}' ensured (created only if no users exist).");
    }

    /// <summary>Logs production warnings for weak or missing configuration (does not block startup).</summary>
    public static void LogProductionReadinessWarnings(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
            return;

        var config = app.Configuration;
        var warnings = new List<string>();

        var seedPassword = config["Auth:SeedAdminPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
            warnings.Add("Auth:SeedAdminPassword is not set — first admin must be created in Admin panel or via environment variable.");
        else if (WeakSeedPasswords.Contains(seedPassword.Trim()))
            warnings.Add("Auth:SeedAdminPassword is a known default — change it before go-live (use environment variables).");

        var pg = config.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(pg))
            warnings.Add("ConnectionStrings:Postgres is not set — app will use JSON files under App_Data (not recommended for production teams).");
        else if (pg.Contains("YOUR_PASSWORD", StringComparison.OrdinalIgnoreCase)
                 || pg.Contains("Password=1234", StringComparison.OrdinalIgnoreCase))
            warnings.Add("PostgreSQL connection string uses a placeholder or dev password — set ConnectionStrings__Postgres on the server.");

        if (config.GetValue("Auth:RegistrationEnabled", false))
            warnings.Add("Auth:RegistrationEnabled is true — public registration is open.");

        foreach (var warning in warnings)
            Console.WriteLine($"[PatternPro] PRODUCTION WARNING: {warning}");
    }

    public static string? CurrentDisplayName(ClaimsPrincipal user) =>
        user.FindFirst("DisplayName")?.Value ?? user.Identity?.Name;

    public static string CurrentInitials(ClaimsPrincipal user)
    {
        var name = CurrentDisplayName(user) ?? "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}
