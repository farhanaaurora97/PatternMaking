namespace Pattern.Core.Model;

/// <summary>PLM style-row lifecycle (separate from pattern-room workflow status).</summary>
public static class StyleLifecycle
{
    public const string Idea = "Idea";
    public const string Sampling = "Sampling";
    public const string Bulk = "Bulk";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Idea, Sampling, Bulk, Cancelled];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value, StringComparer.Ordinal);

    /// <summary>Default season label from calendar (e.g. SS26, FW25).</summary>
    public static string DefaultSeason(DateTime? at = null)
    {
        var d = at ?? DateTime.Today;
        var yy = d.Year % 100;
        return d.Month <= 6 ? $"SS{yy:D2}" : $"FW{yy:D2}";
    }

    /// <summary>Backfill when legacy rows have no lifecycle stored.</summary>
    public static string InferFromPatternStatus(string patternStatus) => patternStatus switch
    {
        "Done" => Bulk,
        "Graded" => Sampling,
        "Cancelled" => Cancelled,
        _ => Idea
    };
}
