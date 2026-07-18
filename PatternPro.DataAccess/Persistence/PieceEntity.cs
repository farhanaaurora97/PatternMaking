namespace PatternPro.DataAccess.Persistence;

/// <summary>
/// Stores one piece definition either for a style template (<see cref="StyleKey"/>) or a saved pattern
/// (<see cref="PatternId"/>). Geometry lives in <see cref="Vertices"/>.
/// </summary>
public class PieceEntity
{
    public int Id { get; set; }
    public int? PatternId { get; set; }
    public string? StyleKey { get; set; }
    public int PieceOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PieceNumber { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public bool OnFold { get; set; }
    public string Cut { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string GrainLine { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public double SeamAllowance { get; set; }
    public string SeamAllowanceJoin { get; set; } = "miter";

    public List<PieceVertexEntity> Vertices { get; set; } = [];
}
