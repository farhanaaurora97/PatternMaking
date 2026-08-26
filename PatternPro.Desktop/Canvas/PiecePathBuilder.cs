using Pattern.Core.Model;
using SkiaSharp;

namespace PatternPro.Desktop.Canvas;

internal static class PiecePathBuilder
{
    private const int DefaultCurveSegments = 16;

    public static void EnsureEdges(PieceDefinition piece)
    {
        var n = piece.Points.Count;
        if (n == 0)
        {
            piece.Edges = null;
            return;
        }

        piece.Edges ??= [];
        while (piece.Edges.Count < n)
            piece.Edges.Add(new PieceEdge());
        if (piece.Edges.Count > n)
            piece.Edges.RemoveRange(n, piece.Edges.Count - n);
    }

    public static bool IsCurved(PieceDefinition piece, int edgeIndex) =>
        PieceOutlineTessellator.IsCurved(piece, edgeIndex);

    public static SKPath BuildPath(PieceDefinition piece)
    {
        var path = new SKPath();
        var pts = piece.Points;
        if (pts.Count < 3)
            return path;

        EnsureEdges(piece);
        var ox = piece.OffsetX;
        var oy = piece.OffsetY;

        var first = pts[0];
        path.MoveTo(first[0] + ox, first[1] + oy);
        for (var i = 0; i < pts.Count; i++)
            AppendEdge(path, piece, i, ox, oy, moveOnly: false);

        path.Close();
        return path;
    }

    public static void AppendEdge(SKPath path, PieceDefinition piece, int edgeIndex, int ox, int oy, bool moveOnly)
    {
        var pts = piece.Points;
        var a = pts[edgeIndex];
        var b = pts[(edgeIndex + 1) % pts.Count];
        if (a.Length < 2 || b.Length < 2)
            return;

        var ax = a[0] + ox;
        var ay = a[1] + oy;
        var bx = b[0] + ox;
        var by = b[1] + oy;

        if (moveOnly)
        {
            path.MoveTo(ax, ay);
            return;
        }

        if (piece.Edges is null || edgeIndex >= piece.Edges.Count || !IsCurved(piece, edgeIndex))
        {
            path.LineTo(bx, by);
            return;
        }

        var edge = piece.Edges[edgeIndex];
        var c1 = edge.C1!;
        var c1x = c1[0] + ox;
        var c1y = c1[1] + oy;

        if (edge.Kind == "cubic" && edge.C2 is { Length: >= 2 })
        {
            var c2 = edge.C2;
            path.CubicTo(c1x, c1y, c2[0] + ox, c2[1] + oy, bx, by);
            return;
        }

        path.QuadTo(c1x, c1y, bx, by);
    }

    public static float EdgeLength(PieceDefinition piece, int edgeIndex, int segments = DefaultCurveSegments)
    {
        var pts = TessellateEdge(piece, edgeIndex, segments);
        var len = 0f;
        for (var i = 1; i < pts.Count; i++)
        {
            var dx = pts[i].X - pts[i - 1].X;
            var dy = pts[i].Y - pts[i - 1].Y;
            len += MathF.Sqrt(dx * dx + dy * dy);
        }
        return len;
    }

    public static float Perimeter(PieceDefinition piece, int segments = DefaultCurveSegments)
    {
        if (piece.Points.Count < 2) return 0f;
        var total = 0f;
        for (var i = 0; i < piece.Points.Count; i++)
            total += EdgeLength(piece, i, segments);
        return total;
    }

    public static List<(float X, float Y)> Tessellate(PieceDefinition piece, int segments = DefaultCurveSegments)
    {
        var result = new List<(float X, float Y)>();
        if (piece.Points.Count < 3)
            return result;

        EnsureEdges(piece);
        var ox = piece.OffsetX;
        var oy = piece.OffsetY;

        for (var i = 0; i < piece.Points.Count; i++)
        {
            var edgePts = TessellateEdge(piece, i, segments);
            var start = i == 0 ? 0 : 1;
            for (var j = start; j < edgePts.Count; j++)
                result.Add((edgePts[j].X + ox, edgePts[j].Y + oy));
        }

        return result;
    }

    public static List<(float X, float Y)> TessellateEdge(PieceDefinition piece, int edgeIndex, int segments = DefaultCurveSegments)
    {
        return PieceOutlineTessellator.TessellateEdge(piece, edgeIndex, segments)
            .Select(p => ((float)p.X, (float)p.Y))
            .ToList();
    }

    public static int? HitEdge(PieceDefinition piece, float wx, float wy, float scale, float hitPx = 14f)
    {
        if (piece.Points.Count < 2) return null;
        EnsureEdges(piece);

        var lx = wx - piece.OffsetX;
        var ly = wy - piece.OffsetY;
        var bestDist = float.MaxValue;
        int? bestEdge = null;

        for (var i = 0; i < piece.Points.Count; i++)
        {
            var edgePts = TessellateEdge(piece, i, DefaultCurveSegments);
            for (var j = 1; j < edgePts.Count; j++)
            {
                var (_, d) = CanvasGeometryHelper.ClosestOnSegment(
                    lx, ly,
                    edgePts[j - 1].X, edgePts[j - 1].Y,
                    edgePts[j].X, edgePts[j].Y);
                if (d >= bestDist) continue;
                bestDist = d;
                bestEdge = i;
            }
        }

        return bestDist <= hitPx / scale ? bestEdge : null;
    }

    public static (int EdgeIndex, int HandleIndex)? HitCurveHandle(
        PieceDefinition piece, float wx, float wy, float scale, float hitPx = 10f)
    {
        if (piece.Edges is null) return null;
        var r2 = (hitPx / scale) * (hitPx / scale);
        var ox = piece.OffsetX;
        var oy = piece.OffsetY;

        for (var i = 0; i < piece.Edges.Count; i++)
        {
            if (!IsCurved(piece, i)) continue;
            var edge = piece.Edges[i];
            if (edge.C1 is { Length: >= 2 } c1)
            {
                var dx = c1[0] + ox - wx;
                var dy = c1[1] + oy - wy;
                if (dx * dx + dy * dy <= r2)
                    return (i, 0);
            }

            if (edge.Kind == "cubic" && edge.C2 is { Length: >= 2 } c2)
            {
                var dx = c2[0] + ox - wx;
                var dy = c2[1] + oy - wy;
                if (dx * dx + dy * dy <= r2)
                    return (i, 1);
            }
        }

        return null;
    }

    public static void SetQuadraticEdge(PieceDefinition piece, int edgeIndex, int c1x, int c1y)
    {
        EnsureEdges(piece);
        var sa = piece.Edges![edgeIndex].SeamAllowance;
        piece.Edges![edgeIndex] = new PieceEdge
        {
            Kind = "quad",
            C1 = [c1x, c1y],
            SeamAllowance = sa,
        };
    }

    /// <summary>Cubic Bezier with tangent-aligned handles; optional bulge point shapes the curve.</summary>
    public static void SetCubicEdgeWithTangents(PieceDefinition piece, int edgeIndex, int? bulgeLocalX = null, int? bulgeLocalY = null)
    {
        EnsureEdges(piece);
        var sa = piece.Edges![edgeIndex].SeamAllowance;
        var a = piece.Points[edgeIndex];
        var b = piece.Points[(edgeIndex + 1) % piece.Points.Count];
        var dx = b[0] - a[0];
        var dy = b[1] - a[1];
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1e-3f)
        {
            SetLineEdge(piece, edgeIndex);
            return;
        }

        const float t = 0.33f;
        var c1x = a[0] + dx * t;
        var c1y = a[1] + dy * t;
        var c2x = a[0] + dx * (1f - t);
        var c2y = a[1] + dy * (1f - t);

        if (bulgeLocalX is int bx && bulgeLocalY is int by)
        {
            var midX = (a[0] + b[0]) / 2f;
            var midY = (a[1] + b[1]) / 2f;
            var offX = bx - midX;
            var offY = by - midY;
            c1x += offX * 0.65f;
            c1y += offY * 0.65f;
            c2x += offX * 0.65f;
            c2y += offY * 0.65f;
        }

        piece.Edges![edgeIndex] = new PieceEdge
        {
            Kind = "cubic",
            C1 = [(int)Math.Round(c1x), (int)Math.Round(c1y)],
            C2 = [(int)Math.Round(c2x), (int)Math.Round(c2y)],
            SeamAllowance = sa,
        };
    }

    public static void PromoteAdjacentCurvesToCubic(PieceDefinition piece, int edgeIndex)
    {
        EnsureEdges(piece);
        var n = piece.Points.Count;
        var prev = (edgeIndex - 1 + n) % n;
        if (IsCurved(piece, prev) && piece.Edges![prev].Kind == "quad" && piece.Edges[prev].C1 is { Length: >= 2 } c1)
        {
            var a = piece.Points[prev];
            var b = piece.Points[(prev + 1) % n];
            var sa = piece.Edges[prev].SeamAllowance;
            piece.Edges[prev] = new PieceEdge
            {
                Kind = "cubic",
                C1 = [c1[0], c1[1]],
                C2 = [(int)Math.Round((a[0] + b[0]) / 2f), (int)Math.Round((a[1] + b[1]) / 2f)],
                SeamAllowance = sa,
            };
        }
    }

    public static void SetLineEdge(PieceDefinition piece, int edgeIndex)
    {
        EnsureEdges(piece);
        piece.Edges![edgeIndex] = new PieceEdge { Kind = "line" };
    }

    public static void SmoothVertex(PieceDefinition piece, int vertexIndex)
    {
        if (piece.Points.Count < 3) return;
        EnsureEdges(piece);

        var n = piece.Points.Count;
        var prevEdge = (vertexIndex - 1 + n) % n;
        var nextEdge = vertexIndex;
        var p = piece.Points[vertexIndex];
        var prev = piece.Points[prevEdge];
        var next = piece.Points[(vertexIndex + 1) % n];

        var inDx = p[0] - prev[0];
        var inDy = p[1] - prev[1];
        var outDx = next[0] - p[0];
        var outDy = next[1] - p[1];
        var inLen = MathF.Sqrt(inDx * inDx + inDy * inDy);
        var outLen = MathF.Sqrt(outDx * outDx + outDy * outDy);
        if (inLen < 1e-3f || outLen < 1e-3f) return;

        var smooth = 0.35f;
        var cInX = (int)Math.Round(p[0] - inDx / inLen * inLen * smooth);
        var cInY = (int)Math.Round(p[1] - inDy / inLen * inLen * smooth);
        var cOutX = (int)Math.Round(p[0] + outDx / outLen * outLen * smooth);
        var cOutY = (int)Math.Round(p[1] + outDy / outLen * outLen * smooth);

        piece.Edges![prevEdge] = new PieceEdge { Kind = "quad", C1 = [cInX, cInY] };
        piece.Edges![nextEdge] = new PieceEdge { Kind = "quad", C1 = [cOutX, cOutY] };
    }

    public static void SplitEdgeOnInsert(PieceDefinition piece, int edgeIndex, float t)
    {
        EnsureEdges(piece);
        if (!IsCurved(piece, edgeIndex))
        {
            piece.Edges!.Insert(edgeIndex + 1, new PieceEdge { Kind = "line" });
            return;
        }

        var pts = TessellateEdge(piece, edgeIndex, DefaultCurveSegments);
        var target = Math.Clamp((int)Math.Round(t * DefaultCurveSegments), 1, DefaultCurveSegments);
        var mid = pts[Math.Min(target, pts.Count - 1)];
        piece.Edges![edgeIndex] = new PieceEdge { Kind = "line" };
        piece.Edges!.Insert(edgeIndex + 1, new PieceEdge { Kind = "line" });
        _ = mid;
    }

    public static void RemoveVertexEdges(PieceDefinition piece, int vertexIndex)
    {
        if (piece.Edges is null || piece.Edges.Count == 0) return;
        if (vertexIndex < piece.Edges.Count)
            piece.Edges.RemoveAt(vertexIndex);
        if (piece.Edges.Count > 0)
            piece.Edges[(vertexIndex - 1 + piece.Edges.Count) % piece.Edges.Count] = new PieceEdge { Kind = "line" };
    }
}
