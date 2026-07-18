namespace PatternPro.DataAccess.Persistence;

public class SizeChartRowEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string MeasurementPoint { get; set; } = string.Empty;
    public decimal ToleranceCm { get; set; }
    public string MeasurementMethod { get; set; } = string.Empty;
    public ICollection<SizeChartValueEntity> Values { get; set; } = [];
}
