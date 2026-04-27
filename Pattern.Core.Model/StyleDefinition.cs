namespace Pattern.Core.Model;

/// <summary>Style template (piece count and names for a fit key like skinny).</summary>
public class StyleDefinition
{
    public string Label { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public List<string> PieceList { get; set; } = [];
}
