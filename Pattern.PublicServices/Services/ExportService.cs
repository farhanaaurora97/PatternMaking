using System.Globalization;
using System.IO.Compression;
using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class ExportService(IPatternDraftingService draftingService) : IExportService
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
        IReadOnlyList<string> sizes)
    {
        var safeFormat = string.IsNullOrWhiteSpace(format) ? "DXF" : format.Trim().ToUpperInvariant();
        var pickedSizes = sizes.Count == 0 ? ["XS", "S", "M", "L", "XL", "XXL"] : sizes;
        var drafted = draftingService.DraftGradedSet(style, pickedSizes);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (size, pieces) in drafted)
            {
                switch (safeFormat)
                {
                    case "DXF":
                        AddTextEntry(zip, $"{style}_{size}.dxf", BuildCombinedDxf(pieces, size));
                        break;
                    case "SVG":
                        AddTextEntry(zip, $"{style}_{size}.svg", BuildCombinedSvg(pieces, size));
                        break;
                    case "PDF":
                        foreach (var piece in pieces)
                            AddBinaryEntry(zip, $"{style}/{size}/{piece.Name.Replace(' ', '_')}.pdf", BuildPdf(piece));
                        break;
                    default:
                        AddTextEntry(zip, $"{style}_{size}.txt", $"Unsupported format '{safeFormat}'.");
                        break;
                }
            }

            AddTextEntry(zip, "manifest.txt",
                $"Style: {style}\nFormat: {safeFormat}\nSizes: {string.Join(",", pickedSizes)}\nGeneratedUtc: {DateTime.UtcNow:O}\n");
        }

        return (ms.ToArray(), "application/zip", $"pattern-export-{style}-{safeFormat.ToLowerInvariant()}.zip");
    }

    private static void AddTextEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
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
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalW:F1} {totalH:F1}\">");
        sb.AppendLine($"  <title>{Escape(sizeName)} — All Pieces</title>");

        double curX = margin;
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            var b = bboxes[i];
            var dx = curX - b.minX;
            var dy = margin + labelH - b.minY;

            var pointsAttr = string.Join(" ",
                p.Points.Select(pt => $"{pt[0] + p.OffsetX + dx:F1},{pt[1] + p.OffsetY + dy:F1}"));
            var labelX = curX + b.w / 2;

            sb.AppendLine($"  <text x=\"{labelX:F1}\" y=\"{margin + labelH - 4:F1}\" text-anchor=\"middle\" " +
                          $"font-family=\"Arial,sans-serif\" font-size=\"11\" fill=\"#333\">{Escape(p.Name)}</text>");
            sb.AppendLine($"  <polygon points=\"{pointsAttr}\" fill=\"none\" stroke=\"black\" stroke-width=\"1.2\"/>");

            curX += b.w + gap;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string BuildCombinedDxf(IReadOnlyList<PieceDefinition> pieces, string sizeName)
    {
        const double gap = 40;
        var sb = new StringBuilder();

        // ── HEADER (AC1015 = AutoCAD 2000, widely supported) ──────────────
        sb.AppendLine("0");  sb.AppendLine("SECTION");
        sb.AppendLine("2");  sb.AppendLine("HEADER");
        sb.AppendLine("9");  sb.AppendLine("$ACADVER");
        sb.AppendLine("1");  sb.AppendLine("AC1015");
        sb.AppendLine("9");  sb.AppendLine("$INSUNITS");
        sb.AppendLine("70"); sb.AppendLine("0");
        sb.AppendLine("0");  sb.AppendLine("ENDSEC");

        // ── TABLES — declare one layer per piece ──────────────────────────
        sb.AppendLine("0");  sb.AppendLine("SECTION");
        sb.AppendLine("2");  sb.AppendLine("TABLES");
        sb.AppendLine("0");  sb.AppendLine("TABLE");
        sb.AppendLine("2");  sb.AppendLine("LAYER");
        sb.AppendLine("70"); sb.AppendLine(pieces.Count.ToString());

        foreach (var p in pieces)
        {
            var layer = p.Name.Replace(' ', '_');
            sb.AppendLine("0");  sb.AppendLine("LAYER");
            sb.AppendLine("2");  sb.AppendLine(layer);
            sb.AppendLine("70"); sb.AppendLine("0");   // layer on, not frozen
            sb.AppendLine("62"); sb.AppendLine("7");   // colour white/black
            sb.AppendLine("6");  sb.AppendLine("Continuous");
        }

        sb.AppendLine("0"); sb.AppendLine("ENDTAB");
        sb.AppendLine("0"); sb.AppendLine("ENDSEC");

        // ── ENTITIES — one LWPOLYLINE + TEXT per piece ────────────────────
        sb.AppendLine("0"); sb.AppendLine("SECTION");
        sb.AppendLine("2"); sb.AppendLine("ENTITIES");

        double curX = 0;
        foreach (var p in pieces)
        {
            var xs    = p.Points.Select(pt => pt[0] + p.OffsetX).ToArray();
            var ys    = p.Points.Select(pt => pt[1] + p.OffsetY).ToArray();
            var minX  = xs.Min();
            var minY  = ys.Min();
            var w     = xs.Max() - minX;
            var dx    = curX - minX;
            var dy    = -minY;            // normalise so every piece starts at Y = 0
            var layer = p.Name.Replace(' ', '_');

            // LWPOLYLINE — compact, no VERTEX/SEQEND needed
            sb.AppendLine("0");  sb.AppendLine("LWPOLYLINE");
            sb.AppendLine("8");  sb.AppendLine(layer);
            sb.AppendLine("90"); sb.AppendLine(p.Points.Count.ToString()); // vertex count
            sb.AppendLine("70"); sb.AppendLine("1");                        // 1 = closed

            foreach (var pt in p.Points)
            {
                sb.AppendLine("10"); sb.AppendLine((pt[0] + p.OffsetX + dx).ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("20"); sb.AppendLine((pt[1] + p.OffsetY + dy).ToString(CultureInfo.InvariantCulture));
            }

            // TEXT label above the piece
            var labelX = curX + w / 2;
            sb.AppendLine("0");  sb.AppendLine("TEXT");
            sb.AppendLine("8");  sb.AppendLine(layer);
            sb.AppendLine("10"); sb.AppendLine(labelX.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("20"); sb.AppendLine((-15).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("30"); sb.AppendLine("0.0");
            sb.AppendLine("40"); sb.AppendLine("8");   // text height
            sb.AppendLine("1");  sb.AppendLine(p.Name);
            sb.AppendLine("72"); sb.AppendLine("1");   // centre-justified

            curX += w + gap;
        }

        sb.AppendLine("0"); sb.AppendLine("ENDSEC");
        sb.AppendLine("0"); sb.AppendLine("EOF");
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
}