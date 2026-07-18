namespace Pattern.Web.Model;

public class GradingViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public string StyleLabel { get; set; } = "Skinny Fit";
    public IReadOnlyList<string> ColumnLabels { get; set; } = [];
    public int BaseIndex { get; set; }
    public IReadOnlyList<GradingRowViewModel> Rows { get; set; } = [];
}

public class GradingRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;
    public IReadOnlyList<double> Deltas { get; set; } = [];
    public int BaseIndex { get; set; }

    public string FormatDelta(double v) =>
        v == 0 ? "0" : v > 0 ? $"+{v:0.##}" : v.ToString("0.##");
}
