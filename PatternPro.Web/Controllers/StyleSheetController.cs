using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

/// <summary>PLM-style style register (code, season, owner, lifecycle) — no pattern geometry.</summary>
public class StyleSheetController(IPatternService patternService) : Controller
{
    public IActionResult Index()
    {
        var list = patternService.GetAll().ToList();
        var rows = list.Select(p => p.ToViewModel()).ToList();
        var seasons = rows.Select(r => r.Season)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var vm = new StyleSheetViewModel
        {
            Rows = rows,
            TotalCount = rows.Count,
            SeasonOptions = seasons,
        };

        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = "StyleSheet",
            PageTitle = "Style Sheet",
            CurrentStyle = "skinny",
            PendingBadgeCount = list.Count(p => p.Status is "Pending" or "Draft"),
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Rows(string? q, string? sort, bool asc = true)
    {
        var patterns = patternService.Search(q);
        if (!string.IsNullOrEmpty(sort))
            patterns = patternService.Sort(patterns, sort, asc);
        return Ok(patterns.Select(p => p.ToViewModel()));
    }
}
