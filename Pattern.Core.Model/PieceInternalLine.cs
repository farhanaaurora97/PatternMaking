namespace Pattern.Core.Model;

/// <summary>Non-cutting construction guide (pocket placement, fly line, etc.).</summary>
public class PieceInternalLine
{
    public string Label { get; set; } = "Guide";

    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }

    public static List<PieceInternalLine> CloneList(IReadOnlyList<PieceInternalLine>? lines) =>
        lines is null
            ? []
            : lines.Select(l => new PieceInternalLine
            {
                Label = l.Label,
                X1 = l.X1,
                Y1 = l.Y1,
                X2 = l.X2,
                Y2 = l.Y2,
            }).ToList();
}
