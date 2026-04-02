namespace Pattern.Web.Model;

public class NestViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public IReadOnlyList<NestSizeViewModel> Sizes { get; set; } =
    [
        new("XS",      "#7b68ee"),
        new("S",       "#4169e1"),
        new("M (Base)","#0a0a24", IsBase: true),
        new("L",       "#1a7a45"),
        new("XL",      "#d4a843"),
        new("XXL",     "#c0392b"),
    ];
}

public record NestSizeViewModel(string Label, string Color, bool IsBase = false);
