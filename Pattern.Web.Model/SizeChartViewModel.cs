namespace Pattern.Web.Model;

public class SizeChartViewModel
{
    public IReadOnlyList<string> ColumnLabels { get; set; } = [];

    public IReadOnlyList<SizeRowViewModel> Rows { get; set; } = [];

    public string ScopeLabel { get; set; } = "Global (all styles)";

    public int SelectedPatternId { get; set; }

    public string ChartMode { get; set; } = "Body";

    public bool UseCustomChart { get; set; }

    public string BaseSizeLabel { get; set; } = "M";

    public IReadOnlyList<SizeChartPatternOption> Patterns { get; set; } = [];

    public bool IsPerStyleScope => SelectedPatternId > 0;

    public int SizeCount => ColumnLabels.Count;

    public int MeasurementCount => Rows.Count;
}

public class SizeChartPatternOption
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Season { get; set; } = string.Empty;

    public string BaseSize { get; set; } = string.Empty;
}

public class SizeRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;

    public decimal ToleranceCm { get; set; }

    public string MeasurementMethod { get; set; } = string.Empty;

    public IReadOnlyList<decimal> Values { get; set; } = [];
}
