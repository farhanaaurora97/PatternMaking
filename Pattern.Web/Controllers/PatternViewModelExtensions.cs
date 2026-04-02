using Pattern.Web.Model;
namespace Pattern.Web.Controllers;

/// <summary>Extension to map Pattern domain model → PatternViewModel.</summary>
internal static class PatternViewModelExtensions
{
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
        BaseSize    = p.BaseSize,
        PieceCount  = p.PieceCount,
        Status      = p.Status,
        StatusLabel = _statusLabels.GetValueOrDefault(p.Status, p.Status),
        Date        = p.Date,
    };
}
