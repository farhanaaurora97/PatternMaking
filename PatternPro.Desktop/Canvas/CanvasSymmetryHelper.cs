using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasSymmetryHelper
{
    private const float PartnerTolerancePx = 18f;

    public static float ComputeAxisWorldX(CoreModel.PieceDefinition piece)
    {
        if (piece.Points.Count == 0)
            return piece.OffsetX;

        var minX = float.MaxValue;
        var maxX = float.MinValue;
        foreach (var pt in piece.Points)
        {
            if (pt.Length < 2) continue;
            var wx = pt[0] + piece.OffsetX;
            minX = Math.Min(minX, wx);
            maxX = Math.Max(maxX, wx);
        }

        return (minX + maxX) / 2f;
    }

    public static int? FindMirrorVertex(CoreModel.PieceDefinition piece, int vertexIndex, float axisWorldX)
    {
        if (vertexIndex < 0 || vertexIndex >= piece.Points.Count)
            return null;

        var src = piece.Points[vertexIndex];
        var srcWx = src[0] + piece.OffsetX;
        var srcWy = src[1] + piece.OffsetY;
        var mirrorWx = 2f * axisWorldX - srcWx;

        int? best = null;
        var bestScore = float.MaxValue;
        for (var i = 0; i < piece.Points.Count; i++)
        {
            if (i == vertexIndex) continue;
            var pt = piece.Points[i];
            if (pt.Length < 2) continue;
            var wx = pt[0] + piece.OffsetX;
            var wy = pt[1] + piece.OffsetY;
            var dx = wx - mirrorWx;
            var dy = wy - srcWy;
            var score = dx * dx + dy * dy;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return bestScore <= PartnerTolerancePx * PartnerTolerancePx ? best : null;
    }

    public static void MirrorVertexFromSource(
        CoreModel.PieceDefinition piece,
        int sourceIndex,
        int targetIndex,
        float axisWorldX)
    {
        if (sourceIndex < 0 || sourceIndex >= piece.Points.Count
            || targetIndex < 0 || targetIndex >= piece.Points.Count)
            return;

        var src = piece.Points[sourceIndex];
        var srcWx = src[0] + piece.OffsetX;
        var srcWy = src[1] + piece.OffsetY;
        var mirrorWx = 2f * axisWorldX - srcWx;
        piece.Points[targetIndex][0] = (int)Math.Round((double)(mirrorWx - piece.OffsetX));
        piece.Points[targetIndex][1] = (int)Math.Round((double)(srcWy - piece.OffsetY));
    }

    public static void ApplyMirrorAfterVertexEdit(
        CoreModel.PieceDefinition piece,
        int editedVertexIndex,
        float axisWorldX)
    {
        var partner = FindMirrorVertex(piece, editedVertexIndex, axisWorldX);
        if (partner is int pi)
            MirrorVertexFromSource(piece, editedVertexIndex, pi, axisWorldX);
    }
}
