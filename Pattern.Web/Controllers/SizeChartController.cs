using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class SizeChartController(ISizeChartService sizeChartService) : Controller
{
    public IActionResult Index()
    {
        var columns = sizeChartService.GetColumnLabels();
        var rows = sizeChartService.GetAll();

        var vm = new SizeChartViewModel
        {
            ColumnLabels = columns,
            Rows = rows.Select(r => new SizeRowViewModel
            {
                MeasurementPoint = r.MeasurementPoint,
                Values = r.Values,
            }).ToList(),
        };

        SetLayout("SizeChart", "Size Chart");
        return View(vm);
    }

    [HttpGet]
    public IActionResult ExportCsv()
    {
        var csv = sizeChartService.ExportCsv();
        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            "size-chart.csv");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult AddColumn([FromBody] AddSizeColumnBody body)
    {
        var label = body.Label?.Trim() ?? "";
        var (ok, err) = sizeChartService.TryAddSizeColumn(label);
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
        var (ok, err) = sizeChartService.TryAddMeasurementRow(name, copyFrom);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { name });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateCell([FromBody] UpdateSizeCellBody body)
    {
        var (ok, err) = sizeChartService.TryUpdateCell(body.MeasurementPoint ?? "", body.ColumnIndex, body.Value);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok(new { measurementPoint = body.MeasurementPoint, columnIndex = body.ColumnIndex, value = body.Value });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateRowMeta([FromBody] UpdateSizeRowMetaBody body)
    {
        var (ok, err) = sizeChartService.TryUpdateRowMeta(
            body.MeasurementPoint ?? "", body.ToleranceCm, body.MeasurementMethod);
        if (!ok)
            return BadRequest(new { error = err });
        return Ok();
    }

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel { ActiveController = controller, PageTitle = title };
}

public sealed record AddSizeColumnBody(string? Label);

public sealed record AddMeasurementRowBody(string? Name, string? CopyFrom);

public sealed record UpdateSizeCellBody(string? MeasurementPoint, int ColumnIndex, decimal Value);

public sealed record UpdateSizeRowMetaBody(string? MeasurementPoint, decimal ToleranceCm, string? MeasurementMethod);
