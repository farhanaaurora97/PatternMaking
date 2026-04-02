namespace Pattern.Web.Model;

public class GradingViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public string StyleLabel { get; set; } = "Skinny Fit";
    public IReadOnlyList<GradingRowViewModel> Rows { get; set; } = [];
}

public class GradingRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;
    public decimal XS  { get; set; }
    public decimal S   { get; set; }
    public decimal L   { get; set; }
    public decimal XL  { get; set; }
    public decimal XXL { get; set; }

    public string FormatDelta(decimal v) =>
        v == 0 ? "0" : v > 0 ? $"+{v}" : v.ToString("0.##");
}
