namespace Pattern.Core.Model;

public class GradingRow
{
    public string  MeasurementPoint { get; set; } = string.Empty;
    public decimal XS  { get; set; }
    public decimal S   { get; set; }
    public decimal L   { get; set; }
    public decimal XL  { get; set; }
    public decimal XXL { get; set; }
}