using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class ExportController(IExportService exportService, IPatternService patternService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var patterns = patternService.GetAll();
        var first    = patterns.FirstOrDefault();

        var vm = new ExportViewModel
        {
            PatternDisplayName = first is not null ? $"{first.Code} {first.Name}" : "—",
            StyleLabel         = patternService.GetStyleDefinition(style).Label,
            PieceCount         = first?.PieceCount ?? 9,
            SelectedFormat     = "DXF",
        };

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

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}

public record ExportRequest(string Format);
