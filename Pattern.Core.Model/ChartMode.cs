namespace Pattern.Core.Model;

/// <summary>How size-chart values are interpreted when drafting patterns.</summary>
public static class MeasurementChartMode
{
    /// <summary>Body measurements; Block Generator ease is applied when drafting.</summary>
    public const string Body = "Body";

    /// <summary>Finished garment POM values; ease is not applied when drafting.</summary>
    public const string Garment = "Garment";

    public static bool IsGarment(string? mode) =>
        Garment.Equals(mode?.Trim(), StringComparison.OrdinalIgnoreCase);
}
