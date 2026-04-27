namespace Pattern.Web.Model;

public class SizeChartViewModel
{
    public IReadOnlyList<string> ColumnLabels { get; set; } = [];

    public IReadOnlyList<SizeRowViewModel> Rows { get; set; } = [];
}

public class SizeRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;

    public IReadOnlyList<decimal> Values { get; set; } = [];
}
