using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasGeometryHelper
{
    public static (float Wx, float Wy) ScreenToWorld(float sx, float sy, CanvasViewport viewport) =>
        ((sx - viewport.PanX) / viewport.Scale, (sy - viewport.PanY) / viewport.Scale);

    public static int? HitVertex(CoreModel.PieceDefinition piece, float wx, float wy, float scale, float hitPx = 10f)
    {
        var r2 = (hitPx / scale) * (hitPx / scale);
        for (var i = 0; i < piece.Points.Count; i++)
        {
            var pt = piece.Points[i];
            if (pt.Length < 2) continue;
            var x = pt[0] + piece.OffsetX;
            var y = pt[1] + piece.OffsetY;
            var dx = x - wx;
            var dy = y - wy;
            if (dx * dx + dy * dy <= r2)
                return i;
        }
        return null;
    }

    public static bool HitPieceBody(CoreModel.PieceDefinition piece, float wx, float wy)
    {
        if (piece.Points.Count < 3) return false;
        var inside = false;
        for (int i = 0, j = piece.Points.Count - 1; i < piece.Points.Count; j = i++)
        {
            var pi = piece.Points[i];
            var pj = piece.Points[j];
            if (pi.Length < 2 || pj.Length < 2) continue;
            var xi = pi[0] + piece.OffsetX;
            var yi = pi[1] + piece.OffsetY;
            var xj = pj[0] + piece.OffsetX;
            var yj = pj[1] + piece.OffsetY;
            var intersect = yi > wy != yj > wy &&
                wx < (xj - xi) * (wy - yi) / (yj - yi + float.Epsilon) + xi;
            if (intersect) inside = !inside;
        }
        return inside;
    }

    public static (float T, float Distance) ClosestOnSegment(
        float px, float py, float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = dx * dx + dy * dy;
        if (len2 <= float.Epsilon)
            return (0f, (float)Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay)));

        var t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / len2, 0f, 1f);
        var cx = ax + t * dx;
        var cy = ay + t * dy;
        var ddx = px - cx;
        var ddy = py - cy;
        return (t, (float)Math.Sqrt(ddx * ddx + ddy * ddy));
    }

    public static int? HitNotch(CoreModel.PieceDefinition piece, float wx, float wy, float scale, float hitPx = 12f)
    {
        if (piece.Notches is not { Count: > 0 }) return null;
        var r2 = (hitPx / scale) * (hitPx / scale);
        for (var i = 0; i < piece.Notches.Count; i++)
        {
            var n = piece.Notches[i];
            if (n.Length < 2) continue;
            var x = n[0] + piece.OffsetX;
            var y = n[1] + piece.OffsetY;
            var dx = x - wx;
            var dy = y - wy;
            if (dx * dx + dy * dy <= r2)
                return i;
        }
        return null;
    }

    public static bool TryInsertPointOnEdge(
        CoreModel.PieceDefinition piece, float wx, float wy, float scale, float maxDistPx = 25f)
    {
        var lx = wx - piece.OffsetX;
        var ly = wy - piece.OffsetY;
        var bestDist = float.MaxValue;
        var bestIdx = -1;
        var bestT = 0f;

        for (var i = 0; i < piece.Points.Count; i++)
        {
            var a = piece.Points[i];
            var b = piece.Points[(i + 1) % piece.Points.Count];
            if (a.Length < 2 || b.Length < 2) continue;
            var (t, d) = ClosestOnSegment(lx, ly, a[0], a[1], b[0], b[1]);
            if (d >= bestDist) continue;
            bestDist = d;
            bestIdx = i;
            bestT = t;
        }

        if (bestIdx < 0 || bestDist > maxDistPx / scale) return false;

        var edgeA = piece.Points[bestIdx];
        var edgeB = piece.Points[(bestIdx + 1) % piece.Points.Count];
        piece.Points.Insert(bestIdx + 1,
        [
            (int)Math.Round(edgeA[0] + (edgeB[0] - edgeA[0]) * bestT),
            (int)Math.Round(edgeA[1] + (edgeB[1] - edgeA[1]) * bestT),
        ]);
        return true;
    }

    public static bool TryAddNotchOnEdge(
        CoreModel.PieceDefinition piece, float wx, float wy, float scale, float maxDistPx = 30f)
    {
        var lx = wx - piece.OffsetX;
        var ly = wy - piece.OffsetY;
        var bestDist = float.MaxValue;
        var bestT = 0f;
        int[]? edgeA = null;
        int[]? edgeB = null;

        for (var i = 0; i < piece.Points.Count; i++)
        {
            var a = piece.Points[i];
            var b = piece.Points[(i + 1) % piece.Points.Count];
            if (a.Length < 2 || b.Length < 2) continue;
            var (t, d) = ClosestOnSegment(lx, ly, a[0], a[1], b[0], b[1]);
            if (d >= bestDist) continue;
            bestDist = d;
            bestT = t;
            edgeA = a;
            edgeB = b;
        }

        if (edgeA is null || edgeB is null || bestDist > maxDistPx / scale) return false;

        piece.Notches ??= [];
        piece.Notches.Add(
        [
            (int)Math.Round(edgeA[0] + (edgeB[0] - edgeA[0]) * bestT),
            (int)Math.Round(edgeA[1] + (edgeB[1] - edgeA[1]) * bestT),
        ]);
        return true;
    }
}
