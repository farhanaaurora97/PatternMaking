namespace Pattern.Web.Model;

public class PiecesViewModel
{
    public int    PatternId   { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public string PatternCode { get; set; } = string.Empty;
    public string StyleKey    { get; set; } = "skinny";
    public string StyleLabel  { get; set; } = "Skinny Fit";
    public IReadOnlyList<PieceCardViewModel> Pieces { get; set; } = [];

    public string? ErrorMessage   { get; set; }
    public string? SuccessMessage { get; set; }
}

public class PieceCardViewModel
{
    public int    Index          { get; set; }
    public string Name           { get; set; } = string.Empty;
    public string CutInstruction { get; set; } = string.Empty;
    public string Color          { get; set; } = "#a78bfa";
    public string Category       { get; set; } = "Hardware & Details";
    public string GrainLine      { get; set; } = "Straight";
    public string Description    { get; set; } = string.Empty;
}
