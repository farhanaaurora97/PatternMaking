namespace Pattern.Core.Model;

/// <summary>Shared outline math for canvas rendering and DXF export (no Skia dependency).</summary>
public static class PieceOutlineTessellator
{
    private const int DefaultCurveSegments = 16;

    public static bool IsCurved(PieceDefinition piece, int edgeIndex)
    {
        if (piece.Edges is null || edgeIndex < 0 || edgeIndex >= piece.Edges.Count)
            return false;
        var edge = piece.Edges[edgeIndex];
        return edge.Kind is "quad" or "cubic" && edge.C1 is { Length: >= 2 };
    }

    public static IReadOnlyList<(int X, int Y)> TessellateOutline(PieceDefinition piece, int segments = DefaultCurveSegments)
    {
        var result = new List<(int X, int Y)>();
        if (piece.Points.Count < 3)
            return result;

        for (var i = 0; i < piece.Points.Count; i++)
        {
            var edgePts = TessellateEdge(piece, i, segments);
            var start = i == 0 ? 0 : 1;
            for (var j = start; j < edgePts.Count; j++)
                result.Add(edgePts[j]);
        }

        return result;
    }

    public static List<(int X, int Y)> TessellateEdge(PieceDefinition piece, int edgeIndex, int segments = DefaultCurveSegments)
    {
        var pts = piece.Points;
        var a = pts[edgeIndex];
        var b = pts[(edgeIndex + 1) % pts.Count];
        var result = new List<(int X, int Y)> { (a[0], a[1]) };

        if (!IsCurved(piece, edgeIndex))
        {
            result.Add((b[0], b[1]));
            return result;
        }

        var edge = piece.Edges![edgeIndex];
        var c1 = edge.C1!;
        if (edge.Kind == "cubic" && edge.C2 is { Length: >= 2 } c2)
        {
            for (var s = 1; s <= segments; s++)
            {
                var t = s / (float)segments;
                result.Add(CubicPoint(a[0], a[1], c1[0], c1[1], c2[0], c2[1], b[0], b[1], t));
            }
            return result;
        }

        for (var s = 1; s <= segments; s++)
        {
            var t = s / (float)segments;
            result.Add(QuadPoint(a[0], a[1], c1[0], c1[1], b[0], b[1], t));
        }

        return result;
    }

    public static bool HasCurves(PieceDefinition piece) =>
        piece.Edges?.Any(e => e.Kind is "quad" or "cubic" && e.C1 is { Length: >= 2 }) == true;

    private static (int X, int Y) QuadPoint(int x0, int y0, int cx, int cy, int x1, int y1, float t)
    {
        var u = 1f - t;
        return (
            (int)Math.Round(u * u * x0 + 2f * u * t * cx + t * t * x1),
            (int)Math.Round(u * u * y0 + 2f * u * t * cy + t * t * y1));
    }

    private static (int X, int Y) CubicPoint(
        int x0, int y0, int c1x, int c1y, int c2x, int c2y, int x1, int y1, float t)
    {
        var u = 1f - t;
        return (
            (int)Math.Round(u * u * u * x0 + 3f * u * u * t * c1x + 3f * u * t * t * c2x + t * t * t * x1),
            (int)Math.Round(u * u * u * y0 + 3f * u * u * t * c1y + 3f * u * t * t * c2y + t * t * t * y1));
    }
}
