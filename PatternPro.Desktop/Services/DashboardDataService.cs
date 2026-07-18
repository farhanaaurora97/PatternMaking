using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Desktop.Mapping;
using Pattern.Web.Model;
using PatternEntity = Pattern.Core.Model.Pattern;

namespace PatternPro.Desktop.Services;

public sealed class DashboardDataService(IPatternService patternService)
{
    public DashboardViewModel Load(string style = "skinny")
    {
        var patterns = patternService.GetAll();
        var styleDef = patternService.GetStyleDefinition(style);
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

        return new DashboardViewModel
        {
            CurrentStyle = style,
            CurrentStyleLabel = styleDef.Label,
            PieceCount = styleDef.PieceCount,
            Patterns = patternVms,
            TotalPatternCount = total,
            ActivePatternCount = activeNonDraft,
            ActiveBarPercent = total == 0 ? 0 : (int)Math.Round(activeNonDraft * 100.0 / total),
            AvgPieceCount = total == 0 ? 0 : (int)Math.Round(list.Average(p => p.PieceCount)),
            GradedSizesCount = 6,
            CompletionPercent = total == 0 ? 0
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
            Charts = DashboardChartsBuilder.Build(list),
        };
    }

    private static Dictionary<string, int> BuildStyleProgress(IEnumerable<PatternEntity> patterns)
    {
        var styles = new[] { "Skinny", "Slim", "Straight", "Bootcut", "Wide Leg" };
        var defaults = new[] { 90, 75, 60, 40, 20 };
        var result = new Dictionary<string, int>();

        for (var i = 0; i < styles.Length; i++)
        {
            var styleList = patterns.Where(p => p.Style == styles[i]).ToList();
            var done = styleList.Count(p => p.Status is "Graded" or "Done");
            result[styles[i]] = styleList.Count == 0 ? defaults[i]
                : (int)Math.Round(done * 100.0 / styleList.Count);
        }
        return result;
    }

    private static DateTime StartOfWeekMonday(DateTime date)
    {
        var d = date.Date;
        var diff = d.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - d.DayOfWeek;
        return d.AddDays(diff);
    }

    private static IReadOnlyList<ActivityItem> BuildRecentActivity(IReadOnlyList<PatternEntity> list)
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
}
