namespace Pattern.Web.Model;

/// <summary>Serialized to JSON for Chart.js on the dashboard.</summary>
public sealed class DashboardChartsModel
{
    public IReadOnlyList<ChartStatusSlice> Status { get; init; } = [];
    /// <summary>Stacked horizontal bar: each fit (silhouette) split by workflow status.</summary>
    public ChartStackedByFit StylesByFit { get; init; } = new();
    public IReadOnlyList<ChartStyleBar> PantTypes { get; init; } = [];
}

public sealed record ChartStatusSlice(string Key, string Label, int Count, string Color);

public sealed record ChartStyleBar(string Label, int Count);

public sealed class ChartStackedByFit
{
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<ChartStackDataset> Datasets { get; init; } = [];
}

public sealed class ChartStackDataset
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public IReadOnlyList<int> Data { get; init; } = [];
    public string BackgroundColor { get; init; } = "#64748b";
}

public class DashboardViewModel
{
    public IReadOnlyList<PatternViewModel> Patterns { get; set; } = [];
    public PatternCreateViewModel CreateForm { get; set; } = new();
    public string CurrentStyle { get; set; } = "skinny";
    public string CurrentStyleLabel { get; set; } = "Skinny Fit";

    // Stats
    public int ActivePatternCount { get; set; }
    /// <summary>Percent of patterns that are not drafts (for stat bar).</summary>
    public int ActiveBarPercent { get; set; }
    public int TotalPatternCount { get; set; }
    public int AvgPieceCount { get; set; }
    /// <summary>Graded size steps shown on dashboard (e.g. XS–XXL).</summary>
    public int GradedSizesCount { get; set; } = 6;
    public int CompletionPercent { get; set; }
    public int PieceCount { get; set; }

    /// <summary>Patterns created in the current calendar week (Mon start).</summary>
    public int PatternsCreatedThisWeek { get; set; }
    /// <summary>Patterns created in the previous calendar week.</summary>
    public int PatternsCreatedLastWeek { get; set; }
    /// <summary>Net change: this week count minus last week (for stat card copy).</summary>
    public int WeekOverWeekDelta => PatternsCreatedThisWeek - PatternsCreatedLastWeek;

    /// <summary>Patterns with a due date falling in the current week.</summary>
    public int DueThisWeekCount { get; set; }

    // Style progress (0–100 per style)
    public Dictionary<string, int> StyleProgress { get; set; } = new();

    // Recent activity feed
    public IReadOnlyList<ActivityItem> RecentActivity { get; set; } = [];

    // Pending/draft count for sidebar badge
    public int PendingCount { get; set; }

    public int ProductionCertifiedCount { get; set; }

    /// <summary>Category tab labels including leading "All".</summary>
    public IReadOnlyList<string> CategoryTabs { get; set; } = [];

    /// <summary>Analytics chart datasets (status mix, patterns by style).</summary>
    public DashboardChartsModel Charts { get; set; } = new();
}

public record ActivityItem(string Badge, string BadgeCss, string Text, string TimeAgo);
