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

    public static PatternViewModel ToViewModel(this Pattern.Core.Model.Pattern p) => new()
    {
        Id          = p.Id,
        Code        = p.Code,
        Name        = p.Name,
        Style       = p.Style,
        StyleKey    = _styleKeys.TryGetValue(p.Style, out var sk) ? sk : p.Style.ToLower(),
        BaseSize    = p.BaseSize,
        PieceCount  = p.PieceCount,
        Status      = p.Status,
        StatusLabel = _statusLabels.GetValueOrDefault(p.Status, p.Status),
        Date        = p.Date,
        DueDateLabel = p.DueDate?.ToString("MMM d", CultureInfo.InvariantCulture) ?? "—",
        DueDateIso   = p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
        Category    = InferCategory(p),
    };
}
