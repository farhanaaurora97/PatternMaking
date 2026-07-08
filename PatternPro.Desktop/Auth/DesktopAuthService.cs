using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Desktop.Auth;

public sealed class DesktopAuthService(IUserService userService)
{
    private ClaimsPrincipal _current = new(new ClaimsIdentity());

    public ClaimsPrincipal Current => _current;

    /// <summary>
    /// True after an explicit user sign-out. Used to stop the local-pilot
    /// auto-login from immediately signing the user back in.
    /// </summary>
    public bool AutoLoginSuppressed { get; private set; }

    public event Action? Changed;

    public async Task<bool> SignInAsync(string userName, string password, bool rememberMe)
    {
        userName = userName.Trim();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            return false;

        var user = userService.ValidateLogin(userName, password);
        if (user is null)
            return false;

        userService.RecordLogin(user.Id);
        AutoLoginSuppressed = false;
        _current = BuildPrincipal(user);

        try
        {
            if (rememberMe)
                await SecureStorage.SetAsync("pp_user_id", user.Id.ToString());
            else
                SecureStorage.Default.Remove("pp_user_id");
        }
        catch
        {
            // SecureStorage can fail on some Windows setups — login still succeeds.
        }

        Changed?.Invoke();
        return true;
    }

    public async Task TryRestoreSessionAsync()
    {
        try
        {
            var idStr = await SecureStorage.GetAsync("pp_user_id");
            if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id))
                return;

            var user = userService.GetById(id);
            if (user is null || !user.IsActive)
                return;

            _current = BuildPrincipal(user);
            Changed?.Invoke();
        }
        catch
        {
            // SecureStorage unavailable on some platforms — ignore
        }
    }

    public void SignOut()
    {
        AutoLoginSuppressed = true;
        _current = new ClaimsPrincipal(new ClaimsIdentity());
        try { SecureStorage.Default.Remove("pp_user_id"); } catch { /* ignore */ }
        Changed?.Invoke();
    }

    public static string? DisplayName(ClaimsPrincipal user) =>
        user.FindFirst("DisplayName")?.Value ?? user.Identity?.Name;

    private static ClaimsPrincipal BuildPrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new("DisplayName", user.DisplayName),
            new("EmployeeId", user.EmployeeId),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "PatternProDesktop");
        return new ClaimsPrincipal(identity);
    }
}

public sealed class PatternProAuthStateProvider : AuthenticationStateProvider
{
    private readonly DesktopAuthService _auth;

    public PatternProAuthStateProvider(DesktopAuthService auth)
    {
        _auth = auth;
        _auth.Changed += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_auth.Current));
}
