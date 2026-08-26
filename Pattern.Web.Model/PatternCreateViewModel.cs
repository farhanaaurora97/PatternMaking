using System.ComponentModel.DataAnnotations;

namespace Pattern.Web.Model;

public class PatternCreateViewModel
{
    [Required(ErrorMessage = "Please enter a pattern name.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string StyleKey { get; set; } = "skinny";

    [Required]
    public string BaseSize { get; set; } = "M";

    /// <summary>Bottom-wear product line (denim, chinos, trousers, …).</summary>
    [Required]
    public string CategoryKey { get; set; } = "denim";

    public string Designer { get; set; } = "Pattern Designer";

    /// <summary>PLM season (e.g. SS26). Empty = current season.</summary>
    public string Season { get; set; } = string.Empty;

    /// <summary>Style owner / merchandiser. Empty = same as designer.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>PLM lifecycle when the style row is created.</summary>
    public string LifecycleStatus { get; set; } = Pattern.Core.Model.StyleLifecycle.Idea;

    public static IEnumerable<(string Value, string Label)> LifecycleOptions => StyleLifecycleOptions.All;

    /// <summary>User-entered fit name when <see cref="StyleKey"/> is <see cref="StyleOptionCatalog.CustomFitOption"/>.</summary>
    public string? CustomFitLabel { get; set; }

    /// <summary>User-entered pant type when <see cref="CategoryKey"/> is <see cref="StyleOptionCatalog.CustomCategoryOption"/>.</summary>
    public string? CustomCategoryLabel { get; set; }

    public static IEnumerable<(string Value, string Label)> CategoryOptions =>
    [
        .. Pattern.Core.Model.StyleOptionCatalog.BuiltInCategoryOptions.Select(c => (c.Key, c.Label)),
        (Pattern.Core.Model.StyleOptionCatalog.CustomCategoryOption, "Custom type…"),
    ];

    public static IEnumerable<(string Value, string Label)> StyleOptions =>
    [
        .. Pattern.Core.Model.StyleOptionCatalog.BuiltInFitOptions,
        (Pattern.Core.Model.StyleOptionCatalog.CustomFitOption, "Custom fit…"),
    ];

    /// <summary>User-entered base size when <see cref="BaseSize"/> is <see cref="StyleOptionCatalog.CustomBaseSizeOption"/>.</summary>
    public string? CustomBaseSizeLabel { get; set; }

    public static IEnumerable<(string Value, string Label)> BaseSizeOptions =>
    [
        .. Pattern.Core.Model.StyleOptionCatalog.BuiltInBaseSizes.Select(s => (s, s)),
        (Pattern.Core.Model.StyleOptionCatalog.CustomBaseSizeOption, "Custom size…"),
    ];

    public static IEnumerable<string> SizeOptions =>
        Pattern.Core.Model.StyleOptionCatalog.BuiltInBaseSizes;
}
