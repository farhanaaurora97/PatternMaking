using System.Globalization;
using System.Text;
using PdfSharpCore.Drawing;
using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>Grain lines + triangular notch marks on dedicated export layers.</summary>
internal static class ExportAnnotations
{
    internal const double NotchDepth = 9.0;
    internal const double NotchHalfWidth = 6.0;

    internal static void AppendGrainDxf(StringBuilder sb, PieceDefinition p, double dx, double dy, double scale = 1.0)
    {
        if (p.Grain is not { Count: >= 2 }) return;
        var nl = "\r\n";
        var g0 = p.Grain[0];
        var g1 = p.Grain[^1];
        var x1 = (g0[0] + p.OffsetX + dx) * scale;
        var y1 = (g0[1] + p.OffsetY + dy) * scale;
        var x2 = (g1[0] + p.OffsetX + dx) * scale;
        var y2 = (g1[1] + p.OffsetY + dy) * scale;
        sb.Append($"0{nl}LINE{nl}8{nl}GRAIN{nl}");
        sb.Append($"10{nl}{x1.ToString(CultureInfo.InvariantCulture)}{nl}");
        sb.Append($"20{nl}{y1.ToString(CultureInfo.InvariantCulture)}{nl}");
        sb.Append($"11{nl}{x2.ToString(CultureInfo.InvariantCulture)}{nl}");
        sb.Append($"21{nl}{y2.ToString(CultureInfo.InvariantCulture)}{nl}");
    }

    internal static void AppendNotchesDxf(StringBuilder sb, PieceDefinition p, double dx, double dy, double scale = 1.0)
    {
        if (p.Notches is not { Count: > 0 }) return;
        var poly = p.Points;
        if (poly.Count < 3) return;
        var nl = "\r\n";
        var notchDepth = NotchDepth * scale;
        var notchHalf = NotchHalfWidth * scale;

        foreach (var nv in p.Notches)
        {
            var lx = nv[0];
            var ly = nv[1];
            var edgeIdx = FindNearestEdgeIndex(poly, lx, ly, out var tEdge);
            var (inx, iny) = NotchGrainResolver.InwardNormal(poly, edgeIdx, tEdge);
            var i = edgeIdx;
            var j = (edgeIdx + 1) % poly.Count;
            var ex = poly[j][0] - poly[i][0];
            var ey = poly[j][1] - poly[i][1];
            var el = Math.Sqrt(ex * ex + ey * ey);
            if (el < 1e-6) continue;
            var tx = ex / el;
            var ty = ey / el;

            var wx = (lx + p.OffsetX + dx) * scale;
            var wy = (ly + p.OffsetY + dy) * scale;
            var tipX = wx + inx * notchDepth;
            var tipY = wy + iny * notchDepth;
            var b0x = wx - tx * notchHalf;
            var b0y = wy - ty * notchHalf;
            var b1x = wx + tx * notchHalf;
            var b1y = wy + ty * notchHalf;

            void Line(double xa, double ya, double xb, double yb)
            {
                sb.Append($"0{nl}LINE{nl}8{nl}NOTCH{nl}");
                sb.Append($"10{nl}{xa.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"20{nl}{ya.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"11{nl}{xb.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"21{nl}{yb.ToString(CultureInfo.InvariantCulture)}{nl}");
            }

            Line(b0x, b0y, tipX, tipY);
            Line(tipX, tipY, b1x, b1y);
            Line(b1x, b1y, b0x, b0y);
        }
    }

    internal static void AppendGrainHpgl(StringBuilder sb, PieceDefinition p, double dx, double dy, double scale)
    {
        if (p.Grain is not { Count: >= 2 }) return;
        var g0 = p.Grain[0];
        var g1 = p.Grain[^1];
        var x1 = (g0[0] + p.OffsetX + dx) * scale;
        var y1 = (g0[1] + p.OffsetY + dy) * scale;
        var x2 = (g1[0] + p.OffsetX + dx) * scale;
        var y2 = (g1[1] + p.OffsetY + dy) * scale;
        sb.Append("SP3;");
        HpglHelpers.Line(sb, x1, y1, x2, y2);
    }

    internal static void AppendNotchesHpgl(StringBuilder sb, PieceDefinition p, double dx, double dy, double scale)
    {
        if (p.Notches is not { Count: > 0 }) return;
        var poly = p.Points;
        if (poly.Count < 3) return;
        var notchDepth = NotchDepth * scale;
        var notchHalf = NotchHalfWidth * scale;

        sb.Append("SP4;");
        foreach (var nv in p.Notches)
        {
            var lx = nv[0];
            var ly = nv[1];
            var edgeIdx = FindNearestEdgeIndex(poly, lx, ly, out var tEdge);
            var (inx, iny) = NotchGrainResolver.InwardNormal(poly, edgeIdx, tEdge);
            var i = edgeIdx;
            var j = (edgeIdx + 1) % poly.Count;
            var ex = poly[j][0] - poly[i][0];
            var ey = poly[j][1] - poly[i][1];
            var el = Math.Sqrt(ex * ex + ey * ey);
            if (el < 1e-6) continue;
            var tx = ex / el;
            var ty = ey / el;

            var wx = (lx + p.OffsetX + dx) * scale;
            var wy = (ly + p.OffsetY + dy) * scale;
            var tipX = wx + inx * notchDepth;
            var tipY = wy + iny * notchDepth;
            var b0x = wx - tx * notchHalf;
            var b0y = wy - ty * notchHalf;
            var b1x = wx + tx * notchHalf;
            var b1y = wy + ty * notchHalf;

            HpglHelpers.Line(sb, b0x, b0y, tipX, tipY);
            HpglHelpers.Line(sb, tipX, tipY, b1x, b1y);
            HpglHelpers.Line(sb, b1x, b1y, b0x, b0y);
        }
    }

    private static int FindNearestEdgeIndex(IReadOnlyList<int[]> poly, int lx, int ly, out double tEdge)
    {
        tEdge = 0;
        var best = double.MaxValue;
        var bestIdx = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            var ax = poly[i][0];
            var ay = poly[i][1];
            var bx = poly[j][0];
            var by = poly[j][1];
            var (t, d) = ClosestOnSegment(lx, ly, ax, ay, bx, by);
            if (d < best)
            {
                best = d;
                bestIdx = i;
                tEdge = t;
            }
        }

        return bestIdx;
    }

    private static (double t, double dist) ClosestOnSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-18) return (0, Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay)));
        var t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / len2, 0.0, 1.0);
        var qx = ax + dx * t;
        var qy = ay + dy * t;
        var dist = Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
        return (t, dist);
    }

    internal static void DrawGrainPdf(
        XGraphics gfx, PieceDefinition piece,
        double minX, double minY, double mmScale, double marginMm, double labelMm, double ptPerMm)
    {
        if (piece.Grain is not { Count: >= 2 }) return;
        double Px(double x) => ((x + piece.OffsetX - minX) * mmScale + marginMm) * ptPerMm;
        double Py(double y) => ((y + piece.OffsetY - minY) * mmScale + marginMm + labelMm) * ptPerMm;

        var pen = new XPen(XColor.FromArgb(22, 101, 52), 1.0) { DashStyle = XDashStyle.Dash };
        var g0 = piece.Grain[0];
        var g1 = piece.Grain[^1];
        var x1 = Px(g0[0]);
        var y1 = Py(g0[1]);
        var x2 = Px(g1[0]);
        var y2 = Py(g1[1]);
        gfx.DrawLine(pen, x1, y1, x2, y2);

        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        dx /= len;
        dy /= len;
        const double ah = 9.0;
        var bx = x1 + dx * ah;
        var by = y1 + dy * ah;
        var px = -dy * 4;
        var py = dx * 4;
        var arrow = new XGraphicsPath();
        arrow.AddLines(new[] { new XPoint(x1, y1), new XPoint(bx + px, by + py), new XPoint(bx - px, by - py) });
        arrow.CloseFigure();
        gfx.DrawPath(new XPen(XColor.FromArgb(22, 101, 52), 0.5), new XSolidBrush(XColor.FromArgb(217, 22, 101, 52)), arrow);
    }

    internal static void DrawNotchesPdf(
        XGraphics gfx, PieceDefinition piece,
        double minX, double minY, double mmScale, double marginMm, double labelMm, double ptPerMm)
    {
        if (piece.Notches is not { Count: > 0 }) return;
        var poly = piece.Points;
        if (poly.Count < 3) return;

        double Px(double x) => ((x + piece.OffsetX - minX) * mmScale + marginMm) * ptPerMm;
        double Py(double y) => ((y + piece.OffsetY - minY) * mmScale + marginMm + labelMm) * ptPerMm;

        var pen = new XPen(XColor.FromArgb(69, 10, 10), 0.9);
        var fill = new XSolidBrush(XColor.FromArgb(210, 70, 70));
        var notchDepth = NotchDepth * mmScale * ptPerMm;
        var notchHalf = NotchHalfWidth * mmScale * ptPerMm;

        foreach (var nv in piece.Notches)
        {
            var lx = nv[0];
            var ly = nv[1];
            var edgeIdx = FindNearestEdgeIndex(poly, lx, ly, out var tEdge);
            var (inx, iny) = NotchGrainResolver.InwardNormal(poly, edgeIdx, tEdge);
            var i = edgeIdx;
            var j = (edgeIdx + 1) % poly.Count;
            var ex = poly[j][0] - poly[i][0];
            var ey = poly[j][1] - poly[i][1];
            var el = Math.Sqrt(ex * ex + ey * ey);
            if (el < 1e-6) continue;
            var tx = ex / el;
            var ty = ey / el;

            var wx = Px(lx);
            var wy = Py(ly);
            var tipX = wx + inx * notchDepth;
            var tipY = wy + iny * notchDepth;
            var b0x = wx - tx * notchHalf;
            var b0y = wy - ty * notchHalf;
            var b1x = wx + tx * notchHalf;
            var b1y = wy + ty * notchHalf;

            var path = new XGraphicsPath();
            path.AddLines(new[] { new XPoint(b0x, b0y), new XPoint(tipX, tipY), new XPoint(b1x, b1y) });
            path.CloseFigure();
            gfx.DrawPath(pen, fill, path);
        }
    }
}

internal static class HpglHelpers
{
    /// <summary>Standard HPGL: 1016 plotter units per inch (40 units/mm).</summary>
    internal const double UnitsPerMm = 1016.0 / 25.4;

    internal static double CanvasToPlotterScale =>
        (10.0 / SeamGeometry.PixelsPerCm) * UnitsPerMm;

    internal static string Pair(double x, double y) =>
        $"{Math.Round(x).ToString(CultureInfo.InvariantCulture)},{Math.Round(y).ToString(CultureInfo.InvariantCulture)}";

    internal static void Line(StringBuilder sb, double x1, double y1, double x2, double y2)
    {
        sb.Append("PU").Append(Pair(x1, y1)).Append(';');
        sb.Append("PD").Append(Pair(x2, y2)).Append(';');
    }

    internal static void ClosedPolygon(StringBuilder sb, IReadOnlyList<(double x, double y)> pts)
    {
        if (pts.Count < 2) return;
        sb.Append("PU").Append(Pair(pts[0].x, pts[0].y)).Append(';');
        sb.Append("PD");
        for (var i = 1; i < pts.Count; i++)
            sb.Append(Pair(pts[i].x, pts[i].y)).Append(',');
        sb.Append(Pair(pts[0].x, pts[0].y)).Append(';');
    }
}
