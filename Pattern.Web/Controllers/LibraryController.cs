using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class LibraryController(
    IPatternService patternService,
    IPieceService pieceService) : Controller
{
    public IActionResult Index()
    {
        var all     = patternService.GetAll();
        var savedIds = pieceService.GetSavedPatternIds();

        var entries = all.Select(p =>
        {
            var styleKey = StyleKey(p.Style);
            var hasSaved = savedIds.Contains(p.Id);
            var pieceCount = hasSaved
                ? pieceService.GetPieceDefinitions(p.Id, styleKey).Count
                : 0;

            return new LibraryEntryViewModel
            {
                Id         = p.Id,
                Code       = p.Code,
                Name       = p.Name,
                Style      = p.Style,
                StyleKey   = styleKey,
                BaseSize   = p.BaseSize,
                Category   = p.Category,
                Status     = p.Status,
                Date       = p.Date,
                HasCanvas  = hasSaved,
                PieceCount = pieceCount,
            };
        }).ToList();

        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = "Library",
            PageTitle        = "Pattern Library",
        };

        return View(entries);
    }

    private static string StyleKey(string style) => style.ToLowerInvariant() switch
    {
        "wide leg" => "wideLeg",
        var s      => s.Replace(" ", ""),
    };
}
