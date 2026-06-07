namespace Pattern.Core.Model;

/// <summary>One measurement row; <see cref="Values"/> order matches the chart’s size columns (left to right).</summary>
public class SizeRow
{
    public string MeasurementPoint { get; set; } = string.Empty;

  /// <summary>Spec tolerance ± cm (industry POM tolerance).</summary>
    public decimal ToleranceCm { get; set; }

    /// <summary>How to measure (e.g. flat, circumference, HPS).</summary>
    public string MeasurementMethod { get; set; } = string.Empty;

    public List<decimal> Values { get; set; } = [];
}
