namespace Pattern.Web.Model;

public class PatternViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Style    { get; set; } = string.Empty;
    /// <summary>Lowercase fit key used in route params (e.g. "skinny", "wideLeg").</summary>
    public string StyleKey { get; set; } = "skinny";
    public string BaseSize { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusCssClass => $"st-{Status}";
    public string Date { get; set; } = string.Empty;
    /// <summary>Short due date for table (e.g. Apr 5) or em dash.</summary>
    public string DueDateLabel { get; set; } = "—";
    /// <summary>ISO date for the date input value (yyyy-MM-dd) or empty string when no due date.</summary>
    public string DueDateIso { get; set; } = string.Empty;
    /// <summary>Product line for dashboard category tabs (e.g. Denim).</summary>
    public string Category { get; set; } = "Denim";

    public string DisplayName => $"{Code} {Name}";
}
