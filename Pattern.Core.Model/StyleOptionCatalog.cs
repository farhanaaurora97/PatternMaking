using System.Text.RegularExpressions;

namespace Pattern.Core.Model;

/// <summary>Built-in and user-defined fit / pant-type options for pattern creation.</summary>
public static class StyleOptionCatalog
{
    public const string CustomFitOption = "__custom_fit__";
    public const string CustomCategoryOption = "__custom_category__";
    public const string CustomBaseSizeOption = "__custom_base__";

    public static readonly string[] BuiltInBaseSizes = ["XS", "S", "M", "L", "XL", "XXL"];

    public static readonly (string Key, string Label)[] BuiltInFitOptions =
    [
        ("skinny", "Skinny Fit"),
        ("slim", "Slim Fit"),
        ("straight", "Straight Fit"),
        ("bootcut", "Bootcut Fit"),
        ("wideLeg", "Wide Leg Fit"),
    ];

    private static readonly Dictionary<string, string> BuiltInFitDisplayLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["skinny"] = "Skinny",
            ["slim"] = "Slim",
            ["straight"] = "Straight",
            ["bootcut"] = "Bootcut",
            ["wideLeg"] = "Wide Leg",
        };

    public static readonly (string Key, string Label, string Prefix)[] BuiltInCategoryOptions =
    [
        ("denim", "Denim", "DN"),
        ("chinos", "Chinos", "CH"),
        ("trousers", "Trousers", "TR"),
        ("cargo", "Cargo", "CG"),
        ("joggers", "Joggers", "JG"),
        ("linen", "Linen", "LN"),
        ("leather", "Leather", "LE"),
        ("palazzo", "Palazzo", "PA"),
        ("shorts", "Shorts", "SH"),
        ("sweatpants", "Sweatpants", "SW"),
        ("corduroy", "Corduroy", "CD"),
        ("workwear", "Workwear", "WK"),
    ];

    private static readonly HashSet<string> BuiltInStyleKeys =
        new(BuiltInFitOptions.Select(o => o.Key), StringComparer.OrdinalIgnoreCase);

    public static bool IsBuiltInStyleKey(string? styleKey) =>
        !string.IsNullOrWhiteSpace(styleKey) && BuiltInStyleKeys.Contains(styleKey.Trim());

    /// <summary>Built-in fits use their own block; custom fits draft from slim template.</summary>
    public static string TemplateStyleKey(string? styleKey)
    {
        var key = NormalizeStyleKey(styleKey);
        return IsBuiltInStyleKey(key) ? key : "slim";
    }

    public static string NormalizeStyleKey(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return "skinny";
        var s = style.Trim();
        if (string.Equals(s, "wide leg", StringComparison.OrdinalIgnoreCase)) return "wideLeg";
        if (string.Equals(s, CustomFitOption, StringComparison.Ordinal)) return "skinny";

        return s.ToLowerInvariant() switch
        {
            "skinny" => "skinny",
            "slim" => "slim",
            "straight" => "straight",
            "bootcut" => "bootcut",
            "wideleg" => "wideLeg",
            _ when IsValidCustomStyleKey(s) => s,
            _ => ToStyleKey(s),
        };
    }

    public static bool IsValidCustomStyleKey(string key) =>
        !string.IsNullOrWhiteSpace(key)
        && !IsBuiltInStyleKey(key)
        && Regex.IsMatch(key, @"^[a-z][a-zA-Z0-9]{0,31}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public static string ToStyleKey(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "customFit";

        var words = Regex.Split(label.Trim(), @"[^\w]+")
            .Where(w => w.Length > 0)
            .ToArray();
        if (words.Length == 0) return "customFit";

        var first = words[0].ToLowerInvariant();
        var rest = string.Concat(words.Skip(1).Select(w =>
            char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "")));

        var key = (first + rest);
        if (key.Length > 32) key = key[..32];
        if (IsBuiltInStyleKey(key)) key += "Custom";
        if (char.IsUpper(key[0])) key = char.ToLowerInvariant(key[0]) + key[1..];
        return key;
    }

    public static string StyleKeyFromDisplayLabel(string? displayLabel)
    {
        if (string.IsNullOrWhiteSpace(displayLabel)) return "skinny";
        foreach (var (key, label) in BuiltInFitDisplayLabels)
        {
            if (string.Equals(label, displayLabel.Trim(), StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return ToStyleKey(displayLabel);
    }

    public static string FormatFitDisplayLabel(string styleKey, string? customLabel = null)
    {
        if (!string.IsNullOrWhiteSpace(customLabel))
            return customLabel.Trim();
        if (BuiltInFitDisplayLabels.TryGetValue(styleKey, out var label))
            return label;
        return HumanizeStyleKey(styleKey);
    }

    public static (string Key, string DisplayLabel) ResolveFit(string selectedKey, string? customLabel)
    {
        if (string.Equals(selectedKey, CustomFitOption, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(customLabel))
                throw new ArgumentException("Enter a custom fit name.");
            var label = customLabel.Trim();
            return (ToStyleKey(label), label);
        }

        var key = NormalizeStyleKey(selectedKey);
        return (key, FormatFitDisplayLabel(key));
    }

    public static string ResolveBaseSize(string selectedKey, string? customLabel)
    {
        if (string.Equals(selectedKey, CustomBaseSizeOption, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(customLabel))
                throw new ArgumentException("Enter a custom base size.");
            return NormalizeBaseSizeLabel(customLabel);
        }

        return NormalizeBaseSizeLabel(selectedKey);
    }

    public static string NormalizeBaseSizeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Enter a base size.");

        var s = label.Trim();
        if (s.Length > 12)
            s = s[..12];

        if (int.TryParse(s, out _))
            return s;

        if (s.StartsWith('W') || s.StartsWith('w'))
        {
            var tail = s[1..].Trim();
            return string.IsNullOrEmpty(tail) ? "W" : "W" + tail.ToUpperInvariant();
        }

        return s.ToUpperInvariant();
    }

    public static (string Key, string Label, string CodePrefix) ResolveCategory(string selectedKey, string? customLabel)
    {
        if (string.Equals(selectedKey, CustomCategoryOption, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(customLabel))
                throw new ArgumentException("Enter a custom pant type.");
            var label = customLabel.Trim();
            var key = ToCategoryKey(label);
            return (key, label, DeriveCodePrefix(label));
        }

        var builtIn = BuiltInCategoryOptions.FirstOrDefault(c =>
            c.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
        if (builtIn.Key is null)
            builtIn = BuiltInCategoryOptions[0];

        return (builtIn.Key, builtIn.Label, builtIn.Prefix);
    }

    public static string ToCategoryKey(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "custom";
        var slug = Regex.Replace(label.Trim().ToLowerInvariant(), @"[^\w]+", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        return string.IsNullOrEmpty(slug) ? "custom" : slug[..Math.Min(slug.Length, 24)];
    }

    public static string DeriveCodePrefix(string label)
    {
        var letters = new string(label.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
        return letters.Length >= 2 ? letters : "CU";
    }

    private static string HumanizeStyleKey(string styleKey)
    {
        if (string.IsNullOrWhiteSpace(styleKey)) return "Custom";
        var spaced = Regex.Replace(styleKey, "([a-z])([A-Z])", "$1 $2", RegexOptions.None, TimeSpan.FromSeconds(1));
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
