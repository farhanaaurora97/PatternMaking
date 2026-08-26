using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>
/// AAMA-style DXF for Optitex / Gerber / Lectra import.
/// Layer 1 = cut boundary (closed polyline), 14 = net/stitch line, 7 = grain, 4 = notch points.
/// Each piece is a named BLOCK referenced by INSERT in model space.
/// </summary>
internal static class AamaDxfExporter
{
    internal const string LayerCut = "1";
    internal const string LayerNotch = "4";
    internal const string LayerGrain = "7";
    internal const string LayerNet = "14";
    internal const string LayerText = "15";

    /// <summary>Canvas pixels to export units. 3 px = 1 cm (Optitex-friendly).</summary>
    private const double PieceGapCm = 4.0;
    private static readonly double CmScale = 1.0 / SeamGeometry.PixelsPerCm;
    private static readonly string Nl = "\r\n";

    internal static string Build(IReadOnlyList<PieceDefinition> pieces, string sizeCode)
    {
        var size = string.IsNullOrWhiteSpace(sizeCode) ? "M" : sizeCode.Trim();
        var sb = new StringBuilder();

        AppendHeader(sb);
        AppendLayerTable(sb);
        AppendBlocks(sb, pieces, size);
        AppendInserts(sb, pieces, size);

        sb.Append($"0{Nl}EOF{Nl}");
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.Append($"0{Nl}SECTION{Nl}2{Nl}HEADER{Nl}");
        sb.Append($"9{Nl}$ACADVER{Nl}1{Nl}AC1009{Nl}");
        sb.Append($"9{Nl}$INSUNITS{Nl}70{Nl}5{Nl}");
        sb.Append($"9{Nl}$MEASUREMENT{Nl}70{Nl}1{Nl}");
        sb.Append($"0{Nl}ENDSEC{Nl}");
    }

    private static void AppendLayerTable(StringBuilder sb)
    {
        sb.Append($"0{Nl}SECTION{Nl}2{Nl}TABLES{Nl}");
        sb.Append($"0{Nl}TABLE{Nl}2{Nl}LAYER{Nl}70{Nl}6{Nl}");

        AppendLayerRow(sb, "0", 7);
        AppendLayerRow(sb, LayerCut, 1);
        AppendLayerRow(sb, LayerNotch, 3);
        AppendLayerRow(sb, LayerGrain, 3);
        AppendLayerRow(sb, LayerNet, 5);
        AppendLayerRow(sb, LayerText, 7);

        sb.Append($"0{Nl}ENDTAB{Nl}0{Nl}ENDSEC{Nl}");
    }

    private static void AppendLayerRow(StringBuilder sb, string name, int color)
    {
        sb.Append($"0{Nl}LAYER{Nl}2{Nl}{name}{Nl}70{Nl}0{Nl}62{Nl}{color}{Nl}6{Nl}CONTINUOUS{Nl}");
    }

    private static void AppendBlocks(StringBuilder sb, IReadOnlyList<PieceDefinition> pieces, string size)
    {
        sb.Append($"0{Nl}SECTION{Nl}2{Nl}BLOCKS{Nl}");

        foreach (var piece in pieces)
        {
            if (piece.Points.Count < 3)
                continue;

            var blockName = BlockName(piece.Name, size);
            sb.Append($"0{Nl}BLOCK{Nl}2{Nl}{blockName}{Nl}70{Nl}0{Nl}");
            sb.Append($"10{Nl}0{Nl}20{Nl}0{Nl}30{Nl}0{Nl}3{Nl}{blockName}{Nl}1{Nl}0{Nl}0{Nl}0{Nl}");

            AppendPieceEntities(sb, piece, size, localOrigin: true);

            sb.Append($"0{Nl}ENDBLK{Nl}");
        }

        sb.Append($"0{Nl}ENDSEC{Nl}");
    }

    private static void AppendInserts(StringBuilder sb, IReadOnlyList<PieceDefinition> pieces, string size)
    {
        sb.Append($"0{Nl}SECTION{Nl}2{Nl}ENTITIES{Nl}");

        double curX = 0;
        foreach (var piece in pieces)
        {
            if (piece.Points.Count < 3)
                continue;

            var xs = piece.Points.Select(pt => (pt[0] + piece.OffsetX) * CmScale).ToArray();
            var width = xs.Max() - xs.Min();
            var blockName = BlockName(piece.Name, size);

            sb.Append($"0{Nl}INSERT{Nl}2{Nl}{blockName}{Nl}8{Nl}0{Nl}");
            sb.Append($"10{Nl}{F(curX)}{Nl}20{Nl}0{Nl}30{Nl}0{Nl}");

            curX += width + PieceGapCm;
        }

        sb.Append($"0{Nl}ENDSEC{Nl}");
    }

    private static void AppendPieceEntities(StringBuilder sb, PieceDefinition piece, string size, bool localOrigin)
    {
        var xs = piece.Points.Select(pt => pt[0] + piece.OffsetX).ToArray();
        var ys = piece.Points.Select(pt => pt[1] + piece.OffsetY).ToArray();
        var minX = xs.Min();
        var minY = ys.Min();

        double Tx(double canvasX) => (canvasX - (localOrigin ? minX : 0)) * CmScale;
        double Ty(double canvasY) => (canvasY - (localOrigin ? minY : 0)) * CmScale;

        var netPts = PieceOutlineTessellator.HasCurves(piece)
            ? PieceOutlineTessellator.TessellateOutline(piece)
                .Select(pt => (Tx(pt.X + piece.OffsetX), Ty(pt.Y + piece.OffsetY)))
                .ToList()
            : piece.Points
                .Select(pt => (Tx(pt[0] + piece.OffsetX), Ty(pt[1] + piece.OffsetY)))
                .ToList();

        IReadOnlyList<(double X, double Y)> cutPts;
        if (PieceSeamAllowanceHelper.EffectiveSeamAllowance(piece) > 0.0001)
        {
            var basePts = PieceOutlineTessellator.HasCurves(piece)
                ? PieceOutlineTessellator.TessellateOutline(piece)
                    .Select(pt => new SeamAllowanceOffset.Pt(pt.X + piece.OffsetX, pt.Y + piece.OffsetY))
                    .ToList()
                : piece.Points
                    .Select(pt => new SeamAllowanceOffset.Pt(pt[0] + piece.OffsetX, pt[1] + piece.OffsetY))
                    .ToList();
            var saPts = SeamAllowanceOffset.OffsetClosed(
                basePts,
                piece.SeamAllowance,
                SeamAllowanceOffset.ParseJoin(piece.SeamAllowanceJoin),
                PieceSeamAllowanceHelper.BuildEdgeOffsets(piece));

            cutPts = saPts.Count >= 3
                ? saPts.Select(pt => (Tx(pt.X), Ty(pt.Y))).ToList()
                : netPts;
        }
        else
        {
            cutPts = netPts;
        }

        AppendClosedPolyline(sb, LayerCut, cutPts);

        if (piece.SeamAllowance > 0.0001 && netPts.Count >= 3)
            AppendClosedPolyline(sb, LayerNet, netPts);

        if (piece.Grain is { Count: >= 2 })
        {
            var g0 = piece.Grain[0];
            var g1 = piece.Grain[^1];
            AppendLine(sb, LayerGrain,
                Tx(g0[0] + piece.OffsetX), Ty(g0[1] + piece.OffsetY),
                Tx(g1[0] + piece.OffsetX), Ty(g1[1] + piece.OffsetY));
        }

        if (piece.Notches is { Count: > 0 })
        {
            foreach (var nv in piece.Notches)
                AppendPoint(sb, LayerNotch, Tx(nv[0] + piece.OffsetX), Ty(nv[1] + piece.OffsetY));
        }

        var labelY = netPts.Min(p => p.Item2) - 0.8;
        var labelX = netPts.Average(p => p.Item1);
        AppendText(sb, LayerText, labelX, labelY, $"{piece.Name} ({size})");
    }

    private static void AppendClosedPolyline(StringBuilder sb, string layer, IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3)
            return;

        sb.Append($"0{Nl}POLYLINE{Nl}8{Nl}{layer}{Nl}66{Nl}1{Nl}70{Nl}1{Nl}");
        foreach (var (x, y) in points)
        {
            sb.Append($"0{Nl}VERTEX{Nl}8{Nl}{layer}{Nl}");
            sb.Append($"10{Nl}{F(x)}{Nl}20{Nl}{F(y)}{Nl}");
        }

        sb.Append($"0{Nl}SEQEND{Nl}8{Nl}{layer}{Nl}");
    }

    private static void AppendLine(StringBuilder sb, string layer, double x1, double y1, double x2, double y2)
    {
        sb.Append($"0{Nl}LINE{Nl}8{Nl}{layer}{Nl}");
        sb.Append($"10{Nl}{F(x1)}{Nl}20{Nl}{F(y1)}{Nl}");
        sb.Append($"11{Nl}{F(x2)}{Nl}21{Nl}{F(y2)}{Nl}");
    }

    private static void AppendPoint(StringBuilder sb, string layer, double x, double y)
    {
        sb.Append($"0{Nl}POINT{Nl}8{Nl}{layer}{Nl}");
        sb.Append($"10{Nl}{F(x)}{Nl}20{Nl}{F(y)}{Nl}");
    }

    private static void AppendText(StringBuilder sb, string layer, double x, double y, string text)
    {
        var safe = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (safe.Length == 0)
            return;

        sb.Append($"0{Nl}TEXT{Nl}8{Nl}{layer}{Nl}");
        sb.Append($"10{Nl}{F(x)}{Nl}20{Nl}{F(y)}{Nl}40{Nl}3{Nl}1{Nl}{safe}{Nl}");
    }

    internal static string BlockName(string pieceName, string size)
    {
        var stem = Regex.Replace(pieceName.Trim(), @"[^\w]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim('_');
        if (string.IsNullOrEmpty(stem))
            stem = "PIECE";
        var sz = Regex.Replace(size.Trim(), @"[^\w]+", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (string.IsNullOrEmpty(sz))
            sz = "M";
        var name = $"{stem}_{sz}";
        return name.Length <= 31 ? name : name[..31];
    }

    private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
