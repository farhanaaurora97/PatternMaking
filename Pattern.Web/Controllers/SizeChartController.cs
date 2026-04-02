using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class SizeChartController(ISizeChartService sizeChartService) : Controller
{
    public IActionResult Index()
    {
        var rows = sizeChartService.GetAll();

        var vm = new SizeChartViewModel
        {
            Rows = rows.Select(r => new SizeRowViewModel
            {
                MeasurementPoint = r.MeasurementPoint,
                XS  = r.XS,
                S   = r.S,
                M   = r.M,
                L   = r.L,
                XL  = r.XL,
                XXL = r.XXL,
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

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel { ActiveController = controller, PageTitle = title };
}
