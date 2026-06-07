using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class HomeController(IPatternService patternService) : Controller
{
    public IActionResult Index(string style = "skinny")
    {
        var patterns = patternService.GetAll();
        var styleDef  = patternService.GetStyleDefinition(style);

        var list = patterns.ToList();
        var total = list.Count;
        var activeNonDraft = list.Count(p => p.Status != "Draft");
        var patternVms = list.Select(p => p.ToViewModel()).ToList();

        var today = DateTime.Today;
        var weekStart = StartOfWeekMonday(today);
        var weekEnd = weekStart.AddDays(7);
        var prevWeekStart = weekStart.AddDays(-7);
        var patternsThisWeek = list.Count(p => p.CreatedAt >= weekStart && p.CreatedAt < weekEnd);
        var patternsLastWeek = list.Count(p => p.CreatedAt >= prevWeekStart && p.CreatedAt < weekStart);
        var dueThisWeek = list.Count(p => p.DueDate.HasValue && p.DueDate.Value >= weekStart && p.DueDate.Value < weekEnd);

        var vm = new DashboardViewModel
        {
            CurrentStyle      = style,
            CurrentStyleLabel = styleDef.Label,
            PieceCount        = styleDef.PieceCount,
            Patterns          = patternVms,
            TotalPatternCount = total,
            ActivePatternCount = activeNonDraft,
            ActiveBarPercent   = total == 0 ? 0 : (int)Math.Round(activeNonDraft * 100.0 / total),
            AvgPieceCount      = total == 0 ? 0 : (int)Math.Round(list.Average(p => p.PieceCount)),
            GradedSizesCount   = 6,
            CompletionPercent  = total == 0 ? 0
                : (int)Math.Round(list.Count(p => p.Status is "Graded" or "Done") * 100.0 / total),
            PendingCount = list.Count(p => p.Status is "Pending" or "Draft"),
            ProductionCertifiedCount = list.Count(p => p.ApprovedForCutting && p.CutterTestPassed),
            PatternsCreatedThisWeek = patternsThisWeek,
            PatternsCreatedLastWeek = patternsLastWeek,
            DueThisWeekCount = dueThisWeek,
            StyleProgress = BuildStyleProgress(list),
            CategoryTabs = ["All", .. patternVms.Select(p => p.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase)],
            RecentActivity = BuildRecentActivity(list),
            CreateForm = new PatternCreateViewModel(),
            Charts           = BuildCharts(list),
        };

        SetLayoutData("Home", "Dashboard", style, vm.PendingCount);
        return View(vm);
    }

    // ── AJAX: Create pattern ────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Create([FromBody] PatternCreateViewModel form)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

        var created = patternService.Create(
            form.Name, form.StyleKey, form.BaseSize, form.Designer, form.CategoryKey,
            form.Season, form.Owner, form.LifecycleStatus);
        return Ok(created.ToViewModel());
    }

    // ── AJAX: Cycle status ──────────────────────────────────────────
    [HttpPost]
    [Route("Home/CycleStatus/{id:int}")]
    public IActionResult CycleStatus(int id)
    {
        var updated = patternService.CycleStatus(id);
        if (updated is null) return NotFound();
        return Ok(updated.ToViewModel());
    }

    // ── AJAX: Set status directly ───────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult SetStatus([FromBody] SetStatusBody body)
    {
        var updated = patternService.SetStatus(body.Id, body.Status);
        if (updated is null) return BadRequest();
        return Ok(updated.ToViewModel());
    }

    // ── AJAX: PLM lifecycle ───────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult SetLifecycle([FromBody] SetLifecycleBody body)
    {
        var updated = patternService.SetLifecycleStatus(body.Id, body.LifecycleStatus);
        if (updated is null) return BadRequest();
        return Ok(updated.ToViewModel());
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateStyleSheet([FromBody] UpdateStyleSheetBody body)
    {
        var updated = patternService.UpdateStyleSheet(body.Id, body.Season, body.Owner, body.Designer);
        if (updated is null) return NotFound();
        return Ok(updated.ToViewModel());
    }

    // ── AJAX: Set due date ──────────────────────────────────────────
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult SetDueDate([FromBody] SetDueDateBody body)
    {
        DateTime? date = null;
        if (!string.IsNullOrWhiteSpace(body.Date) && DateTime.TryParse(body.Date, out var d))
            date = d;
        var updated = patternService.SetDueDate(body.Id, date);
        if (updated is null) return NotFound();
        return Ok(updated.ToViewModel());
    }

    // ── AJAX: Duplicate pattern ─────────────────────────────────────
    [HttpPost]
    [Route("Home/Duplicate/{id:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Duplicate(int id)
    {
        var copy = patternService.Duplicate(id);
        if (copy is null) return NotFound();
        return Ok(copy.ToViewModel());
    }

    // ── AJAX: Delete pattern ────────────────────────────────────────
    [HttpDelete]
    [Route("Home/Delete/{id:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Delete(int id)
    {
        var ok = patternService.Delete(id);
        return ok ? Ok() : NotFound();
    }

    // ── AJAX: Chart data ────────────────────────────────────────────
    [HttpGet]
    public IActionResult ChartsData()
    {
        var list = patternService.GetAll().ToList();
        return Ok(BuildCharts(list));
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

    private static DashboardChartsModel BuildCharts(IReadOnlyList<Pattern.Core.Model.Pattern> list)
    {
        var statusOrder = new[] { "Pending", "Draft", "InProgress", "Graded", "Done" };
        var statusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Pending"] = "Pending",
            ["Draft"] = "Draft",
            ["InProgress"] = "In Progress",
            ["Graded"] = "Graded",
            ["Done"] = "Done",
        };
        var statusColors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Pending"] = "#d97706",
            ["Draft"] = "#64748b",
            ["InProgress"] = "#1e40af",
            ["Graded"] = "#16a34a",
            ["Done"] = "#7c3aed",
        };

        var statusSlices = statusOrder
            .Select(s => new ChartStatusSlice(s, statusLabels[s], list.Count(p => p.Status == s), statusColors[s]))
            .ToList();

        var pantTypeBars = list
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "Other" : p.Category)
            .Select(g => new ChartStyleBar(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var styleOrder = new[] { "Skinny", "Slim", "Straight", "Bootcut", "Wide Leg" };
        var stackedDatasets = statusOrder.Select(s => new ChartStackDataset
        {
            Key = s,
            Label = statusLabels[s],
            Data = styleOrder.Select(fit => list.Count(p => p.Style == fit && p.Status == s)).ToList(),
            BackgroundColor = statusColors[s],
        }).ToList();

        var stylesByFit = new ChartStackedByFit
        {
            Labels = styleOrder.ToList(),
            Datasets = stackedDatasets,
        };

        return new DashboardChartsModel { Status = statusSlices, StylesByFit = stylesByFit, PantTypes = pantTypeBars };
    }

    private static Dictionary<string, int> BuildStyleProgress(IEnumerable<Pattern.Core.Model.Pattern> patterns)
    {
        var styles = new[] { "Skinny", "Slim", "Straight", "Bootcut", "Wide Leg" };
        var defaults = new[] { 90, 75, 60, 40, 20 };
        var result = new Dictionary<string, int>();

        for (int i = 0; i < styles.Length; i++)
        {
            var list  = patterns.Where(p => p.Style == styles[i]).ToList();
            var done  = list.Count(p => p.Status is "Graded" or "Done");
            result[styles[i]] = list.Count == 0 ? defaults[i]
                : (int)Math.Round(done * 100.0 / list.Count);
        }
        return result;
    }

    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var d = date.Date;
        var diff = d.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - d.DayOfWeek;
        return d.AddDays(diff);
    }

    private static IReadOnlyList<ActivityItem> BuildRecentActivity(IReadOnlyList<Pattern.Core.Model.Pattern> list)
    {
        static string BadgeCss(string status) => status switch
        {
            "Graded" => "badge-graded",
            "Done" => "badge-done",
            "InProgress" => "badge-review",
            "Pending" => "badge-export",
            _ => "badge-draft",
        };

        return list
            .OrderByDescending(p => p.Date, StringComparer.Ordinal)
            .ThenByDescending(p => p.Id)
            .Take(6)
            .Select(p =>
            {
                var life = string.IsNullOrWhiteSpace(p.LifecycleStatus) ? "" : $" · {p.LifecycleStatus}";
                var cert = p.ApprovedForCutting && p.CutterTestPassed ? " · Factory ready" : "";
                return new ActivityItem(
                    p.Status,
                    BadgeCss(p.Status),
                    $"{p.Code} {p.Name}{life}{cert}",
                    p.Date);
            })
            .ToList();
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

public sealed record SetStatusBody(int Id, string Status);
public sealed record SetDueDateBody(int Id, string? Date);
public sealed record SetLifecycleBody(int Id, string LifecycleStatus);
public sealed record UpdateStyleSheetBody(int Id, string? Season, string? Owner, string? Designer);