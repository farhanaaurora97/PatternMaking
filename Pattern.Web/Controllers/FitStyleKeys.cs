using Pattern.Core.Model;

namespace Pattern.Web.Controllers;

/// <summary>Canonical fit keys for URLs, PieceService, and top-bar style pills.</summary>
internal static class FitStyleKeys
{
    public static string Normalize(string? style) => StyleOptionCatalog.NormalizeStyleKey(style);
}
