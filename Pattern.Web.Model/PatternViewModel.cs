namespace Pattern.Web.Model;

public class PatternViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string BaseSize { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusCssClass => $"st-{Status}";
    public string Date { get; set; } = string.Empty;
    public string DisplayName => $"{Code} {Name}";
}
