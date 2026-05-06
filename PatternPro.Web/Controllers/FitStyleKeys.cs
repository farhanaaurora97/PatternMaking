namespace Pattern.Web.Controllers;

/// <summary>Canonical fit keys for URLs, PieceService, and top-bar style pills.</summary>
internal static class FitStyleKeys
{
    public static string Normalize(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return "skinny";
        var s = style.Trim();
        if (string.Equals(s, "wide leg", StringComparison.OrdinalIgnoreCase)) return "wideLeg";
        if (s.Equals("skinny", StringComparison.OrdinalIgnoreCase)) return "skinny";
        if (s.Equals("slim", StringComparison.OrdinalIgnoreCase)) return "slim";
        if (s.Equals("straight", StringComparison.OrdinalIgnoreCase)) return "straight";
        if (s.Equals("bootcut", StringComparison.OrdinalIgnoreCase)) return "bootcut";
        if (s.Equals("wideLeg", StringComparison.OrdinalIgnoreCase) || s.Equals("wideleg", StringComparison.OrdinalIgnoreCase))
            return "wideLeg";
        return "skinny";
    }
}
