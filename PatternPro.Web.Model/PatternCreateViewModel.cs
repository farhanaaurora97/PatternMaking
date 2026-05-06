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

    public static IEnumerable<(string Value, string Label)> CategoryOptions =>
    [
        ("denim", "Denim"),
        ("chinos", "Chinos"),
        ("trousers", "Trousers"),
        ("cargo", "Cargo"),
        ("joggers", "Joggers"),
        ("linen", "Linen"),
        ("leather", "Leather"),
        ("palazzo", "Palazzo"),
        ("shorts", "Shorts"),
        ("sweatpants", "Sweatpants"),
        ("corduroy", "Corduroy"),
        ("dress", "Dress pants"),
        ("workwear", "Workwear"),
    ];

    public static IEnumerable<(string Value, string Label)> StyleOptions =>
    [
        ("skinny",   "Skinny Fit"),
        ("slim",     "Slim Fit"),
        ("straight", "Straight Fit"),
        ("bootcut",  "Bootcut Fit"),
        ("wideLeg",  "Wide Leg Fit"),
    ];

    public static IEnumerable<string> SizeOptions =>
        ["XS", "S", "M", "L", "XL", "XXL"];
}
