using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>
/// Post-draft automation for bottom-wear: balance key seams, apply production seam allowance, snap notches/grain.
/// </summary>
public static class PatternAutoRefineService
{
    private const double SeamTolerancePx = SeamGeometry.PixelsPerCm * 0.75;

    public static void Refine(IList<PieceDefinition> pieces, string styleKey)
    {
        if (pieces.Count == 0) return;

        BalanceWaistbandToLegs(pieces);
        foreach (var pair in StyleAssemblyCatalog.GetSeamPairs(styleKey))
            AlignSeamEdgeLengths(pieces, pair.PieceA, pair.EdgeIndexA, pair.PieceB, pair.EdgeIndexB);
        ApplyProductionSeamAllowances(pieces);
        NotchGrainResolver.ApplyAutomation(pieces, styleKey);
    }

    /// <summary>Waistband top edge length = front waist edge + back waist edge.</summary>
    public static void BalanceWaistbandToLegs(IList<PieceDefinition> pieces)
    {
        var front = SeamGeometry.FindPiece(pieces, "Front Leg");
        var back = SeamGeometry.FindPiece(pieces, "Back Leg");
        var wb = SeamGeometry.FindPiece(pieces, "Waistband");
        if (front is null || back is null || wb is null || wb.Points.Count < 4)
            return;

        var target = SeamGeometry.EdgeLengthPx(front, 0) + SeamGeometry.EdgeLengthPx(back, 0);
        var current = SeamGeometry.EdgeLengthPx(wb, 0);
        if (current < 1 || Math.Abs(target - current) <= SeamTolerancePx)
            return;

        var scale = target / current;
        var anchorX = wb.Points[0][0];
        for (var i = 0; i < wb.Points.Count; i++)
        {
            if (i == 0) continue;
            wb.Points[i][0] = (int)Math.Round(anchorX + (wb.Points[i][0] - anchorX) * scale);
        }
    }

    /// <summary>Shortens the longer edge by moving its end vertex toward the start (keeps vertex count).</summary>
    public static void AlignSeamEdgeLengths(
        IList<PieceDefinition> pieces,
        string pieceA, int edgeA,
        string pieceB, int edgeB)
    {
        var a = SeamGeometry.FindPiece(pieces, pieceA);
        var b = SeamGeometry.FindPiece(pieces, pieceB);
        if (a is null || b is null) return;

        var lenA = SeamGeometry.EdgeLengthPx(a, edgeA);
        var lenB = SeamGeometry.EdgeLengthPx(b, edgeB);
        var diff = Math.Abs(lenA - lenB);
        if (diff <= SeamTolerancePx) return;

        if (lenA > lenB)
            ShortenEdge(a, edgeA, lenB);
        else
            ShortenEdge(b, edgeB, lenA);
    }

    private static void ShortenEdge(PieceDefinition piece, int edgeIndex, double targetLen)
    {
        var n = piece.Points.Count;
        if (n < 2) return;
        var i = ((edgeIndex % n) + n) % n;
        var j = (i + 1) % n;
        var ax = piece.Points[i][0];
        var ay = piece.Points[i][1];
        var bx = piece.Points[j][0];
        var by = piece.Points[j][1];
        var dx = bx - ax;
        var dy = by - ay;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        var t = targetLen / len;
        piece.Points[j][0] = (int)Math.Round(ax + dx * t);
        piece.Points[j][1] = (int)Math.Round(ay + dy * t);
    }

    public static void ApplyProductionSeamAllowances(IList<PieceDefinition> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece.SeamAllowance > 0.0001) continue;
            if (piece.Category.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
                continue;

            var cm = piece.Category.Contains("Pockets", StringComparison.OrdinalIgnoreCase) ? 0.7
                : piece.Category.Contains("Closures", StringComparison.OrdinalIgnoreCase) ? 0.8
                : 1.0;

            piece.SeamAllowance = cm * SeamGeometry.PixelsPerCm;
            if (string.IsNullOrWhiteSpace(piece.SeamAllowanceJoin))
                piece.SeamAllowanceJoin = "miter";
        }
    }

    public static PieceDefinition Clone(PieceDefinition p) =>
        new()
        {
            Name              = p.Name,
            Cut               = p.Cut,
            Color             = p.Color,
            Category          = p.Category,
            GrainLine         = p.GrainLine,
            Description       = p.Description,
            Points            = p.Points.Select(pt => new[] { pt[0], pt[1] }).ToList(),
            Grain             = p.Grain?.Select(pt => new[] { pt[0], pt[1] }).ToList(),
            Cf                = p.Cf?.Select(pt => new[] { pt[0], pt[1] }).ToList(),
            Notches           = p.Notches?.Select(pt => new[] { pt[0], pt[1] }).ToList(),
            OffsetX           = p.OffsetX,
            OffsetY           = p.OffsetY,
            SeamAllowance     = p.SeamAllowance,
            SeamAllowanceJoin = p.SeamAllowanceJoin,
        };
}
