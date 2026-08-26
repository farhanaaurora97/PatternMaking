using System.Globalization;
using Pattern.Web.Model;
namespace Pattern.Web.Controllers;

/// <summary>Extension to map Pattern domain model → PatternViewModel.</summary>
internal static class PatternViewModelExtensions
{
    private static string InferCategory(Pattern.Core.Model.Pattern p)
    {
        if (!string.IsNullOrWhiteSpace(p.Category))
            return p.Category;

        var code = p.Code;
        if (code.StartsWith("DN", StringComparison.OrdinalIgnoreCase)) return "Denim";
        if (code.StartsWith("CH", StringComparison.OrdinalIgnoreCase)) return "Chinos";
        if (code.StartsWith("TR", StringComparison.OrdinalIgnoreCase)) return "Trousers";
        if (code.StartsWith("CG", StringComparison.OrdinalIgnoreCase)) return "Cargo";
        if (code.StartsWith("JG", StringComparison.OrdinalIgnoreCase)) return "Joggers";
        if (code.StartsWith("LN", StringComparison.OrdinalIgnoreCase)) return "Linen";
        if (code.StartsWith("LE", StringComparison.OrdinalIgnoreCase)) return "Leather";
        if (code.StartsWith("PA", StringComparison.OrdinalIgnoreCase)) return "Palazzo";
        if (code.StartsWith("SH", StringComparison.OrdinalIgnoreCase)) return "Shorts";
        if (code.StartsWith("SW", StringComparison.OrdinalIgnoreCase)) return "Sweatpants";
        if (code.StartsWith("CD", StringComparison.OrdinalIgnoreCase)) return "Corduroy";
        if (code.StartsWith("DR", StringComparison.OrdinalIgnoreCase)) return "Dress";
        if (code.StartsWith("WK", StringComparison.OrdinalIgnoreCase)) return "Workwear";
        return "Denim";
    }

    private static readonly Dictionary<string, string> _styleKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Skinny"]   = "skinny",
        ["Slim"]     = "slim",
        ["Straight"] = "straight",
        ["Bootcut"]  = "bootcut",
        ["Wide Leg"] = "wideLeg",
    };

    private static readonly Dictionary<string, string> _statusLabels = new()
    {
        ["Pending"]    = "Pending",
        ["Draft"]      = "Draft",
        ["InProgress"] = "In Progress",
        ["Graded"]     = "Graded",
        ["Done"]       = "Done",
    };

    private static readonly Dictionary<string, string> _lifecycleLabels = new(StringComparer.Ordinal)
    {
        [Pattern.Core.Model.StyleLifecycle.Idea]       = "Idea",
        [Pattern.Core.Model.StyleLifecycle.Sampling]   = "Sampling",
        [Pattern.Core.Model.StyleLifecycle.Bulk]       = "Bulk",
        [Pattern.Core.Model.StyleLifecycle.Cancelled]  = "Cancelled",
    };

    public static PatternViewModel ToViewModel(this Pattern.Core.Model.Pattern p)
    {
        var lifecycle = Pattern.Core.Model.StyleLifecycle.IsValid(p.LifecycleStatus)
            ? p.LifecycleStatus
            : Pattern.Core.Model.StyleLifecycle.InferFromPatternStatus(p.Status);
        return new PatternViewModel
    {
        Id          = p.Id,
        Code        = p.Code,
        Name        = p.Name,
        Style       = p.Style,
        StyleKey    = _styleKeys.TryGetValue(p.Style, out var sk)
            ? sk
            : Pattern.Core.Model.StyleOptionCatalog.StyleKeyFromDisplayLabel(p.Style),
        BaseSize    = p.BaseSize,
        PieceCount  = p.PieceCount,
        Status      = p.Status,
        StatusLabel = _statusLabels.GetValueOrDefault(p.Status, p.Status),
        Date             = p.Date,
        Season           = string.IsNullOrWhiteSpace(p.Season) ? Pattern.Core.Model.StyleLifecycle.DefaultSeason() : p.Season,
        Owner            = string.IsNullOrWhiteSpace(p.Owner) ? (p.Designer ?? "Unassigned") : p.Owner,
        Designer         = p.Designer ?? string.Empty,
        LifecycleStatus  = lifecycle,
        LifecycleLabel   = _lifecycleLabels.GetValueOrDefault(lifecycle, lifecycle),
        Revision         = string.IsNullOrWhiteSpace(p.Revision) ? "Proto-1" : p.Revision,
        DueDateLabel = p.DueDate?.ToString("MMM d", CultureInfo.InvariantCulture) ?? "—",
        DueDateIso   = p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
        Category    = InferCategory(p),
        ApprovedForCutting = p.ApprovedForCutting,
        CutterTestPassed   = p.CutterTestPassed,
        IsProductionCertified = p.ApprovedForCutting && p.CutterTestPassed,
        ProductionBadgeLabel = GetProductionBadgeLabel(p),
        ProductionBadgeCss   = GetProductionBadgeCss(p),
    };
    }

    private static string GetProductionBadgeLabel(Pattern.Core.Model.Pattern p)
    {
        if (p.ApprovedForCutting && p.CutterTestPassed) return "Factory ready";
        if (p.ApprovedForCutting) return "Approved";
        if (p.CutterTestPassed) return "Cutter OK";
        return string.Empty;
    }

    private static string GetProductionBadgeCss(Pattern.Core.Model.Pattern p)
    {
        if (p.ApprovedForCutting && p.CutterTestPassed) return "tag-green";
        if (p.ApprovedForCutting || p.CutterTestPassed) return "tag-gold";
        return "tag-purple";
    }
}
