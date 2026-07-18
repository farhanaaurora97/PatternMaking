namespace PatternPro.DataAccess.Persistence;

public class GradingDeltaEntity
{
    public int RowId { get; set; }
    public int ColumnIndex { get; set; }
    public double Delta { get; set; }
    public GradingRowEntity Row { get; set; } = null!;
}
