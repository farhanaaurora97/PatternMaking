namespace Pattern.Core.Model;

/// <summary>
/// One grading row. <see cref="Deltas"/> order matches the service's column labels,
/// with the M base-size column always present at index <see cref="BaseIndex"/> (delta = 0).
/// </summary>
public class GradingRow
{
    public string MeasurementPoint { get; set; } = string.Empty;

    /// <summary>Delta values ordered left-to-right across size columns (including M = 0).</summary>
    public List<double> Deltas { get; set; } = [];

    /// <summary>Index of the M (base) size within <see cref="Deltas"/>.</summary>
    public int BaseIndex { get; set; }
}