namespace Pattern.Core.Model;

public class Pattern
{
    public int    Id         { get; set; }
    public string Code       { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public string Style      { get; set; } = string.Empty;
    public string BaseSize   { get; set; } = string.Empty;
    public int    PieceCount { get; set; }
    public string Status     { get; set; } = "Draft";
    public string Date       { get; set; } = string.Empty;
    public string Designer   { get; set; } = string.Empty;
}