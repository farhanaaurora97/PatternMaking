using Pattern.Core.Model;

namespace PatternPro.Desktop.Helpers;

internal static class FitStyleKeys
{
    public static string Normalize(string? style) => StyleOptionCatalog.NormalizeStyleKey(style);
}
