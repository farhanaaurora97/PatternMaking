using System.Globalization;

namespace PatternPro.Business.Services;

internal static class SeamAllowanceOffset
{
    internal enum JoinStyle
    {
        Miter,
        Bevel,
        Round,
    }

    internal readonly record struct Pt(double X, double Y);

    internal static JoinStyle ParseJoin(string? join) =>
        (join ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "bevel" => JoinStyle.Bevel,
            "round" => JoinStyle.Round,
            _ => JoinStyle.Miter,
        };

    internal static IReadOnlyList<Pt> OffsetClosed(
        IReadOnlyList<Pt> pts,
        double offset,
        JoinStyle join,
        int roundSegments = 10,
        double miterLimit = 6.0)
    {
        if (pts.Count < 3 || Math.Abs(offset) < 0.0001)
            return pts;

        // Ensure polygon is CCW so "outward" is consistent.
        var signedArea = 0d;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            signedArea += (a.X * b.Y) - (b.X * a.Y);
        }
        var ccw = signedArea > 0;
        var outSign = ccw ? 1d : -1d;
        var d = offset * outSign;

        // Precompute edge unit normals (left normals for CCW).
        var n = pts.Count;
        var edgeNormals = new (double nx, double ny)[n];
        for (var i = 0; i < n; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var len = Math.Sqrt(ex * ex + ey * ey);
            if (len < 1e-9) { edgeNormals[i] = (0, 0); continue; }
            ex /= len; ey /= len;
            // left normal
            edgeNormals[i] = (-ey, ex);
        }

        var outPts = new List<Pt>(n * (join == JoinStyle.Round ? roundSegments : 2));

        for (var i = 0; i < n; i++)
        {
            var prev = (i - 1 + n) % n;
            var curr = i;
            var next = (i + 1) % n;

            var p = pts[curr];
            var n0 = edgeNormals[prev];
            var n1 = edgeNormals[curr];

            // Offset lines for edges prev and curr:
            // line0: through pts[curr] shifted by n0*d, direction = edge prev
            // line1: through pts[curr] shifted by n1*d, direction = edge curr
            var a0 = pts[prev];
            var a1 = pts[curr];
            var b1 = pts[next];

            var dir0x = a1.X - a0.X;
            var dir0y = a1.Y - a0.Y;
            var dir1x = b1.X - a1.X;
            var dir1y = b1.Y - a1.Y;

            var len0 = Math.Sqrt(dir0x * dir0x + dir0y * dir0y);
            var len1 = Math.Sqrt(dir1x * dir1x + dir1y * dir1y);
            if (len0 < 1e-9 || len1 < 1e-9)
            {
                outPts.Add(new Pt(p.X + n1.nx * d, p.Y + n1.ny * d));
                continue;
            }
            dir0x /= len0; dir0y /= len0;
            dir1x /= len1; dir1y /= len1;

            var l0p = new Pt(p.X + n0.nx * d, p.Y + n0.ny * d);
            var l1p = new Pt(p.X + n1.nx * d, p.Y + n1.ny * d);

            // Intersect the two offset rays. If parallel or miter too long, fallback.
            var denom = Cross(dir0x, dir0y, dir1x, dir1y);
            var isParallel = Math.Abs(denom) < 1e-9;

            var bevelA = new Pt(p.X + n0.nx * d, p.Y + n0.ny * d);
            var bevelB = new Pt(p.X + n1.nx * d, p.Y + n1.ny * d);

            if (isParallel)
            {
                outPts.Add(bevelB);
                continue;
            }

            // Solve l0p + t*dir0 = l1p + u*dir1
            var rx = l1p.X - l0p.X;
            var ry = l1p.Y - l0p.Y;
            var t = Cross(rx, ry, dir1x, dir1y) / denom;
            var ix = l0p.X + t * dir0x;
            var iy = l0p.Y + t * dir0y;

            // Miter length check relative to offset distance.
            var mx = ix - p.X;
            var my = iy - p.Y;
            var mLen = Math.Sqrt(mx * mx + my * my);
            if (mLen > Math.Abs(d) * miterLimit)
            {
                // Too spiky — bevel/round instead.
                if (join == JoinStyle.Round)
                    AddRound(outPts, p, bevelA, bevelB, roundSegments, ccw);
                else
                {
                    outPts.Add(bevelA);
                    outPts.Add(bevelB);
                }
                continue;
            }

            if (join == JoinStyle.Miter)
            {
                outPts.Add(new Pt(ix, iy));
            }
            else if (join == JoinStyle.Bevel)
            {
                outPts.Add(bevelA);
                outPts.Add(bevelB);
            }
            else
            {
                AddRound(outPts, p, bevelA, bevelB, roundSegments, ccw);
            }
        }

        return outPts;
    }

    private static void AddRound(List<Pt> outPts, Pt center, Pt a, Pt b, int segments, bool ccw)
    {
        // Arc from vector (a-center) to (b-center) around center.
        var ax = a.X - center.X; var ay = a.Y - center.Y;
        var bx = b.X - center.X; var by = b.Y - center.Y;
        var angA = Math.Atan2(ay, ax);
        var angB = Math.Atan2(by, bx);

        var delta = angB - angA;
        if (ccw)
        {
            while (delta <= 0) delta += Math.PI * 2;
        }
        else
        {
            while (delta >= 0) delta -= Math.PI * 2;
        }

        var steps = Math.Max(3, segments);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var ang = angA + delta * t;
            var x = center.X + Math.Cos(ang) * Math.Sqrt(ax * ax + ay * ay);
            var y = center.Y + Math.Sin(ang) * Math.Sqrt(ax * ax + ay * ay);
            outPts.Add(new Pt(x, y));
        }
    }

    private static double Cross(double ax, double ay, double bx, double by) => (ax * by) - (ay * bx);
}

