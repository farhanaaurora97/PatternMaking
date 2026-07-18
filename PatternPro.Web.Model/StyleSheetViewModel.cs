namespace Pattern.Web.Model;

public class StyleSheetViewModel
{
    public IReadOnlyList<PatternViewModel> Rows { get; set; } = [];
    public int TotalCount { get; set; }
    public IReadOnlyList<string> LifecycleTabs { get; set; } =
        ["All", Pattern.Core.Model.StyleLifecycle.Idea, Pattern.Core.Model.StyleLifecycle.Sampling,
         Pattern.Core.Model.StyleLifecycle.Bulk, Pattern.Core.Model.StyleLifecycle.Cancelled];
    public IReadOnlyList<string> SeasonOptions { get; set; } = [];
}

public static class StyleLifecycleOptions
{
    public static readonly (string Value, string Label)[] All =
    [
        (Pattern.Core.Model.StyleLifecycle.Idea, "Idea"),
        (Pattern.Core.Model.StyleLifecycle.Sampling, "Sampling"),
        (Pattern.Core.Model.StyleLifecycle.Bulk, "Bulk"),
        (Pattern.Core.Model.StyleLifecycle.Cancelled, "Cancelled"),
    ];
}
