using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class ExportService(
    IPatternDraftingService draftingService,
    IPieceService pieceService,
    IPatternService patternService) : IExportService
{
    public IReadOnlyList<string> GetExportSteps(string format) =>
    [
        "Collecting pattern pieces",
        "Applying seam allowances",
        $"Generating {format.ToUpper()} geometry",
        "Packaging all sizes (XS-XXL)",
        "Finalising output files",
    ];

    public (byte[] Bytes, string ContentType, string FileName) BuildExportPackage(
        string style,
        string format,
        IReadOnlyList<string> sizes,
        int patternId = 0)
    {
        var safeFormat = string.IsNullOrWhiteSpace(format) ? "DXF" : format.Trim().ToUpperInvariant();
        var pickedSizes = sizes.Count == 0 ? ["XS", "S", "M", "L", "XL", "XXL"] : sizes;
        var styleKey = NormalizeStyleKey(style);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            if (patternId > 0)
            {
                var canvasPieces = pieceService.GetPieceDefinitions(patternId, styleKey).ToList();
                var pattern      = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);
                var baseSize     = string.IsNullOrWhiteSpace(pattern?.BaseSize) ? "M" : pattern.BaseSize;
                AddZipReadmeForIllustrator(zip);
                AddCanvasGeometryExport(zip, canvasPieces, safeFormat, styleKey, patternId, baseSize, pickedSizes);
                AddTextEntry(zip, "manifest.txt",
                    $"Source: saved canvas geometry, graded to each size (pattern id {patternId})\n" +
                    $"Base size (edited master on canvas): {baseSize}\n" +
                    $"Style: {styleKey}\nFormat: {safeFormat}\nSizes: {string.Join(",", pickedSizes)}\n" +
                    "Grading: vertex deltas from the same auto-draft templates as Size chart / Block (per piece name).\n" +
                    "Open in Illustrator: extract ZIP, then open a per-size .svg or .dxf (e.g. canvas/skinny_M.svg).\n" +
                    "Coordinates are in canvas pixels (import scale as needed).\n" +
                    $"GeneratedUtc: {DateTime.UtcNow:O}\n");
            }
            else
            {
                var drafted = draftingService.DraftGradedSet(styleKey, pickedSizes);
                foreach (var (size, pieces) in drafted)
                {
                    switch (safeFormat)
                    {
                        case "DXF":
                            AddTextEntry(zip, $"{styleKey}_{size}.dxf", BuildCombinedDxf(pieces, size));
                            break;
                        case "SVG":
                            AddTextEntry(zip, $"{styleKey}_{size}.svg", BuildCombinedSvg(pieces, size));
                            break;
                        case "PDF":
                            foreach (var piece in pieces)
                                AddBinaryEntry(zip, $"{styleKey}/{size}/{piece.Name.Replace(' ', '_')}.pdf", BuildPdf(piece));
                            break;
                        default:
                            AddTextEntry(zip, $"{styleKey}_{size}.txt", $"Unsupported format '{safeFormat}'.");
                            break;
                    }
                }

                AddTextEntry(zip, "manifest.txt",
                    $"Source: drafted from size chart (not canvas edits)\nStyle: {styleKey}\nFormat: {safeFormat}\nSizes: {string.Join(",", pickedSizes)}\n" +
                    $"Illustrator: use .svg inside this ZIP (File > Open). DXF also supported.\nGeneratedUtc: {DateTime.UtcNow:O}\n");
            }
        }

        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Export ZIP generation produced zero bytes.");

        var fileStem = patternId > 0
            ? $"pattern-{patternId}-{styleKey}-{safeFormat.ToLowerInvariant()}-canvas"
            : $"pattern-export-{styleKey}-{safeFormat.ToLowerInvariant()}";
        return (bytes, "application/zip", $"{fileStem}.zip");
    }

    private static string NormalizeStyleKey(string style)
    {
        if (string.IsNullOrWhiteSpace(style)) return "skinny";
        var s = style.Trim();
        if (string.Equals(s, "wide leg", StringComparison.OrdinalIgnoreCase)) return "wideLeg";
        return s.ToLowerInvariant() switch
        {
            "skinny" => "skinny",
            "slim" => "slim",
            "straight" => "straight",
            "bootcut" => "bootcut",
            "wideleg" => "wideLeg",
            "wideLeg" => "wideLeg",
            _ => "skinny",
        };
    }

    private static void AddZipReadmeForIllustrator(ZipArchive zip)
    {
        const string readme =
            "HOW TO OPEN IN ADOBE ILLUSTRATOR\r\n" +
            "1) Extract this ZIP (Illustrator does not open .zip as artwork).\r\n" +
            "2) Double-click the .svg file, or in Illustrator: File > Open > choose the .svg\r\n" +
            "   (Best compatibility — use canvas/*_all_pieces.svg if present.)\r\n" +
            "3) For DXF: File > Open, pick the .dxf — set units if prompted (file uses canvas pixel units).\r\n" +
            "4) PDF: open individual .pdf files if exported.\r\n";
        AddTextEntry(zip, "README-Illustrator.txt", readme);
    }

    private void AddCanvasGeometryExport(
        ZipArchive zip,
        IReadOnlyList<PieceDefinition> pieces,
        string safeFormat,
        string styleKey,
        int patternId,
        string patternBaseSize,
        IReadOnlyList<string> sizes)
    {
        if (pieces.Count == 0)
        {
            AddTextEntry(zip, "canvas/empty.txt",
                "No pieces to export. Open the Canvas editor, edit or generate pieces, then Save All.\n");
            return;
        }

        foreach (var size in sizes)
        {
            var graded = draftingService.GradeCanvasPiecesForSize(pieces, styleKey, patternBaseSize, size);
            switch (safeFormat)
            {
                case "DXF":
                    AddTextEntry(zip, $"canvas/{styleKey}_{size}.dxf", BuildCombinedDxf(graded, size));
                    break;
                case "SVG":
                    AddTextEntry(zip, $"canvas/{styleKey}_{size}.svg", BuildCombinedSvg(graded, $"Pattern {patternId} {size}"));
                    break;
                case "PDF":
                    foreach (var piece in graded)
                        AddBinaryEntry(zip,
                            $"canvas/{size}/{SanitizeFileSegment(piece.Name)}.pdf",
                            BuildPdf(piece));
                    break;
                default:
                    AddTextEntry(zip, $"canvas/note-{size}.txt", $"Unsupported format '{safeFormat}'.");
                    break;
            }
        }
    }

    private static string SanitizeFileSegment(string name)
    {
        var s = Regex.Replace(name.Trim(), @"[^\w\-\.]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1));
        return string.IsNullOrEmpty(s) ? "piece" : s;
    }

    private static void AddTextEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        // UTF-8 without BOM — BOM breaks some SVG/XML imports (e.g. strict Illustrator / parsers).
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AddBinaryEntry(ZipArchive zip, string path, byte[] content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string BuildCombinedSvg(IReadOnlyList<PieceDefinition> pieces, string sizeName)
    {
        const double gap = 40;
        const double labelH = 18;
        const double margin = 20;

        var bboxes = pieces.Select(p =>
        {
            var xs = p.Points.Select(pt => pt[0] + p.OffsetX).ToArray();
            var ys = p.Points.Select(pt => pt[1] + p.OffsetY).ToArray();
            return (minX: xs.Min(), minY: ys.Min(), w: xs.Max() - xs.Min(), h: ys.Max() - ys.Min());
        }).ToList();

        var totalW = bboxes.Sum(b => b.w) + gap * (pieces.Count - 1) + margin * 2;
        var totalH = (bboxes.Count > 0 ? bboxes.Max(b => b.h) : 100) + labelH + margin * 2;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        // Minimal SVG 1.1 for Illustrator: no unused xlink; ASCII title; path > polygon for import quirks.
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" " +
                      $"width=\"{totalW:F1}\" height=\"{totalH:F1}\" " +
                      $"viewBox=\"0 0 {totalW:F1} {totalH:F1}\" preserveAspectRatio=\"xMidYMid meet\">");
        sb.AppendLine($"  <title>{Escape(AsciiTitle(sizeName))} - All Pieces</title>");

        double curX = margin;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            var b = bboxes[i];
            var dx = curX - b.minX;
            var dy = margin + labelH - b.minY;

            var labelX = curX + b.w / 2;
            var gid = SanitizeSvgId(p.Name, i);

            var pathD = string.Join(" ",
                p.Points.Select((pt, vi) =>
                {
                    var x = pt[0] + p.OffsetX + dx;
                    var y = pt[1] + p.OffsetY + dy;
                    var cmd = vi == 0 ? "M" : "L";
                    return $"{cmd}{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";
                })) + " Z";

            sb.AppendLine($"  <g id=\"{gid}\">");
            sb.AppendLine($"    <text x=\"{labelX:F1}\" y=\"{margin + labelH - 4:F1}\" text-anchor=\"middle\" " +
                          "font-family=\"Arial,Helvetica,sans-serif\" font-size=\"11\" fill=\"#333333\">" +
                          $"{Escape(p.Name)}</text>");
            sb.AppendLine($"    <path d=\"{pathD}\" fill=\"none\" stroke=\"#000000\" stroke-width=\"1\"/>");
            sb.AppendLine($"  </g>");

            curX += b.w + gap;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    /// <summary>Strip non-ASCII from SVG title to avoid strict-parser issues in some Illustrator versions.</summary>
    private static string AsciiTitle(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Export";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is >= ' ' and <= '~') sb.Append(c);
            else sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// AutoCAD R12-style ASCII DXF using only LINE entities on layer 0.
    /// Illustrator 2022+ often returns error [2067] on LWPOLYLINE/AC1015 DXF from third-party tools;
    /// R12 LINE format is the common workaround (Adobe community / EXDXF-Pro notes).
    /// </summary>
    private static string BuildCombinedDxf(IReadOnlyList<PieceDefinition> pieces, string _sizeName)
    {
        const double gap = 40;
        var nl = "\r\n";
        var sb = new StringBuilder();

        sb.Append($"0{nl}SECTION{nl}2{nl}HEADER{nl}9{nl}$ACADVER{nl}1{nl}AC1009{nl}0{nl}ENDSEC{nl}");
        sb.Append($"0{nl}SECTION{nl}2{nl}TABLES{nl}0{nl}ENDSEC{nl}");
        sb.Append($"0{nl}SECTION{nl}2{nl}ENTITIES{nl}");

        double curX = 0;
        foreach (var p in pieces)
        {
            var xs = p.Points.Select(pt => pt[0] + p.OffsetX).ToArray();
            var ys = p.Points.Select(pt => pt[1] + p.OffsetY).ToArray();
            var minX = xs.Min();
            var minY = ys.Min();
            var w = xs.Max() - minX;
            var dx = curX - minX;
            var dy = -minY;

            var n = p.Points.Count;
            if (n < 2) { curX += w + gap; continue; }

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var x1 = p.Points[i][0] + p.OffsetX + dx;
                var y1 = p.Points[i][1] + p.OffsetY + dy;
                var x2 = p.Points[j][0] + p.OffsetX + dx;
                var y2 = p.Points[j][1] + p.OffsetY + dy;
                sb.Append($"0{nl}LINE{nl}8{nl}0{nl}");
                sb.Append($"10{nl}{x1.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"20{nl}{y1.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"11{nl}{x2.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"21{nl}{y2.ToString(CultureInfo.InvariantCulture)}{nl}");
            }

            curX += w + gap;
        }

        sb.Append($"0{nl}ENDSEC{nl}0{nl}EOF{nl}");
        return sb.ToString();
    }

    private static byte[] BuildPdf(PieceDefinition piece)
    {
        var minX = piece.Points.Min(p => p[0] + piece.OffsetX);
        var minY = piece.Points.Min(p => p[1] + piece.OffsetY);
        var maxX = piece.Points.Max(p => p[0] + piece.OffsetX);
        var maxY = piece.Points.Max(p => p[1] + piece.OffsetY);

        const double margin = 24d;
        var width = Math.Max(200d, (maxX - minX) + margin * 2);
        var height = Math.Max(200d, (maxY - minY) + margin * 2 + 24d);

        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = width;
        page.Height = height;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var points = piece.Points
                .Select(p => new XPoint(
                    (p[0] + piece.OffsetX - minX) + margin,
                    (p[1] + piece.OffsetY - minY) + margin + 20))
                .ToArray();

            if (points.Length >= 2)
            {
                var path = new XGraphicsPath();
                path.AddLines(points);
                path.CloseFigure();
                gfx.DrawPath(new XPen(XColors.Black, 1.2), XBrushes.Transparent, path);
            }

            var font = new XFont("Arial", 10, XFontStyle.Regular);
            gfx.DrawString(piece.Name, font, XBrushes.Black, new XPoint(margin, margin));
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string SanitizeSvgId(string name, int index)
    {
        var raw = Regex.Replace(name.Trim(), @"[^\w\-\.]", "_", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (string.IsNullOrEmpty(raw)) raw = "piece";
        if (char.IsDigit(raw[0])) raw = "p_" + raw;
        return $"{raw}_{index}";
    }
}