using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class PiecesController(IPieceService pieceService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var styleDef = pieceService.GetStyleDefinition(style);
        var pieces   = pieceService.GetPieceList(style);

        var vm = new PiecesViewModel
        {
            StyleKey   = style,
            StyleLabel = styleDef.Label,
            Pieces = pieces.Select((name, i) => new PieceCardViewModel
            {
                Index          = i,
                Name           = name,
                CutInstruction = i == 2 ? "cut 1" : i == 8 ? "cut 8" : "cut 2",
            }).ToList(),
        };

        SetLayout("Pieces", "Pattern Pieces", style);
        return View(vm);
    }

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}
