namespace Pattern.Web.Model;

public class DashboardViewModel
{
    public IReadOnlyList<PatternViewModel> Patterns { get; set; } = [];
    public PatternCreateViewModel CreateForm { get; set; } = new();
    public string CurrentStyle { get; set; } = "skinny";
    public string CurrentStyleLabel { get; set; } = "Skinny Fit";

    // Stats
    public int ActivePatternCount { get; set; }
    public int CompletionPercent { get; set; }
    public int PieceCount { get; set; }

    // Style progress (0–100 per style)
    public Dictionary<string, int> StyleProgress { get; set; } = new();

    // Recent activity feed
    public IReadOnlyList<ActivityItem> RecentActivity { get; set; } = [];

    // Pending/draft count for sidebar badge
    public int PendingCount { get; set; }
}

public record ActivityItem(string Badge, string BadgeCss, string Text, string TimeAgo);
