namespace Pattern.Web.Model;

/// <summary>Fixed colors per pant / product line for dashboard tiles.</summary>
public static class PantCategoryPalette
{
    private static readonly Dictionary<string, string> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["denim"] = "#1e3a5f",
        ["trousers"] = "#2d5282",
        ["chinos"] = "#3a6491",
        ["cargo"] = "#1e5f5a",
        ["linen"] = "#2a7a6e",
        ["leather"] = "#4a3320",
        ["palazzo"] = "#5a3a5c",
        ["corduroy"] = "#5e3d1e",
        ["workwear"] = "#4a3a1e",
        ["joggers"] = "#3d5166",
        ["shorts"] = "#5c6f82",
        ["sweatpants"] = "#4a5568",
        ["dress"] = "#2d3748",
        ["other"] = "#718096",
    };

    public static string GetColor(string? label)
    {
        var key = string.IsNullOrWhiteSpace(label) ? "other" : label.Trim();
        return Colors.TryGetValue(key, out var color) ? color : Colors["other"];
    }
}
