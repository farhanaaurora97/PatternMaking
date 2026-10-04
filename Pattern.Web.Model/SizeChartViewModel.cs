namespace Pattern.Web.Model;

public class SizeChartViewModel
{
    public IReadOnlyList<string> ColumnLabels { get; set; } = [];

    public IReadOnlyList<SizeRowViewModel> Rows { get; set; } = [];

    public string ScopeLabel { get; set; } = "Global (all styles)";

    public int SizeCount => ColumnLabels.Count;

    public int MeasurementCount => Rows.Count;
}

public class SizeRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;

    public decimal ToleranceCm { get; set; }

    public string MeasurementMethod { get; set; } = string.Empty;

    public IReadOnlyList<decimal> Values { get; set; } = [];
}
