using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class GradingController(IGradingService gradingService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var rows = gradingService.GetGradingTable(style);
        var label = gradingService.GetStyleLabel(style);

        var vm = new GradingViewModel
        {
            StyleKey   = style,
            StyleLabel = label,
            Rows = rows.Select(r => new GradingRowViewModel
            {
                MeasurementPoint = r.MeasurementPoint,
                XS  = r.XS,
                S   = r.S,
                L   = r.L,
                XL  = r.XL,
                XXL = r.XXL,
            }).ToList(),
        };

        SetLayout("Grading", "Grading", style);
        return View(vm);
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

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}
