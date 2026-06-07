namespace PatternPro.DataAccess.Persistence;

public class SizeChartValueEntity
{
    public int RowId { get; set; }
    public int ColumnIndex { get; set; }
    public decimal Value { get; set; }
    public SizeChartRowEntity Row { get; set; } = null!;
}
