 namespace Pattern.Web.Model;

public class CanvasViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public IReadOnlyList<string> PieceNames { get; set; } = [];
    public int SelectedPieceIndex { get; set; } = 0;
}
 