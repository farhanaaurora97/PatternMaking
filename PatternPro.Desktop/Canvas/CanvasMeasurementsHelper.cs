using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

public sealed class CanvasLiveMeasurements
{
    public bool IsLegPiece { get; init; }
    public float WaistArcPx { get; init; }
    public float InseamPx { get; init; }
    public float RisePx { get; init; }
    public float SelectedEdgePx { get; init; }
    public int? WaistEdgeIndex { get; init; }
    public int? InseamEdgeIndex { get; init; }
    public int? SelectedEdgeIndex { get; init; }
}

internal static class CanvasMeasurementsHelper
{
    /// <summary>Waist / inseam / rise labels only apply to trouser leg panels.</summary>
    public static bool SupportsLegLiveMeasurements(CoreModel.PieceDefinition piece)
    {
        var name = piece.Name ?? "";
        return name.Contains("leg", StringComparison.OrdinalIgnoreCase);
    }

    public static CanvasLiveMeasurements Compute(
        CoreModel.PieceDefinition piece,
        int? selectedEdgeIndex = null)
    {
        var selectedLen = selectedEdgeIndex is int se && se >= 0 && se < piece.Points.Count
            ? PiecePathBuilder.EdgeLength(piece, se)
            : 0f;

        if (piece.Points.Count < 2 || !SupportsLegLiveMeasurements(piece))
        {
            return new CanvasLiveMeasurements
            {
                IsLegPiece = false,
                SelectedEdgePx = selectedLen,
                SelectedEdgeIndex = selectedEdgeIndex,
            };
        }

        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var pt in piece.Points)
        {
            if (pt.Length < 2) continue;
            var wy = pt[1] + piece.OffsetY;
            minY = Math.Min(minY, wy);
            maxY = Math.Max(maxY, wy);
        }

        var rise = maxY - minY;
        var waistEdge = FindTopEdgeIndex(piece);
        var inseamEdge = FindInseamEdgeIndex(piece);
        var waistArc = waistEdge is int we ? PiecePathBuilder.EdgeLength(piece, we) : 0f;
        var inseam = inseamEdge is int ie ? PiecePathBuilder.EdgeLength(piece, ie) : 0f;

        return new CanvasLiveMeasurements
        {
            IsLegPiece = true,
            WaistArcPx = waistArc,
            InseamPx = inseam,
            RisePx = rise,
            SelectedEdgePx = selectedLen,
            WaistEdgeIndex = waistEdge,
            InseamEdgeIndex = inseamEdge,
            SelectedEdgeIndex = selectedEdgeIndex,
        };
    }

    private static int? FindTopEdgeIndex(CoreModel.PieceDefinition piece)
    {
        int? best = null;
        var bestY = float.MaxValue;
        for (var i = 0; i < piece.Points.Count; i++)
        {
            var pts = PiecePathBuilder.TessellateEdge(piece, i);
            if (pts.Count == 0) continue;
            var avgY = pts.Average(p => p.Y + piece.OffsetY);
            if (avgY >= bestY) continue;
            bestY = avgY;
            best = i;
        }

        return best;
    }

    private static int? FindInseamEdgeIndex(CoreModel.PieceDefinition piece)
    {
        int? best = null;
        var bestLen = 0f;
        for (var i = 0; i < piece.Points.Count; i++)
        {
            var pts = PiecePathBuilder.TessellateEdge(piece, i);
            if (pts.Count < 2) continue;
            var ax = pts[0].X;
            var ay = pts[0].Y;
            var bx = pts[^1].X;
            var by = pts[^1].Y;
            var dx = Math.Abs(bx - ax);
            var dy = Math.Abs(by - ay);
            if (dy <= dx * 1.2f) continue;

            var len = PiecePathBuilder.EdgeLength(piece, i);
            if (len <= bestLen) continue;
            bestLen = len;
            best = i;
        }

        return best;
    }
}
