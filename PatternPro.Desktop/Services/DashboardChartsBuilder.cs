using Pattern.Core.Model;
using Pattern.Web.Model;
using PatternEntity = Pattern.Core.Model.Pattern;

namespace PatternPro.Desktop.Services;

internal static class DashboardChartsBuilder
{
    public static DashboardChartsModel Build(IReadOnlyList<PatternEntity> list)
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
}
