using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class ExportService(
    IPatternDraftingService draftingService,
    IPieceService pieceService,
    IPatternService patternService,
    IProductionCertificationService productionCertification) : IExportService
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "DXF", "HPGL", "PLT", "PDF",
    };

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
        int patternId = 0,
        ExportPurpose purpose = ExportPurpose.Factory)
    {
        var safeFormat = NormalizeFormat(format);
        var styleKey = NormalizeStyleKey(style);

        if (purpose == ExportPurpose.Factory)
        {
            var report = productionCertification.ValidateForFactory(patternId, styleKey);
            if (!report.CanExportToFactory)
            {
                var blockers = report.Issues.Select(i => i.Message).ToList();
                throw new InvalidOperationException(
                    "Factory export blocked: " + string.Join(" ", blockers));
            }
        }

        var patternForSizes = patternId > 0
            ? patternService.GetAll().FirstOrDefault(p => p.Id == patternId)
            : null;
        var pickedSizes = purpose == ExportPurpose.CloReview && patternForSizes is not null
            ? [string.IsNullOrWhiteSpace(patternForSizes.BaseSize) ? "M" : patternForSizes.BaseSize]
            : sizes.Count == 0 ? ["XS", "S", "M", "L", "XL", "XXL"] : sizes;

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            if (patternId > 0)
            {
                var canvasPieces = pieceService.GetPieceDefinitions(patternId, styleKey).ToList();
                var pattern      = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);
                var baseSize     = string.IsNullOrWhiteSpace(pattern?.BaseSize) ? "M" : pattern.BaseSize;
                if (purpose == ExportPurpose.CloReview)
                {
                    AddCloReadme(zip);
                    AddCanvasGeometryExport(zip, canvasPieces, safeFormat, styleKey, patternId, baseSize, pickedSizes);
                }
                else
                {
                    if (string.Equals(safeFormat, "PDF", StringComparison.OrdinalIgnoreCase))
                        AddZipReadmeForPrint(zip);
                    else
                        AddZipReadmeForPlotter(zip);
                    AddCanvasGeometryExport(zip, canvasPieces, safeFormat, styleKey, patternId, baseSize, pickedSizes);
                }

                AddTextEntry(zip, "manifest.txt", BuildCanvasManifest(patternId, styleKey, safeFormat, pickedSizes, baseSize, pattern, purpose));

                if (purpose == ExportPurpose.Factory && pattern is not null)
                {
                    var certReport = productionCertification.ValidateForFactory(patternId, styleKey);
                    AddTextEntry(zip, "certification.json", BuildCertificationJson(pattern, certReport));
                }
            }
            else
            {
                var drafted = draftingService.DraftGradedSet(styleKey, pickedSizes);
                foreach (var (size, pieces) in drafted)
                {
                    var pieceList = pieces.ToList();
                    NotchGrainResolver.ApplyAutomation(pieceList, styleKey);
                    if (safeFormat == "PDF")
                    {
                        foreach (var piece in pieceList)
                            AddBinaryEntry(zip,
                                $"{styleKey}/{size}/{SanitizeFileSegment(piece.Name)}.pdf",
                                BuildPdf(piece, size));
                    }
                    else
                    {
                        AddGeometryEntry(zip, $"{styleKey}_{size}{FileExtension(safeFormat)}", safeFormat, pieceList, size);
                    }
                }

                AddTextEntry(zip, "manifest.txt",
                    $"Source: drafted from size chart (not canvas edits)\nStyle: {styleKey}\nFormat: {safeFormat}\nSizes: {string.Join(",", pickedSizes)}\n" +
                    "Pipeline: each size from DraftGradedSet, then NotchGrainResolver.ApplyAutomation (snap notches, grain if missing, catalog rule notches).\n" +
                    FormatLayerNote(safeFormat) + "\n" +
                    $"GeneratedUtc: {DateTime.UtcNow:O}\n");
            }
        }

        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Export ZIP generation produced zero bytes.");

        var purposeTag = purpose switch
        {
            ExportPurpose.CloReview => "clo-review",
            ExportPurpose.Draft => "draft",
            _ => "factory",
        };
        var fileStem = patternId > 0
            ? $"pattern-{patternId}-{styleKey}-{safeFormat.ToLowerInvariant()}-{purposeTag}"
            : $"pattern-export-{styleKey}-{safeFormat.ToLowerInvariant()}-{purposeTag}";
        return (bytes, "application/zip", $"{fileStem}.zip");
    }

    private static string NormalizeFormat(string format)
    {
        var f = string.IsNullOrWhiteSpace(format) ? "DXF" : format.Trim().ToUpperInvariant();
        return SupportedFormats.Contains(f) ? f : "DXF";
    }

    private static string FileExtension(string format) => format switch
    {
        "HPGL" => ".hpgl",
        "PLT" => ".plt",
        _ => ".dxf",
    };

    private static string FormatLayerNote(string format) => format switch
    {
        "HPGL" or "PLT" => "Plotter pens: SP1=CUT, SP2=SA, SP3=GRAIN, SP4=NOTCH. Coordinates in standard HPGL units (1016/in).",
        "PDF" => "One PDF per piece per size (mm scale). Open and print from Adobe Reader or any PDF viewer.",
        _ => "Notches: rule-based from style assembly catalog plus drafted piece notches; grain line auto if missing. DXF layers: CUT, SA, GRAIN, NOTCH.",
    };

    private static void AddCloReadme(ZipArchive zip)
    {
        const string readme =
            "CLO3D REVIEW PACKAGE\r\n" +
            "1) Extract this ZIP.\r\n" +
            "2) In CLO: File > Import > DXF — use the base-size file in canvas/.\r\n" +
            "3) This package is for drape/fit review only. Do not send to the cutting room.\r\n" +
            "4) After approval, export the factory-certified package from PatternPro Export.\r\n";
        AddTextEntry(zip, "README-CLO.txt", readme);
    }

    private static string BuildCanvasManifest(
        int patternId,
        string styleKey,
        string safeFormat,
        IReadOnlyList<string> pickedSizes,
        string baseSize,
        Pattern.Core.Model.Pattern? pattern,
        ExportPurpose purpose)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source: saved canvas geometry (pattern id {patternId})");
        sb.AppendLine($"Export purpose: {purpose}");
        sb.AppendLine($"Base size (canvas master): {baseSize}");
        sb.AppendLine($"Style: {styleKey}");
        sb.AppendLine($"Format: {safeFormat}");
        sb.AppendLine($"Sizes: {string.Join(",", pickedSizes)}");
        if (pattern is not null && purpose == ExportPurpose.Factory)
        {
            sb.AppendLine($"Production certified: yes");
            sb.AppendLine($"Approved for cutting: {pattern.ApprovedForCutting} by {pattern.ApprovedBy} at {pattern.ApprovedAt:O}");
            sb.AppendLine($"Cutter test passed: {pattern.CutterTestPassed} by {pattern.CutterTestedBy} at {pattern.CutterTestedAt:O}");
            if (!string.IsNullOrWhiteSpace(pattern.CutterTestNotes))
                sb.AppendLine($"Cutter notes: {pattern.CutterTestNotes}");
            if (pattern.ShrinkagePercent > 0)
                sb.AppendLine($"Shrinkage allowance: {pattern.ShrinkagePercent}%");
        }
        sb.AppendLine(FormatLayerNote(safeFormat));
        sb.AppendLine($"GeneratedUtc: {DateTime.UtcNow:O}");
        return sb.ToString();
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

    private static void AddZipReadmeForPlotter(ZipArchive zip)
    {
        const string readme =
            "PATTERN EXPORT — FACTORY / PLOTTER\r\n" +
            "1) Extract this ZIP.\r\n" +
            "2) DXF: open in Gerber, Lectra, Optitex, or AutoCAD-compatible CAM.\r\n" +
            "3) HPGL / PLT: send to HPGL-compatible plotter or cutter (pen order: CUT, SA, GRAIN, NOTCH).\r\n" +
            "4) Units: DXF in mm ($INSUNITS=4); HPGL/PLT in standard plotter units (1016 per inch).\r\n";
        AddTextEntry(zip, "README-PLOTTER.txt", readme);
    }

    private static void AddZipReadmeForPrint(ZipArchive zip)
    {
        const string readme =
            "PATTERN EXPORT — PRINT (PDF)\r\n" +
            "1) Extract this ZIP.\r\n" +
            "2) Open each .pdf in canvas/{size}/ (e.g. canvas/M/Front_Leg.pdf).\r\n" +
            "3) Print from Adobe Reader, browser, or Windows Print to PDF.\r\n" +
            "4) Scale: 100% / Actual size — geometry is in millimeters on the page.\r\n" +
            "5) Black line = cut edge; dashed red = seam allowance; green dashed = grain.\r\n";
        AddTextEntry(zip, "README-PRINT.txt", readme);
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
            var gradedList = draftingService.GradeCanvasPiecesForSize(pieces, styleKey, patternBaseSize, size).ToList();
            NotchGrainResolver.ApplyAutomation(gradedList, styleKey);
            if (string.Equals(safeFormat, "PDF", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var piece in gradedList)
                    AddBinaryEntry(zip,
                        $"canvas/{size}/{SanitizeFileSegment(piece.Name)}.pdf",
                        BuildPdf(piece, size));
            }
            else
            {
                AddGeometryEntry(zip, $"canvas/{styleKey}_{size}{FileExtension(safeFormat)}", safeFormat, gradedList, $"Pattern {patternId} {size}");
            }
        }
    }

    private static void AddGeometryEntry(
        ZipArchive zip,
        string path,
        string safeFormat,
        IReadOnlyList<PieceDefinition> pieces,
        string sizeLabel)
    {
        var content = safeFormat switch
        {
            "DXF" => BuildCombinedDxf(pieces, sizeLabel),
            "HPGL" => BuildCombinedHpgl(pieces, sizeLabel),
            "PLT" => BuildCombinedPlt(pieces, sizeLabel),
            _ => $"Unsupported format '{safeFormat}'.",
        };
        AddTextEntry(zip, path, content);
    }

    private static string SanitizeFileSegment(string name)
    {
        var s = Regex.Replace(name.Trim(), @"[^\w\-\.]+", "_", RegexOptions.None, TimeSpan.FromSeconds(1));
        return string.IsNullOrEmpty(s) ? "piece" : s;
    }

    private static void AddTextEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AddBinaryEntry(ZipArchive zip, string path, byte[] content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    /// <summary>One piece per PDF page, coordinates in millimeters (for paper printing).</summary>
    private static byte[] BuildPdf(PieceDefinition piece, string sizeLabel)
    {
        const double mmScale = 10.0 / SeamGeometry.PixelsPerCm;
        const double ptPerMm = 72.0 / 25.4;
        const double marginMm = 12;
        const double labelMm = 10;

        if (piece.Points.Count < 2)
        {
            var empty = new PdfDocument();
            var p = empty.AddPage();
            p.Width = 200;
            p.Height = 100;
            using var g = XGraphics.FromPdfPage(p);
            g.DrawString($"{piece.Name} ({sizeLabel}) — no geometry", new XFont("Arial", 10), XBrushes.Gray, new XPoint(20, 40));
            using var ms = new MemoryStream();
            empty.Save(ms, false);
            return ms.ToArray();
        }

        var minX = piece.Points.Min(pt => pt[0] + piece.OffsetX);
        var minY = piece.Points.Min(pt => pt[1] + piece.OffsetY);
        var maxX = piece.Points.Max(pt => pt[0] + piece.OffsetX);
        var maxY = piece.Points.Max(pt => pt[1] + piece.OffsetY);

        double PtX(double canvasX) => ((canvasX - minX) * mmScale + marginMm) * ptPerMm;
        double PtY(double canvasY) => ((canvasY - minY) * mmScale + marginMm + labelMm) * ptPerMm;

        var widthPt = Math.Max(120, ((maxX - minX) * mmScale + marginMm * 2) * ptPerMm);
        var heightPt = Math.Max(120, ((maxY - minY) * mmScale + marginMm * 2 + labelMm) * ptPerMm);

        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = widthPt;
        page.Height = heightPt;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var outline = piece.Points
                .Select(pt => new XPoint(PtX(pt[0] + piece.OffsetX), PtY(pt[1] + piece.OffsetY)))
                .ToArray();

            if (outline.Length >= 2)
            {
                var path = new XGraphicsPath();
                path.AddLines(outline);
                path.CloseFigure();
                gfx.DrawPath(new XPen(XColors.Black, 1.2), XBrushes.Transparent, path);
            }

            if (piece.SeamAllowance > 0.0001)
            {
                var basePts = piece.Points.Select(pt => new SeamAllowanceOffset.Pt(
                    pt[0] + piece.OffsetX,
                    pt[1] + piece.OffsetY)).ToList();

                var saPts = SeamAllowanceOffset.OffsetClosed(
                    basePts,
                    piece.SeamAllowance,
                    SeamAllowanceOffset.ParseJoin(piece.SeamAllowanceJoin));

                if (saPts.Count >= 3)
                {
                    var saPath = new XGraphicsPath();
                    saPath.AddLines(saPts.Select(pt => new XPoint(PtX(pt.X), PtY(pt.Y))).ToArray());
                    saPath.CloseFigure();
                    var pen = new XPen(XColors.DarkRed, 0.8) { DashStyle = XDashStyle.Dash };
                    gfx.DrawPath(pen, XBrushes.Transparent, saPath);
                }
            }

            ExportAnnotations.DrawGrainPdf(gfx, piece, minX, minY, mmScale, marginMm, labelMm, ptPerMm);
            ExportAnnotations.DrawNotchesPdf(gfx, piece, minX, minY, mmScale, marginMm, labelMm, ptPerMm);

            var title = $"{piece.Name} — {sizeLabel}";
            gfx.DrawString(title, new XFont("Arial", 10, XFontStyle.Bold), XBrushes.Black, new XPoint(marginMm * ptPerMm, marginMm * ptPerMm));
        }

        using var pdfMs = new MemoryStream();
        doc.Save(pdfMs, false);
        return pdfMs.ToArray();
    }

    /// <summary>
    /// AutoCAD R12-style ASCII DXF using only LINE entities.
    /// </summary>
    private static string BuildCombinedDxf(IReadOnlyList<PieceDefinition> pieces, string _sizeName)
    {
        const double gap = 40;
        const double mmScale = 10.0 / SeamGeometry.PixelsPerCm;
        var nl = "\r\n";
        var sb = new StringBuilder();

        sb.Append($"0{nl}SECTION{nl}2{nl}HEADER{nl}9{nl}$ACADVER{nl}1{nl}AC1009{nl}9{nl}$INSUNITS{nl}70{nl}4{nl}0{nl}ENDSEC{nl}");
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
                var x1 = (p.Points[i][0] + p.OffsetX + dx) * mmScale;
                var y1 = (p.Points[i][1] + p.OffsetY + dy) * mmScale;
                var x2 = (p.Points[j][0] + p.OffsetX + dx) * mmScale;
                var y2 = (p.Points[j][1] + p.OffsetY + dy) * mmScale;
                sb.Append($"0{nl}LINE{nl}8{nl}CUT{nl}");
                sb.Append($"10{nl}{x1.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"20{nl}{y1.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"11{nl}{x2.ToString(CultureInfo.InvariantCulture)}{nl}");
                sb.Append($"21{nl}{y2.ToString(CultureInfo.InvariantCulture)}{nl}");
            }

            if (p.SeamAllowance > 0.0001)
            {
                var basePts = p.Points.Select(pt => new SeamAllowanceOffset.Pt(
                    pt[0] + p.OffsetX + dx,
                    pt[1] + p.OffsetY + dy)).ToList();

                var saPts = SeamAllowanceOffset.OffsetClosed(
                    basePts,
                    p.SeamAllowance,
                    SeamAllowanceOffset.ParseJoin(p.SeamAllowanceJoin));

                if (saPts.Count >= 3)
                {
                    for (var i = 0; i < saPts.Count; i++)
                    {
                        var j = (i + 1) % saPts.Count;
                        var a = saPts[i];
                        var b = saPts[j];
                        sb.Append($"0{nl}LINE{nl}8{nl}SA{nl}");
                        sb.Append($"10{nl}{(a.X * mmScale).ToString(CultureInfo.InvariantCulture)}{nl}");
                        sb.Append($"20{nl}{(a.Y * mmScale).ToString(CultureInfo.InvariantCulture)}{nl}");
                        sb.Append($"11{nl}{(b.X * mmScale).ToString(CultureInfo.InvariantCulture)}{nl}");
                        sb.Append($"21{nl}{(b.Y * mmScale).ToString(CultureInfo.InvariantCulture)}{nl}");
                    }
                }
            }

            ExportAnnotations.AppendGrainDxf(sb, p, dx, dy, mmScale);
            ExportAnnotations.AppendNotchesDxf(sb, p, dx, dy, mmScale);

            curX += (w + gap) * mmScale;
        }

        sb.Append($"0{nl}ENDSEC{nl}0{nl}EOF{nl}");
        return sb.ToString();
    }

    /// <summary>HPGL plotter file — standard 1016 units/inch, pens SP1–SP4 for CUT/SA/GRAIN/NOTCH.</summary>
    private static string BuildCombinedHpgl(IReadOnlyList<PieceDefinition> pieces, string _sizeName) =>
        BuildCombinedPlotter(pieces, includePltHeader: false);

    /// <summary>PLT uses the same HPGL command stream; some cutters expect a .plt extension only.</summary>
    private static string BuildCombinedPlt(IReadOnlyList<PieceDefinition> pieces, string _sizeName) =>
        BuildCombinedPlotter(pieces, includePltHeader: true);

    private static string BuildCombinedPlotter(IReadOnlyList<PieceDefinition> pieces, bool includePltHeader)
    {
        const double gap = 40;
        var scale = HpglHelpers.CanvasToPlotterScale;
        var mmScale = 10.0 / SeamGeometry.PixelsPerCm;
        var sb = new StringBuilder();

        if (includePltHeader)
            sb.AppendLine("; PatternPro PLT export — HPGL command stream");

        sb.AppendLine("IN;");

        double curX = 0;
        foreach (var p in pieces)
        {
            var xs = p.Points.Select(pt => pt[0] + p.OffsetX).ToArray();
            var ys = p.Points.Select(pt => pt[1] + p.OffsetY).ToArray();
            if (xs.Length == 0) continue;

            var minX = xs.Min();
            var minY = ys.Min();
            var w = xs.Max() - minX;
            var dx = curX - minX;
            var dy = -minY;

            if (p.Points.Count >= 2)
            {
                sb.Append("SP1;");
                var cutPts = p.Points
                    .Select(pt => (
                        (pt[0] + p.OffsetX + dx) * scale,
                        (pt[1] + p.OffsetY + dy) * scale))
                    .ToList();
                HpglHelpers.ClosedPolygon(sb, cutPts);
            }

            if (p.SeamAllowance > 0.0001)
            {
                var basePts = p.Points.Select(pt => new SeamAllowanceOffset.Pt(
                    pt[0] + p.OffsetX + dx,
                    pt[1] + p.OffsetY + dy)).ToList();

                var saPts = SeamAllowanceOffset.OffsetClosed(
                    basePts,
                    p.SeamAllowance,
                    SeamAllowanceOffset.ParseJoin(p.SeamAllowanceJoin));

                if (saPts.Count >= 3)
                {
                    sb.Append("SP2;");
                    var plotSa = saPts.Select(pt => (pt.X * scale, pt.Y * scale)).ToList();
                    HpglHelpers.ClosedPolygon(sb, plotSa);
                }
            }

            ExportAnnotations.AppendGrainHpgl(sb, p, dx, dy, scale);
            ExportAnnotations.AppendNotchesHpgl(sb, p, dx, dy, scale);

            curX += (w + gap) * mmScale;
        }

        sb.AppendLine("SP0;");
        sb.AppendLine("PG;");
        return sb.ToString();
    }

    private static string BuildCertificationJson(Pattern.Core.Model.Pattern pattern, ProductionValidationReport report) =>
        JsonSerializer.Serialize(new
        {
            patternId = pattern.Id,
            code = pattern.Code,
            revision = pattern.Revision,
            season = pattern.Season,
            lifecycle = pattern.LifecycleStatus,
            approvedForCutting = pattern.ApprovedForCutting,
            approvedAt = pattern.ApprovedAt,
            approvedBy = pattern.ApprovedBy,
            cutterTestPassed = pattern.CutterTestPassed,
            cutterTestedAt = pattern.CutterTestedAt,
            cutterTestedBy = pattern.CutterTestedBy,
            shrinkagePercent = pattern.ShrinkagePercent,
            canExportToFactory = report.CanExportToFactory,
            issues = report.Issues,
            warnings = report.Warnings,
            exportedUtc = DateTime.UtcNow,
        }, new JsonSerializerOptions { WriteIndented = true });
}
