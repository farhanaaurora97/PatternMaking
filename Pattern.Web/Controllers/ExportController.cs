using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class ExportController(IExportService exportService, IPatternService patternService) : Controller
{
    public IActionResult Index(int patternId = 0, string style = "skinny", string? sizes = null, string? source = null)
    {
        var patterns = patternService.GetAll();
        var selectedPattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : patterns.FirstOrDefault();
        if (selectedPattern is not null)
        {
            patternId = selectedPattern.Id;
            style = ToStyleKey(selectedPattern.Style);
        }
        var selectedSizes = ParseSizes(sizes);

        var vm = new ExportViewModel
        {
            PatternId          = patternId,
            PatternDisplayName = selectedPattern is not null ? $"{selectedPattern.Code} {selectedPattern.Name}" : "—",
            StyleLabel         = patternService.GetStyleDefinition(style).Label,
            PieceCount         = selectedPattern?.PieceCount ?? 9,
            SizeCount          = selectedSizes.Count,
            SizesCsv           = string.Join(",", selectedSizes),
            SelectedFormat     = "DXF",
        };

        ViewBag.ExportSource  = source ?? "standard";
        ViewBag.CurrentStyle  = style;
        SetLayout("Export", "Export / DXF", style);
        return View(vm);
    }

    // ── AJAX: Simulate export step progression ──────────────────────
    [HttpPost]
    public IActionResult StartExport([FromBody] ExportRequest req)
    {
        var steps = exportService.GetExportSteps(req.Format);
        return Ok(steps.Select((s, i) => new { step = i, label = s }));
    }

    // ── AJAX: Return piece names for canvas preview ──────────────────
    [HttpGet]
    public IActionResult PreviewPieces(string style = "skinny")
    {
        var pieces = patternService.GetStyleDefinition(style).PieceList;
        return Ok(pieces);
    }

    [HttpGet]
    public IActionResult DownloadPackage(int patternId = 0, string style = "skinny", string format = "DXF", string? sizes = null)
    {
        var pattern = patternId > 0 ? patternService.GetAll().FirstOrDefault(p => p.Id == patternId) : null;
        if (pattern is not null) style = ToStyleKey(pattern.Style);
        var selectedSizes = ParseSizes(sizes);
        var (bytes, contentType, fileName) = exportService.BuildExportPackage(style, format, selectedSizes);
        return File(bytes, contentType, fileName);
    }

    private static List<string> ParseSizes(string? sizes)
    {
        var parsed = (sizes ?? "XS,S,M,L,XL,XXL")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .Distinct()
            .ToList();
        return parsed.Count == 0 ? ["XS", "S", "M", "L", "XL", "XXL"] : parsed;
    }

    private static string ToStyleKey(string styleLabel) => styleLabel.Trim().ToLowerInvariant() switch
    {
        "wide leg" => "wideLeg",
        "skinny" => "skinny",
        "slim" => "slim",
        "straight" => "straight",
        "bootcut" => "bootcut",
        _ => "skinny",
    };

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}

public record ExportRequest(string Format);
