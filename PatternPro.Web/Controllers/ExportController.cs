using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class ExportController(IExportService exportService, IPatternService patternService) : Controller
{
    public IActionResult Index(int patternId = 0, string style = "skinny", string? sizes = null, string? source = null)
    {
        var patterns = patternService.GetAll().ToList();
        var selectedPattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : patterns.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).FirstOrDefault();
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
            CanvasGradeBaseSize = selectedPattern is not null ? selectedPattern.BaseSize : null,
        };

        ViewBag.ExportSource  = source ?? "standard";
        ViewBag.CurrentStyle  = style;
        SetLayout("Export", "Export / DXF", style, vm.PatternId > 0 ? vm.PatternId : null);
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

    /// <summary>ZIP download for Export page — GET must stay callable via fetch (same-origin credentials).</summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DownloadPackage(int patternId = 0, string style = "skinny", string format = "DXF", string? sizes = null)
    {
        var patterns = patternService.GetAll().ToList();
        var pattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : patterns.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).FirstOrDefault();
        if (pattern is not null)
        {
            patternId = pattern.Id;
            style = ToStyleKey(pattern.Style);
        }
        var selectedSizes = ParseSizes(sizes);
        byte[] bytes;
        string contentType;
        string fileName;
        try
        {
            (bytes, contentType, fileName) = exportService.BuildExportPackage(style, format, selectedSizes, patternId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (bytes.Length == 0)
            return BadRequest("Export produced no data.");

        // Avoid stale or stripped bodies from intermediaries when downloading ZIP (DXF/SVG/PDF inside).
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
        Response.Headers["Pragma"] = "no-cache";

        // Stream-based FileResult plays nicer with fetch().blob() than raw byte[] on some hosts.
        var ms = new MemoryStream(bytes, writable: false);
        return File(ms, contentType, fileName);
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

    private void SetLayout(string controller, string title, string style, int? patternId = null) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
            CurrentPatternId = patternId,
        };
}

public record ExportRequest(string Format);
