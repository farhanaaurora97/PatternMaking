namespace Pattern.Core.Model;

/// <summary>Resolved size chart for global or per-style scope.</summary>
public sealed class SizeChartSnapshot
{
    public int? PatternId { get; init; }
    public string? PatternCode { get; init; }
    public string ChartMode { get; init; } = MeasurementChartMode.Body;
    public bool UseCustomChart { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<SizeRow> Rows { get; init; } = [];

    public bool IsGlobalScope => PatternId is null || !UseCustomChart;

    public string ScopeLabel => PatternId is null
        ? "Global (all styles)"
        : UseCustomChart
            ? $"Custom — {PatternCode ?? $"#{PatternId}"}"
            : $"Global + {PatternCode ?? $"#{PatternId}"} settings";
}
