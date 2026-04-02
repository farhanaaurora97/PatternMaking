namespace Pattern.Core.Model;

public class StyleDefinition
{
    public string       Label      { get; set; } = string.Empty;
    public int          PieceCount { get; set; }
    public List<string> PieceList  { get; set; } = [];
}