using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;
namespace Pattern.Web.Controllers;
public class NestController(IPieceService pieceService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var vm = new NestViewModel { StyleKey = style };
        SetLayout("Nest", "Graded Nest");
        return View(vm);
    }

    // ── API: Base piece geometry for nest rendering ─────────────────
    [HttpGet]
    public IActionResult BasePiece()
    {
        var basePiece = pieceService.GetBasePiecePoints();
        return Ok(basePiece);
    }

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel { ActiveController = controller, PageTitle = title };
}
