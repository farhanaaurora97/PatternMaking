using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class PiecesController(
    IPieceService pieceService,
    IPatternService patternService,
    IPatternDraftingService draftingService) : Controller
{
    public IActionResult Index(int patternId, string style = "skinny")
    {
        SetLayout("Pieces", "Pattern Pieces", style, patternId);
        return View(BuildViewModel(patternId, style));
    }

    [HttpPost]
    public IActionResult AddPiece(
        int patternId, string style, string pieceName, string category,
        string cut, string grainLine, string color, string description)
    {
        var (ok, error, piece) = pieceService.AddPiece(
            patternId, style, pieceName, category, cut, grainLine, color, description);

        if (!ok)
            return BadRequest(new { error });

        var defs    = pieceService.GetPieceDefinitions(patternId, style);
        var pattern = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);
        if (pattern is not null)
            pattern.PieceCount = defs.Count;

        return Ok(new
        {
            index          = defs.Count - 1,
            name           = piece!.Name,
            category       = piece.Category,
            cutInstruction = piece.Cut,
            grainLine      = piece.GrainLine,
            color          = piece.Color,
            description    = piece.Description,
        });
    }

    [HttpPost]
    public IActionResult DraftPieces(int patternId, string style)
    {
        var pattern = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);
        var baseSize = pattern?.BaseSize ?? "M";

            var pieces = draftingService.DraftProductionPieces(style, baseSize, patternId);
        pieceService.ReplacePatternPieces(patternId, pieces);
        if (pattern is not null)
            pattern.PieceCount = pieces.Count;

        SetLayout("Pieces", "Pattern Pieces", style, patternId);
        return RedirectToAction("Index", new { patternId, style });
    }

    [HttpPost]
    public IActionResult RefinePieces(int patternId, string style)
    {
        var defs = pieceService.GetPieceDefinitions(patternId, style)
            .Select(PatternAutoRefineService.Clone)
            .ToList();
        PatternAutoRefineService.Refine(defs, style);
        pieceService.ReplacePatternPieces(patternId, defs);
        SetLayout("Pieces", "Pattern Pieces", style, patternId);
        return RedirectToAction("Index", new { patternId, style });
    }

    [HttpPost]
    public IActionResult DeletePiece(int patternId, string style, string pieceName)
    {
        var (ok, error) = pieceService.DeletePiece(patternId, pieceName);

        var vm = BuildViewModel(patternId, style);
        if (!ok) vm.ErrorMessage = error;

        SetLayout("Pieces", "Pattern Pieces", style, patternId);
        return View("Index", vm);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private PiecesViewModel BuildViewModel(int patternId, string style)
    {
        var styleDef = pieceService.GetStyleDefinition(style);
        var defs     = pieceService.GetPieceDefinitions(patternId, style);
        var pattern  = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);

        return new PiecesViewModel
        {
            PatternId   = patternId,
            PatternName = pattern?.Name ?? string.Empty,
            PatternCode = pattern?.Code ?? string.Empty,
            StyleKey    = style,
            StyleLabel  = styleDef.Label,
            Pieces      = defs.Select((d, i) => new PieceCardViewModel
            {
                Index          = i,
                Name           = d.Name,
                CutInstruction = d.Cut,
                Color          = d.Color,
                Category       = d.Category,
                GrainLine      = d.GrainLine,
                Description    = d.Description,
            }).ToList(),
        };
    }

    private void SetLayout(string controller, string title, string style, int patternId = 0) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
            CurrentPatternId = patternId > 0 ? patternId : null,
        };
}
