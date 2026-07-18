namespace PatternPro.DataAccess.Persistence;

/// <summary>
/// Stores one ordered point for a piece. <see cref="Kind"/> identifies which polyline/list it belongs to:
/// outline, grain, cf, or notch.
/// </summary>
public class PieceVertexEntity
{
    public int Id { get; set; }
    public int PieceId { get; set; }
    public string Kind { get; set; } = "outline";
    public int PointOrder { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public PieceEntity Piece { get; set; } = null!;
}
