using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class GradingController(IGradingService gradingService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var rows    = gradingService.GetGradingTable(style);
        var label   = gradingService.GetStyleLabel(style);
        var columns = gradingService.GetColumnLabels().ToList();
        var baseIdx = rows.FirstOrDefault()?.BaseIndex ?? 0;

        var vm = new GradingViewModel
        {
            StyleKey     = style,
            StyleLabel   = label,
            ColumnLabels = columns,
            BaseIndex    = baseIdx,
            Rows = rows.Select(r => new GradingRowViewModel
            {
                MeasurementPoint = r.MeasurementPoint,
                Deltas           = r.Deltas,
                BaseIndex        = r.BaseIndex,
            }).ToList(),
        };

        SetLayout("Grading", "Grading", style);
        return View(vm);
    }

    [HttpPost]
    public IActionResult AddColumn(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return BadRequest(new { error = "Column label is required." });

        var trimmed = label.Trim().ToUpper();

        if (gradingService.GetColumnLabels().Any(c => c.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { error = $"Size '{trimmed}' already exists." });

        gradingService.AddColumn(trimmed);
        return Ok(new { label = trimmed, columns = gradingService.GetColumnLabels() });
    }

    [HttpPost]
    public IActionResult AddRow(string style, string measurementPoint, string? copyFrom)
    {
        var (ok, error) = gradingService.AddRow(style, measurementPoint, copyFrom);
        if (!ok) return BadRequest(new { error });

        var rows    = gradingService.GetGradingTable(style);
        var columns = gradingService.GetColumnLabels();
        var newRow  = rows.Last();

        return Ok(new
        {
            measurementPoint = newRow.MeasurementPoint,
            baseIndex        = newRow.BaseIndex,
            deltas           = newRow.Deltas,
            columns,
        });
    }

    [HttpGet]
    public IActionResult ExportCsv(string style = "skinny")
    {
        var csv = gradingService.ExportCsv(style);
        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"grading-{style}.csv");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateDelta([FromBody] UpdateGradingDeltaBody body)
    {
        var (ok, err) = gradingService.TryUpdateDelta(
            body.StyleKey ?? "", body.MeasurementPoint ?? "", body.ColumnIndex, body.Delta);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new
        {
            styleKey = body.StyleKey,
            measurementPoint = body.MeasurementPoint,
            columnIndex = body.ColumnIndex,
            delta = body.Delta,
        });
    }

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}

public sealed record UpdateGradingDeltaBody(string? StyleKey, string? MeasurementPoint, int ColumnIndex, double Delta);
