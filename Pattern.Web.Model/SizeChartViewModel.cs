namespace Pattern.Web.Model;

public class SizeChartViewModel
{
    public IReadOnlyList<SizeRowViewModel> Rows { get; set; } = [];
}

public class SizeRowViewModel
{
    public string MeasurementPoint { get; set; } = string.Empty;
    public decimal XS { get; set; }
    public decimal S  { get; set; }
    public decimal M  { get; set; }
    public decimal L  { get; set; }
    public decimal XL { get; set; }
    public decimal XXL { get; set; }
}
