using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class NestController(IPieceService pieceService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var styleKey = FitStyleKeys.Normalize(style);
        var vm = new NestViewModel { StyleKey = styleKey };
        SetLayout("Nest", "Graded Nest", styleKey);
        return View(vm);
    }

    // ── API: Base piece geometry for nest rendering ─────────────────
    [HttpGet]
    public IActionResult BasePiece()
    {
        var basePiece = pieceService.GetBasePiecePoints();
        return Ok(basePiece);
    }

    private void SetLayout(string controller, string title, string currentStyle = "skinny") =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle      = title,
            CurrentStyle   = currentStyle,
        };
}
