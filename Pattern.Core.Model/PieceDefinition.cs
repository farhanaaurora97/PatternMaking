namespace Pattern.Core.Model;

public class PieceDefinition
{
    public string         Name    { get; set; } = string.Empty;
    public string         Cut     { get; set; } = string.Empty;
    public string         Color   { get; set; } = string.Empty;
    public List<int[]>    Points  { get; set; } = [];
    public List<int[]>?   Grain   { get; set; }
    public List<int[]>?   Cf      { get; set; }
    public List<int[]>?   Notches { get; set; }
    public int            OffsetX { get; set; }
    public int            OffsetY { get; set; }
}