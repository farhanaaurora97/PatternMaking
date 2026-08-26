namespace Pattern.Core.Model;

/// <summary>
/// Describes the segment from <see cref="PieceDefinition.Points"/>[i] to Points[(i+1) % Count].
/// Straight segments omit an entry or use Kind = "line".
/// </summary>
public class PieceEdge
{
    /// <summary>"line", "quad", or "cubic".</summary>
    public string Kind { get; set; } = "line";

    /// <summary>First control point in piece-local coordinates.</summary>
    public int[]? C1 { get; set; }

    /// <summary>Second control point for cubic curves.</summary>
    public int[]? C2 { get; set; }

    /// <summary>Per-edge seam allowance in canvas px. 0 = use piece default.</summary>
    public double SeamAllowance { get; set; }
}
