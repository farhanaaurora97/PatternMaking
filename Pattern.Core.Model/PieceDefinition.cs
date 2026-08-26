namespace Pattern.Core.Model;

public class PieceDefinition
{
    public string         Name        { get; set; } = string.Empty;
    /// <summary>Factory piece number (e.g. 01, 02).</summary>
    public string         PieceNumber { get; set; } = string.Empty;
    /// <summary>Fabric / material code for BOM handoff.</summary>
    public string         Material    { get; set; } = string.Empty;
    /// <summary>Cut on fold (factory marker hint).</summary>
    public bool           OnFold      { get; set; }
    public string         Cut         { get; set; } = string.Empty;
    public string         Color       { get; set; } = string.Empty;
    public string         Category    { get; set; } = "Hardware & Details";
    public string         GrainLine   { get; set; } = "Straight";
    public string         Description { get; set; } = string.Empty;
    public List<int[]>    Points      { get; set; } = [];
    /// <summary>Edge i connects Points[i] → Points[(i+1) % Count]. Omitted entries are straight lines.</summary>
    public List<PieceEdge>? Edges     { get; set; }
    public List<int[]>?   Grain       { get; set; }
    public List<int[]>?   Cf          { get; set; }
    public List<int[]>?   Notches     { get; set; }
    /// <summary>Internal construction guides (pocket, fly, etc.) — not cut lines.</summary>
    public List<PieceInternalLine>? InternalLines { get; set; }
    public int            OffsetX     { get; set; }
    public int            OffsetY     { get; set; }

    /// <summary>
    /// Seam allowance offset distance in canvas units (px). 0 = none.
    /// </summary>
    public double         SeamAllowance { get; set; }

    /// <summary>
    /// Join style for seam allowance corners: "miter", "bevel", or "round".
    /// </summary>
    public string         SeamAllowanceJoin { get; set; } = "miter";
}