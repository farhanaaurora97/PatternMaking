namespace Pattern.Core.Model;

public sealed class PatternActivityEntry
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public int PatternId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string BadgeCss { get; set; } = "badge-draft";
    public string Text { get; set; } = string.Empty;
}
