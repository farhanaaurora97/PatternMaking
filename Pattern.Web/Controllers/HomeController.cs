using Microsoft.AspNetCore.Mvc;
using Pattern.PublicServices.Interfaces;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class HomeController(IPatternService patternService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var patterns = patternService.GetAll();
        var styleDef  = patternService.GetStyleDefinition(style);

        var vm = new DashboardViewModel
        {
            CurrentStyle      = style,
            CurrentStyleLabel = styleDef.Label,
            PieceCount        = styleDef.PieceCount,
            Patterns          = patterns.Select(p => p.ToViewModel()).ToList(),
            ActivePatternCount = patterns.Count,
            CompletionPercent  = patterns.Count == 0 ? 0
                : (int)Math.Round(patterns.Count(p => p.Status is "Graded" or "Done") * 100.0 / patterns.Count),
            PendingCount = patterns.Count(p => p.Status is "Pending" or "Draft"),
            StyleProgress = BuildStyleProgress(patterns),
            RecentActivity =
            [
                new("Graded", "ab-green", "Skinny — XS to XXL grading complete",     "2h ago"),
                new("Draft",  "ab-navy",  "Bootcut block generated from M base",      "5h ago"),
                new("Export", "ab-gold",  "Slim DXF exported to PLM system",          "1d ago"),
                new("Review", "ab-red",   "Crotch curve adjustment needed — back leg","2d ago"),
                new("Done",   "ab-green", "Straight — seam allowance layer set",      "3d ago"),
            ],
            CreateForm = new PatternCreateViewModel(),
        };

        SetLayoutData("Home", "Dashboard", style, vm.PendingCount);
        return View(vm);
    }

    // ── AJAX: Create pattern ────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PatternCreateViewModel form)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

        var created = patternService.Create(form.Name, form.StyleKey, form.BaseSize, form.Designer);
        return Ok(created.ToViewModel());
    }

    // ── AJAX: Cycle status ──────────────────────────────────────────
    [HttpPost]
    public IActionResult CycleStatus(int id)
    {
        var updated = patternService.CycleStatus(id);
        if (updated is null) return NotFound();
        return Ok(updated.ToViewModel());
    }

    // ── AJAX: Delete pattern ────────────────────────────────────────
    [HttpDelete]
    public IActionResult Delete(int id)
    {
        var ok = patternService.Delete(id);
        return ok ? Ok() : NotFound();
    }

    // ── AJAX: Get all patterns (for re-render) ──────────────────────
    [HttpGet]
    public IActionResult Patterns(string? q, string? sort, bool asc = true)
    {
        var patterns = patternService.Search(q);
        if (!string.IsNullOrEmpty(sort))
            patterns = patternService.Sort(patterns, sort, asc);
        return Ok(patterns.Select(p => p.ToViewModel()));
    }

    private static Dictionary<string, int> BuildStyleProgress(IEnumerable<Pattern.Core.Model.Pattern> patterns)
    {
        var styles = new[] { "Skinny", "Slim", "Straight", "Bootcut", "Wide Leg" };
        var keys   = new[] { "skinny", "slim", "straight", "bootcut", "wideleg" };
        var defaults = new[] { 90, 75, 60, 40, 20 };
        var result = new Dictionary<string, int>();

        for (int i = 0; i < styles.Length; i++)
        {
            var list  = patterns.Where(p => p.Style == styles[i]).ToList();
            var done  = list.Count(p => p.Status is "Graded" or "Done");
            result[keys[i]] = list.Count == 0 ? defaults[i]
                : (int)Math.Round(done * 100.0 / list.Count);
        }
        return result;
    }

    private void SetLayoutData(string controller, string pageTitle, string style, int pendingCount) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = pageTitle,
            CurrentStyle     = style,
            PendingBadgeCount = pendingCount,
        };
}
