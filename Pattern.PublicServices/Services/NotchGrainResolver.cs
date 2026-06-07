using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>
/// Post-processing after <see cref="PatternDraftingService.GradeCanvasPiecesForSize"/> for export.
/// Order: (1) snap manual notches onto the <b>graded</b> outline — fixes morph drift and keeps
/// notch triangles on the boundary next to seam allowance; (2) fill grain if missing;
/// (3) add catalog rule notches on current edges (deduped with manual).
/// </summary>
public static class NotchGrainResolver
{
    private const double DedupePx = 10.0;

    public static void ApplyAutomation(IList<PieceDefinition> pieces, string styleKey)
    {
        if (pieces.Count == 0) return;

        var rules = StyleAssemblyCatalog.GetNotchRules(styleKey);
        foreach (var piece in pieces)
        {
            SnapManualNotchesToGradedOutline(piece);
            EnsureGrainIfMissing(piece);
            MergeRuleNotches(piece, rules);
        }
    }

    /// <summary>
    /// Projects each manual notch to the closest point on the current piece polygon.
    /// Grading morphs notch coordinates via template deltas; that path can leave points slightly off the edge
    /// or use bbox scaling — snapping restores SA/export alignment on the sew/cut boundary.
    /// </summary>
    private static void SnapManualNotchesToGradedOutline(PieceDefinition piece)
    {
        if (piece.Notches is not { Count: > 0 }) return;
        var poly = piece.Points;
        if (poly.Count < 2)
        {
            piece.Notches = [];
            return;
        }

        var snapped = new List<int[]>(piece.Notches.Count);
        foreach (var n in piece.Notches)
        {
            if (n.Length < 2) continue;
            var (px, py) = ProjectOntoClosedPolygonBoundary(poly, n[0], n[1]);
            snapped.Add([(int)Math.Round(px), (int)Math.Round(py)]);
        }

        piece.Notches = snapped;
    }

    private static (double X, double Y) ProjectOntoClosedPolygonBoundary(IReadOnlyList<int[]> poly, int x, int y)
    {
        double best = double.MaxValue;
        var bx = (double)x;
        var by = (double)y;
        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            var ax = poly[i][0];
            var ay = poly[i][1];
            var cx = poly[j][0];
            var cy = poly[j][1];
            var (qx, qy, d) = ClosestPointOnSegment(x, y, ax, ay, cx, cy);
            if (d < best)
            {
                best = d;
                bx = qx;
                by = qy;
            }
        }

        return (bx, by);
    }

    private static (double X, double Y, double Dist) ClosestPointOnSegment(
        double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-18)
            return (ax, ay, Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay)));
        var t = ((px - ax) * dx + (py - ay) * dy) / len2;
        t = Math.Clamp(t, 0.0, 1.0);
        var qx = ax + dx * t;
        var qy = ay + dy * t;
        var dist = Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
        return (qx, qy, dist);
    }

    private static void EnsureGrainIfMissing(PieceDefinition p)
    {
        if (p.Grain is { Count: >= 2 })
        {
            var g0 = p.Grain[0];
            var g1 = p.Grain[^1];
            var gl = Math.Sqrt(Math.Pow(g1[0] - g0[0], 2) + Math.Pow(g1[1] - g0[1], 2));
            if (gl > 4.0) return;
        }

        var pts = p.Points;
        if (pts.Count < 2) return;

        var xs = pts.Select(pt => (double)pt[0]).ToArray();
        var ys = pts.Select(pt => (double)pt[1]).ToArray();
        var cx = (xs.Min() + xs.Max()) / 2.0;
        var ymin = ys.Min();
        var ymax = ys.Max();
        var pad = Math.Max(3, (ymax - ymin) * 0.02);
        p.Grain =
        [
            new[] { (int)Math.Round(cx), (int)Math.Round(ymin + pad) },
            new[] { (int)Math.Round(cx), (int)Math.Round(ymax - pad) },
        ];
    }

    private static void MergeRuleNotches(PieceDefinition piece, IReadOnlyList<NotchRuleDefinition> rules)
    {
        var mine = rules.Where(r =>
            r.PieceName.Equals(piece.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0) return;

        piece.Notches ??= [];
        var manual = piece.Notches.ToList();

        foreach (var rule in mine)
        {
            var resolved = ResolvePointOnEdge(piece.Points, rule.EdgeIndex, rule.T, rule.DistanceFromStart);
            if (resolved is null) continue;
            if (manual.Any(m => Distance(m, resolved) < DedupePx))
                continue;
            manual.Add(resolved);
        }

        piece.Notches = manual;
    }

    private static double Distance(int[] a, int[] b) =>
        Math.Sqrt(Math.Pow(a[0] - b[0], 2) + Math.Pow(a[1] - b[1], 2));

    /// <summary>World-space point on piece polygon in local coordinates (before OffsetX/Y).</summary>
    public static int[]? ResolvePointOnEdge(IReadOnlyList<int[]> poly, int edgeIndex, double t, double? distanceFromStart)
    {
        if (poly.Count < 2 || edgeIndex < 0 || edgeIndex >= poly.Count)
            return null;

        var i = edgeIndex;
        var j = (edgeIndex + 1) % poly.Count;
        var ax = poly[i][0];
        var ay = poly[i][1];
        var bx = poly[j][0];
        var by = poly[j][1];
        var dx = bx - ax;
        var dy = by - ay;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return null;

        var tt = t;
        if (distanceFromStart.HasValue)
            tt = Math.Clamp(distanceFromStart.Value / len, 0.0, 1.0);

        tt = Math.Clamp(tt, 0.0, 1.0);
        var x = ax + dx * tt;
        var y = ay + dy * tt;
        return [(int)Math.Round(x), (int)Math.Round(y)];
    }

    /// <summary>Centroid of polygon (local coords).</summary>
    public static (double X, double Y) Centroid(IReadOnlyList<int[]> poly)
    {
        if (poly.Count == 0) return (0, 0);
        double a = 0, cx = 0, cy = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            var cross = poly[i][0] * (double)poly[j][1] - poly[j][0] * (double)poly[i][1];
            a += cross;
            cx += (poly[i][0] + poly[j][0]) * cross;
            cy += (poly[i][1] + poly[j][1]) * cross;
        }

        if (Math.Abs(a) < 1e-9)
        {
            var sx = poly.Sum(p => p[0]) / (double)poly.Count;
            var sy = poly.Sum(p => p[1]) / (double)poly.Count;
            return (sx, sy);
        }

        a *= 0.5;
        cx /= (6.0 * a);
        cy /= (6.0 * a);
        return (cx, cy);
    }

    /// <summary>
    /// Unit inward normal at point on edge (toward polygon interior).
    /// </summary>
    public static (double nx, double ny) InwardNormal(IReadOnlyList<int[]> poly, int edgeIndex, double tEdge)
    {
        var i = edgeIndex;
        var j = (edgeIndex + 1) % poly.Count;
        var ax = poly[i][0];
        var ay = poly[i][1];
        var bx = poly[j][0];
        var by = poly[j][1];
        double ex = bx - ax;
        double ey = by - ay;
        var el = Math.Sqrt(ex * ex + ey * ey);
        if (el < 1e-9) return (0, 1);
        ex /= el;
        ey /= el;
        // Left normal of edge direction
        var nx = -ey;
        var ny = ex;
        var px = ax + ex * el * tEdge;
        var py = ay + ey * el * tEdge;
        var (cx, cy) = Centroid(poly);
        var vx = cx - px;
        var vy = cy - py;
        if (nx * vx + ny * vy < 0)
        {
            nx = -nx;
            ny = -ny;
        }

        return (nx, ny);
    }
}
