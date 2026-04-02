using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class BlockGeneratorController(IBlockGeneratorService blockGenService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var vm = BuildViewModel(style);
        SetLayout("BlockGenerator", "Block Generator", style);
        return View(vm);
    }

    // ── AJAX: Save ease override ────────────────────────────────────
    [HttpPost]
    public IActionResult SaveEase([FromBody] SaveEaseRequest req)
    {
        blockGenService.SetEaseOverride(req.StyleKey, req.MeasurementKey, req.Value);
        var vm = BuildViewModel(req.StyleKey);
        return Ok(vm.EaseValues);
    }

    // ── AJAX: Reset ease ────────────────────────────────────────────
    [HttpPost]
    public IActionResult ResetEase(string styleKey)
    {
        blockGenService.ResetEase(styleKey);
        var vm = BuildViewModel(styleKey);
        return Ok(vm.EaseValues);
    }

    // ── AJAX: Generate block ────────────────────────────────────────
    [HttpPost]
    public IActionResult GenerateBlock(string styleKey)
    {
        var result = blockGenService.GenerateBlock(styleKey);
        return Ok(new { result.StyleLabel, result.PieceCount });
    }

    private BlockGeneratorViewModel BuildViewModel(string styleKey)
    {
        var def = blockGenService.GetDefinition(styleKey);
        return new BlockGeneratorViewModel
        {
            StyleKey    = styleKey,
            StyleLabel  = def.Label,
            FitProfile  = def.FitProfile,
            EaseValues  = blockGenService.GetEffectiveEase(styleKey),
            Formulas    = def.Formulas.Select(f => new DraftingFormulaViewModel
            {
                Key      = f.Key,
                Equation = f.Equation,
            }).ToList(),
        };
    }

    private void SetLayout(string controller, string title, string style) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
        };
}

public record SaveEaseRequest(string StyleKey, string MeasurementKey, decimal Value);
