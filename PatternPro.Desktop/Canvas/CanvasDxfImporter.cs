using System.Globalization;
using Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

/// <summary>DXF reader for closed piece outlines (PatternPro AAMA + common Optitex exports).</summary>
internal static class CanvasDxfImporter
{
    public static IReadOnlyList<PieceDefinition> Import(string dxfText, string defaultNamePrefix = "Imported")
    {
        if (string.IsNullOrWhiteSpace(dxfText))
            return [];

        if (dxfText.Length > 2 && dxfText[0] == 'A' && dxfText[1] == 'c')
            return []; // binary DXF

        var doc = DxfDocument.Parse(dxfText);
        var scale = doc.UnitsToCm * CanvasUnits.PixelsPerCm;
        var result = new List<PieceDefinition>();
        var pieceIndex = 0;

        if (doc.Inserts.Count > 0)
        {
            foreach (var insert in doc.Inserts)
            {
                if (!doc.BlockPolylines.TryGetValue(insert.BlockName, out var loops)) continue;
                foreach (var loop in loops)
                    TryAddPiece(result, ref pieceIndex, defaultNamePrefix, loop, insert.X, insert.Y, scale, insert.BlockName);
            }
        }
        else
        {
            foreach (var blockName in doc.BlockPolylines.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var loop in doc.BlockPolylines[blockName])
                    TryAddPiece(result, ref pieceIndex, defaultNamePrefix, loop, 0, 0, scale, blockName);
            }

            foreach (var loop in doc.EntityPolylines)
                TryAddPiece(result, ref pieceIndex, defaultNamePrefix, loop, 0, 0, scale, null);
        }

        return result;
    }

    private static void TryAddPiece(
        List<PieceDefinition> result,
        ref int pieceIndex,
        string prefix,
        IReadOnlyList<(double X, double Y)> loop,
        double offsetX,
        double offsetY,
        double scale,
        string? blockName)
    {
        if (loop.Count < 3) return;

        var world = loop
            .Select(p => ((p.X + offsetX) * scale, (p.Y + offsetY) * scale))
            .ToList();

        var minX = world.Min(p => p.Item1);
        var minY = world.Min(p => p.Item2);

        var name = !string.IsNullOrWhiteSpace(blockName)
            ? SanitizeName(blockName)
            : $"{prefix} {pieceIndex + 1}";

        result.Add(new PieceDefinition
        {
            Name = name,
            Cut = "Cut 2",
            Color = "#6366f1",
            Category = "Body Panels",
            Points = world.Select(p => new[]
            {
                (int)Math.Round(p.Item1 - minX),
                (int)Math.Round(p.Item2 - minY),
            }).ToList(),
            OffsetX = (int)Math.Round(minX),
            OffsetY = (int)Math.Round(minY),
        });
        pieceIndex++;
    }

    private static string SanitizeName(string blockName)
    {
        var name = blockName.Trim();
        var idx = name.LastIndexOf('_');
        if (idx > 0 && name[(idx + 1)..].Length <= 3)
            name = name[..idx].Replace('_', ' ').Trim();
        else
            name = name.Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(name) ? "Imported Piece" : name;
    }

    private sealed class DxfDocument
    {
        public double UnitsToCm { get; set; } = 1.0;
        public Dictionary<string, List<List<(double X, double Y)>>> BlockPolylines { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string BlockName, double X, double Y)> Inserts { get; } = [];
        public List<List<(double X, double Y)>> EntityPolylines { get; } = [];

        public static DxfDocument Parse(string text)
        {
            var doc = new DxfDocument();
            var pairs = ReadPairs(text);
            doc.UnitsToCm = ReadUnits(pairs);

            string? section = null;
            string? currentBlock = null;

            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 0 && pairs[i].Value.Equals("SECTION", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < pairs.Count && pairs[i + 1].Code == 2)
                {
                    section = pairs[i + 1].Value;
                    i++;
                    continue;
                }

                if (pairs[i].Code == 0 && pairs[i].Value.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
                {
                    section = null;
                    currentBlock = null;
                    continue;
                }

                if (section == "BLOCKS" && pairs[i].Code == 0 && pairs[i].Value.Equals("BLOCK", StringComparison.OrdinalIgnoreCase))
                {
                    currentBlock = ReadString(pairs, i + 1, 2);
                    if (!string.IsNullOrWhiteSpace(currentBlock) && !doc.BlockPolylines.ContainsKey(currentBlock))
                        doc.BlockPolylines[currentBlock] = [];
                    continue;
                }

                if (section == "BLOCKS" && pairs[i].Code == 0 && pairs[i].Value.Equals("ENDBLK", StringComparison.OrdinalIgnoreCase))
                {
                    currentBlock = null;
                    continue;
                }

                if (pairs[i].Code == 0 &&
                    (pairs[i].Value.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase) ||
                     pairs[i].Value.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase)))
                {
                    var isLw = pairs[i].Value.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase);
                    if (TryReadPolyline(pairs, i, isLw, out var loop, out var endIndex) && IsCutLayer(loop.Layer))
                    {
                        if (section == "BLOCKS" && !string.IsNullOrWhiteSpace(currentBlock))
                            doc.BlockPolylines[currentBlock].Add(loop.Points);
                        else if (section == "ENTITIES")
                            doc.EntityPolylines.Add(loop.Points);
                    }
                    i = endIndex;
                    continue;
                }

                if (section == "ENTITIES" && pairs[i].Code == 0 && pairs[i].Value.Equals("INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    var insert = ReadInsert(pairs, i + 1);
                    if (insert is not null)
                        doc.Inserts.Add(insert.Value);
                }
            }

            // Entity polylines only used when there are no INSERT references.
            return doc;
        }

        private static bool IsCutLayer(string? layer)
        {
            if (string.IsNullOrWhiteSpace(layer)) return true;
            var l = layer.Trim();
            return l is "1" or "CUT" or "Cut" or "cut" or "0" or "14";
        }

        private static (string BlockName, double X, double Y)? ReadInsert(IReadOnlyList<DxfPair> pairs, int start)
        {
            string? name = null;
            double x = 0, y = 0;
            for (var i = start; i < pairs.Count && pairs[i].Code != 0; i++)
            {
                if (pairs[i].Code == 2) name = pairs[i].Value;
                else if (pairs[i].Code == 10 && TryDouble(pairs[i].Value, out var vx)) x = vx;
                else if (pairs[i].Code == 20 && TryDouble(pairs[i].Value, out var vy)) y = vy;
            }
            return string.IsNullOrWhiteSpace(name) ? null : (name, x, y);
        }

        private sealed record PolylineRead(string? Layer, List<(double X, double Y)> Points);

        private static bool TryReadPolyline(
            IReadOnlyList<DxfPair> pairs, int start, bool lw, out PolylineRead result, out int endIndex)
        {
            result = new PolylineRead(null, []);
            endIndex = start;
            string? layer = null;
            var closed = lw;
            var verts = new List<(double X, double Y)>();
            double? px = null;

            for (var i = start + 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 0)
                {
                    var type = pairs[i].Value;
                    if (type.Equals("SEQEND", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("BLOCK", StringComparison.OrdinalIgnoreCase) ||
                        type.Equals("ENDBLK", StringComparison.OrdinalIgnoreCase))
                    {
                        endIndex = i - 1;
                        break;
                    }

                    if (!lw && type.Equals("VERTEX", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (pairs[i].Code == 8) layer = pairs[i].Value;
                else if (pairs[i].Code == 70 && int.TryParse(pairs[i].Value, out var flags))
                    closed = (flags & 1) == 1;
                else if (pairs[i].Code == 10 && TryDouble(pairs[i].Value, out var x))
                    px = x;
                else if (pairs[i].Code == 20 && px is double xVal && TryDouble(pairs[i].Value, out var y))
                {
                    verts.Add((xVal, y));
                    px = null;
                }
            }

            if (verts.Count >= 3 && (closed || Distance(verts[0], verts[^1]) < 0.05))
            {
                if (Distance(verts[0], verts[^1]) < 0.05)
                    verts.RemoveAt(verts.Count - 1);
                result = new PolylineRead(layer, verts);
                return true;
            }

            return false;
        }

        private static double ReadUnits(IReadOnlyList<DxfPair> pairs)
        {
            for (var i = 0; i < pairs.Count - 1; i++)
            {
                if (pairs[i].Code == 9 && pairs[i].Value.Equals("$INSUNITS", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(pairs[i + 1].Value, out var units))
                {
                    return units switch
                    {
                        4 => 0.1,   // mm
                        5 => 1.0,   // cm
                        6 => 100.0, // m
                        1 => 2.54,  // inches
                        _ => 1.0,
                    };
                }
            }
            return 1.0;
        }

        private static string? ReadString(IReadOnlyList<DxfPair> pairs, int start, int code)
        {
            for (var i = start; i < pairs.Count && pairs[i].Code != 0; i++)
                if (pairs[i].Code == code) return pairs[i].Value;
            return null;
        }

        private static List<DxfPair> ReadPairs(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var pairs = new List<DxfPair>(lines.Length / 2);
            for (var i = 0; i + 1 < lines.Length; i += 2)
            {
                if (!int.TryParse(lines[i].Trim(), out var code)) continue;
                pairs.Add(new DxfPair(code, lines[i + 1].Trim()));
            }
            return pairs;
        }
    }

    private readonly record struct DxfPair(int Code, string Value);

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static bool TryDouble(string s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
