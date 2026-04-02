using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class CanvasController(IPieceService pieceService) : Controller
{
    public IActionResult Index(string style = "skinny", int piece = 0)
    {
        var pieceNames = pieceService.GetPieceList(style);

        var vm = new CanvasViewModel
        {
            StyleKey           = style,
            PieceNames         = pieceNames,
            SelectedPieceIndex = piece,
        };

        SetLayout("Canvas", "Canvas Editor");
        return View(vm);
    }

    // ── API: Return piece geometry JSON for canvas engine ───────────
    [HttpGet]
    public IActionResult PieceData()
    {
        var defs = pieceService.GetPieceDefinitions();
        return Ok(defs);
    }

    private void SetLayout(string controller, string title) =>
        ViewData["Layout"] = new LayoutViewModel { ActiveController = controller, PageTitle = title };
}
