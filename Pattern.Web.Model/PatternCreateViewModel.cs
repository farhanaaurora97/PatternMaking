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

    public string Designer { get; set; } = "Pattern Designer";

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
