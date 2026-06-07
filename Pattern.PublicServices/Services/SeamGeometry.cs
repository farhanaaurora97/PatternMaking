using Pattern.Core.Model;

namespace PatternPro.Business.Services;

internal static class SeamGeometry
{
    /// <summary>Matches <see cref="PatternDraftingService"/> canvas scale.</summary>
    public const double PixelsPerCm = 3.0;

    public static double EdgeLengthPx(PieceDefinition piece, int edgeIndex)
    {
        if (piece.Points.Count < 2) return 0;
        var n = piece.Points.Count;
        var i = ((edgeIndex % n) + n) % n;
        var j = (i + 1) % n;
        var x1 = piece.Points[i][0] + piece.OffsetX;
        var y1 = piece.Points[i][1] + piece.OffsetY;
        var x2 = piece.Points[j][0] + piece.OffsetX;
        var y2 = piece.Points[j][1] + piece.OffsetY;
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double PerimeterPx(PieceDefinition piece) =>
        Enumerable.Range(0, piece.Points.Count).Sum(i => EdgeLengthPx(piece, i));

    public static double ToCm(double pixels) => pixels / PixelsPerCm;

    public static PieceDefinition? FindPiece(IReadOnlyList<PieceDefinition> pieces, string name) =>
        pieces.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
