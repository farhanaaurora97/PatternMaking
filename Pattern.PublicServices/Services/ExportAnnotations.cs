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

    internal static void AppendGrainSvg(StringBuilder sb, PieceDefinition p, double dx, double dy)
    {
        if (p.Grain is not { Count: >= 2 }) return;
        var g0 = p.Grain[0];
        var g1 = p.Grain[^1];
        var x1 = g0[0] + p.OffsetX + dx;
        var y1 = g0[1] + p.OffsetY + dy;
        var x2 = g1[0] + p.OffsetX + dx;
        var y2 = g1[1] + p.OffsetY + dy;
        sb.AppendLine($"    <line x1=\"{x1.ToString(CultureInfo.InvariantCulture)}\" y1=\"{y1.ToString(CultureInfo.InvariantCulture)}\" " +
                      $"x2=\"{x2.ToString(CultureInfo.InvariantCulture)}\" y2=\"{y2.ToString(CultureInfo.InvariantCulture)}\" " +
                      "stroke=\"#166534\" stroke-width=\"1.2\" stroke-dasharray=\"4 3\" opacity=\"0.9\"/>");
        // Arrow head at top (g0)
        AppendGrainArrowSvg(sb, x1, y1, x2, y2);
    }

    private static void AppendGrainArrowSvg(StringBuilder sb, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        dx /= len;
        dy /= len;
        var ah = 9.0;
        var bx = x1 + dx * ah;
        var by = y1 + dy * ah;
        var px = -dy * 4;
        var py = dx * 4;
        sb.AppendLine($"    <polygon points=\"{x1.ToString(CultureInfo.InvariantCulture)},{y1.ToString(CultureInfo.InvariantCulture)} " +
                      $"{(bx + px).ToString(CultureInfo.InvariantCulture)},{(by + py).ToString(CultureInfo.InvariantCulture)} " +
                      $"{(bx - px).ToString(CultureInfo.InvariantCulture)},{(by - py).ToString(CultureInfo.InvariantCulture)}\" " +
                      "fill=\"#166534\" opacity=\"0.85\"/>");
    }

    internal static void AppendNotchesSvg(StringBuilder sb, PieceDefinition p, double dx, double dy)
    {
        if (p.Notches is not { Count: > 0 }) return;
        var poly = p.Points;
        if (poly.Count < 3) return;

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

            var wx = lx + p.OffsetX + dx;
            var wy = ly + p.OffsetY + dy;
            var tipX = wx + inx * NotchDepth;
            var tipY = wy + iny * NotchDepth;
            var b0x = wx - tx * NotchHalfWidth;
            var b0y = wy - ty * NotchHalfWidth;
            var b1x = wx + tx * NotchHalfWidth;
            var b1y = wy + ty * NotchHalfWidth;

            sb.AppendLine($"    <polygon points=\"{b0x.ToString(CultureInfo.InvariantCulture)},{b0y.ToString(CultureInfo.InvariantCulture)} " +
                          $"{tipX.ToString(CultureInfo.InvariantCulture)},{tipY.ToString(CultureInfo.InvariantCulture)} " +
                          $"{b1x.ToString(CultureInfo.InvariantCulture)},{b1y.ToString(CultureInfo.InvariantCulture)}\" " +
                          "fill=\"#7f1d1d\" stroke=\"#450a0a\" stroke-width=\"0.6\" opacity=\"0.95\"/>");
        }
    }

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

    internal static void DrawGrainPdf(XGraphics gfx, PieceDefinition piece, double minX, double minY, double margin, double yLabelOffset)
    {
        if (piece.Grain is not { Count: >= 2 }) return;
        var pen = new XPen(XColor.FromArgb(22, 101, 52), 1.0)
        {
            DashStyle = XDashStyle.Dash,
        };
        var g0 = piece.Grain[0];
        var g1 = piece.Grain[^1];
        var x1 = (g0[0] + piece.OffsetX - minX) + margin;
        var y1 = (g0[1] + piece.OffsetY - minY) + margin + yLabelOffset;
        var x2 = (g1[0] + piece.OffsetX - minX) + margin;
        var y2 = (g1[1] + piece.OffsetY - minY) + margin + yLabelOffset;
        gfx.DrawLine(pen, x1, y1, x2, y2);
        DrawGrainArrowPdf(gfx, x1, y1, x2, y2);
    }

    /// <summary>Same arrowhead as SVG <see cref="AppendGrainArrowSvg"/> — at grain start, pointing along grain.</summary>
    private static void DrawGrainArrowPdf(XGraphics gfx, double x1, double y1, double x2, double y2)
    {
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
        var path = new XGraphicsPath();
        path.AddLines(new[]
        {
            new XPoint(x1, y1),
            new XPoint(bx + px, by + py),
            new XPoint(bx - px, by - py),
        });
        path.CloseFigure();
        var fill = new XSolidBrush(XColor.FromArgb(217, 22, 101, 52));
        gfx.DrawPath(new XPen(XColor.FromArgb(22, 101, 52), 0.5), fill, path);
    }

    internal static void DrawNotchesPdf(XGraphics gfx, PieceDefinition piece, double minX, double minY, double margin, double yLabelOffset)
    {
        if (piece.Notches is not { Count: > 0 }) return;
        var poly = piece.Points;
        if (poly.Count < 3) return;
        var pen = new XPen(XColor.FromArgb(69, 10, 10), 0.9);
        var fill = new XSolidBrush(XColor.FromArgb(210, 70, 70));

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

            var wx = (lx + piece.OffsetX - minX) + margin;
            var wy = (ly + piece.OffsetY - minY) + margin + yLabelOffset;
            var tipX = wx + inx * NotchDepth;
            var tipY = wy + iny * NotchDepth;
            var b0x = wx - tx * NotchHalfWidth;
            var b0y = wy - ty * NotchHalfWidth;
            var b1x = wx + tx * NotchHalfWidth;
            var b1y = wy + ty * NotchHalfWidth;

            var path = new XGraphicsPath();
            path.AddLines(new[]
            {
                new XPoint(b0x, b0y),
                new XPoint(tipX, tipY),
                new XPoint(b1x, b1y),
            });
            path.CloseFigure();
            gfx.DrawPath(pen, fill, path);
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
}
