using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class SizeChartController(
    ISizeChartService sizeChartService,
    IPatternService patternService) : Controller
{
    [HttpGet]
    public IActionResult Index(int? patternId)
    {
        var scopeId = ResolveScopeId(patternId);
        var snapshot = sizeChartService.GetSnapshot(scopeId);
        var patterns = patternService.GetAll().OrderBy(p => p.Code).ToList();
        var selectedId = scopeId ?? 0;
        var pattern = selectedId > 0
            ? patterns.FirstOrDefault(p => p.Id == selectedId)
            : null;
        var baseSize = !string.IsNullOrWhiteSpace(pattern?.BaseSize) ? pattern!.BaseSize : "M";

        var vm = new SizeChartViewModel
        {
            ColumnLabels = snapshot.Columns,
            Rows = snapshot.Rows.Select(r => new SizeRowViewModel
            {
                MeasurementPoint = r.MeasurementPoint,
                ToleranceCm = r.ToleranceCm,
                MeasurementMethod = r.MeasurementMethod,
                Values = r.Values,
            }).ToList(),
            ScopeLabel = snapshot.ScopeLabel,
            SelectedPatternId = selectedId,
            ChartMode = snapshot.ChartMode,
            UseCustomChart = snapshot.UseCustomChart,
            BaseSizeLabel = baseSize,
            Patterns = patterns.Select(p => new SizeChartPatternOption
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Season = p.Season,
                BaseSize = p.BaseSize,
            }).ToList(),
        };

        SetLayout("SizeChart", "Size Chart");
        return View(vm);
    }

    [HttpGet]
    public IActionResult ExportCsv(int? patternId)
    {
        var csv = sizeChartService.ExportCsv(ResolveScopeId(patternId));
        var fileName = patternId is > 0 ? $"size-chart-{patternId}.csv" : "size-chart.csv";
        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            fileName);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult AddColumn([FromBody] AddSizeColumnBody body)
    {
        var label = body.Label?.Trim() ?? "";
        var (ok, err) = sizeChartService.TryAddSizeColumn(label, ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { label });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult AddRow([FromBody] AddMeasurementRowBody body)
    {
        var name = body.Name?.Trim() ?? "";
        var copyFrom = body.CopyFrom?.Trim() ?? "";
        var (ok, err) = sizeChartService.TryAddMeasurementRow(name, copyFrom, ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { name });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateCell([FromBody] UpdateSizeCellBody body)
    {
        var (ok, err) = sizeChartService.TryUpdateCell(
            body.MeasurementPoint ?? "", body.ColumnIndex, body.Value, ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { measurementPoint = body.MeasurementPoint, columnIndex = body.ColumnIndex, value = body.Value });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateRowMeta([FromBody] UpdateSizeRowMetaBody body)
    {
        var (ok, err) = sizeChartService.TryUpdateRowMeta(
            body.MeasurementPoint ?? "", body.ToleranceCm, body.MeasurementMethod, ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult DeleteRow([FromBody] DeleteMeasurementRowBody body)
    {
        var (ok, err) = sizeChartService.TryDeleteMeasurementRow(body.MeasurementPoint ?? "", ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { measurementPoint = body.MeasurementPoint });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult DeleteColumn([FromBody] DeleteSizeColumnBody body)
    {
        var (ok, err) = sizeChartService.TryDeleteSizeColumn(body.ColumnIndex, ResolveScopeId(body.PatternId));
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { columnIndex = body.ColumnIndex });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult SetChartSettings([FromBody] SetChartSettingsBody body)
    {
        if (body.PatternId is not > 0)
            return BadRequest(new { error = "Select a style first." });

        var (ok, err) = sizeChartService.SetChartSettings(body.PatternId, body.UseCustomChart, body.ChartMode ?? MeasurementChartMode.Body);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult CopyGlobal([FromBody] PatternScopeBody body)
    {
        if (body.PatternId is not > 0)
            return BadRequest(new { error = "Select a style first." });

        var (ok, err) = sizeChartService.CopyGlobalToPattern(body.PatternId);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult InitializeGarmentTemplate([FromBody] PatternScopeBody body)
    {
        if (body.PatternId is not > 0)
            return BadRequest(new { error = "Select a style first." });

        var (ok, err) = sizeChartService.InitializeGarmentTemplate(body.PatternId);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok();
    }

    private static int? ResolveScopeId(int? patternId) =>
        patternId is > 0 ? patternId : null;

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel { ActiveController = controller, PageTitle = title };
}

public sealed record AddSizeColumnBody(string? Label, int? PatternId = null);

public sealed record AddMeasurementRowBody(string? Name, string? CopyFrom, int? PatternId = null);

public sealed record UpdateSizeCellBody(string? MeasurementPoint, int ColumnIndex, decimal Value, int? PatternId = null);

public sealed record UpdateSizeRowMetaBody(string? MeasurementPoint, decimal ToleranceCm, string? MeasurementMethod, int? PatternId = null);

public sealed record DeleteMeasurementRowBody(string? MeasurementPoint, int? PatternId = null);

public sealed record DeleteSizeColumnBody(int ColumnIndex, int? PatternId = null);

public sealed record SetChartSettingsBody(int PatternId, bool UseCustomChart, string? ChartMode);

public sealed record PatternScopeBody(int PatternId);
