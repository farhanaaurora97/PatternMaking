using System.Globalization;
using Pattern.Core.Model;
using Pattern.Web.Model;
using PatternEntity = Pattern.Core.Model.Pattern;

namespace PatternPro.Desktop.Mapping;

internal static class PatternMapper
{
    private static readonly Dictionary<string, string> StyleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Skinny"] = "skinny",
        ["Slim"] = "slim",
        ["Straight"] = "straight",
        ["Bootcut"] = "bootcut",
        ["Wide Leg"] = "wideLeg",
    };

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["Pending"] = "Pending",
        ["Draft"] = "Draft",
        ["InProgress"] = "In Progress",
        ["Graded"] = "Graded",
        ["Done"] = "Done",
    };

    private static readonly Dictionary<string, string> LifecycleLabels = new(StringComparer.Ordinal)
    {
        [StyleLifecycle.Idea] = "Idea",
        [StyleLifecycle.Sampling] = "Sampling",
        [StyleLifecycle.Bulk] = "Bulk",
        [StyleLifecycle.Cancelled] = "Cancelled",
    };

    public static PatternViewModel ToViewModel(this PatternEntity p)
    {
        var lifecycle = StyleLifecycle.IsValid(p.LifecycleStatus)
            ? p.LifecycleStatus
            : StyleLifecycle.InferFromPatternStatus(p.Status);

        return new PatternViewModel
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Style = p.Style,
            StyleKey = StyleKeys.TryGetValue(p.Style, out var sk)
                ? sk
                : StyleOptionCatalog.StyleKeyFromDisplayLabel(p.Style),
            BaseSize = p.BaseSize,
            PieceCount = p.PieceCount,
            Status = p.Status,
            StatusLabel = StatusLabels.GetValueOrDefault(p.Status, p.Status),
            Date = p.Date,
            Season = string.IsNullOrWhiteSpace(p.Season) ? StyleLifecycle.DefaultSeason() : p.Season,
            Owner = string.IsNullOrWhiteSpace(p.Owner) ? (p.Designer ?? "Unassigned") : p.Owner,
            Designer = p.Designer ?? string.Empty,
            LifecycleStatus = lifecycle,
            LifecycleLabel = LifecycleLabels.GetValueOrDefault(lifecycle, lifecycle),
            Revision = string.IsNullOrWhiteSpace(p.Revision) ? "Proto-1" : p.Revision,
            DueDateLabel = p.DueDate?.ToString("MMM d", CultureInfo.InvariantCulture) ?? "—",
            DueDateIso = p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            Category = InferCategory(p),
            ApprovedForCutting = p.ApprovedForCutting,
            CutterTestPassed = p.CutterTestPassed,
            IsProductionCertified = p.ApprovedForCutting && p.CutterTestPassed,
            ProductionBadgeLabel = GetProductionBadgeLabel(p),
            ProductionBadgeCss = GetProductionBadgeCss(p),
        };
    }

    private static string InferCategory(PatternEntity p)
    {
        if (!string.IsNullOrWhiteSpace(p.Category))
            return p.Category;

        var code = p.Code;
        if (code.StartsWith("DN", StringComparison.OrdinalIgnoreCase)) return "Denim";
        if (code.StartsWith("CH", StringComparison.OrdinalIgnoreCase)) return "Chinos";
        if (code.StartsWith("TR", StringComparison.OrdinalIgnoreCase)) return "Trousers";
        if (code.StartsWith("CG", StringComparison.OrdinalIgnoreCase)) return "Cargo";
        if (code.StartsWith("JG", StringComparison.OrdinalIgnoreCase)) return "Joggers";
        return "Denim";
    }

    private static string GetProductionBadgeLabel(PatternEntity p)
    {
        if (p.ApprovedForCutting && p.CutterTestPassed) return "Factory ready";
        if (p.ApprovedForCutting) return "Approved";
        if (p.CutterTestPassed) return "Cutter OK";
        return string.Empty;
    }

    private static string GetProductionBadgeCss(PatternEntity p)
    {
        if (p.ApprovedForCutting && p.CutterTestPassed) return "tag-green";
        if (p.ApprovedForCutting || p.CutterTestPassed) return "tag-gold";
        return "tag-purple";
    }
}
