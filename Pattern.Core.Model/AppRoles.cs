namespace Pattern.Core.Model;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Designer = "Designer";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Admin, Designer, Viewer];

    public static string Label(string role) => role switch
    {
        Admin => "Administrator",
        Designer => "Pattern designer",
        Viewer => "View only",
        _ => role,
    };

    public static bool CanEdit(string role) =>
        role is Admin or Designer;

    public static bool CanExportFactory(string role) =>
        role is Admin or Designer;
}
