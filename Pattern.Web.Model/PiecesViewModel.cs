namespace Pattern.Web.Model;

public class PiecesViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public string StyleLabel { get; set; } = "Skinny Fit";
    public IReadOnlyList<PieceCardViewModel> Pieces { get; set; } = [];
}

public class PieceCardViewModel
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CutInstruction { get; set; } = string.Empty;
}
