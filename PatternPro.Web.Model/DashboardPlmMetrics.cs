using Pattern.Core.Model;
using PatternEntity = Pattern.Core.Model.Pattern;

namespace Pattern.Web.Model;

public static class DashboardPlmMetrics
{
    public static int CountOverdue(IEnumerable<PatternEntity> patterns, DateTime today) =>
        patterns.Count(p => p.DueDate.HasValue && p.DueDate.Value.Date < today.Date);

    public static int CountDueThisWeek(IEnumerable<PatternEntity> patterns, DateTime today)
    {
        var weekStart = StartOfWeekMonday(today);
        var weekEnd = weekStart.AddDays(7);
        return patterns.Count(p => p.DueDate.HasValue && p.DueDate.Value >= weekStart && p.DueDate.Value < weekEnd);
    }

    public static int CountBulkReady(IEnumerable<PatternEntity> patterns) =>
        patterns.Count(p => p.ApprovedForCutting && p.CutterTestPassed
            && !StyleLifecycle.Bulk.Equals(p.LifecycleStatus, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<PlmNudgeItem> BuildBulkNudges(IEnumerable<PatternEntity> patterns) =>
        patterns
            .Where(p => p.ApprovedForCutting && p.CutterTestPassed)
            .Where(p => !StyleLifecycle.Bulk.Equals(p.LifecycleStatus, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(p => new PlmNudgeItem(
                p.Id,
                p.Code,
                p.Name,
                StyleOptionCatalog.StyleKeyFromDisplayLabel(p.Style)))
            .ToList();

    public static DateTime StartOfWeekMonday(DateTime date)
    {
        var d = date.Date;
        var diff = d.DayOfWeek == DayOfWeek.Sunday ? -6 : DayOfWeek.Monday - d.DayOfWeek;
        return d.AddDays(diff);
    }

    public static bool IsDueThisWeek(PatternEntity pattern, DateTime today)
    {
        if (!pattern.DueDate.HasValue) return false;
        var weekStart = StartOfWeekMonday(today);
        var weekEnd = weekStart.AddDays(7);
        var due = pattern.DueDate.Value;
        return due >= weekStart && due < weekEnd;
    }

    public static bool NeedsBulkLifecycle(PatternEntity pattern) =>
        pattern.ApprovedForCutting && pattern.CutterTestPassed
        && !StyleLifecycle.Bulk.Equals(pattern.LifecycleStatus, StringComparison.OrdinalIgnoreCase);
}
