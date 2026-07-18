namespace PatternPro.DataAccess.Persistence;

public class SizeChartColumnEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;
    public ICollection<SizeChartValueEntity> Values { get; set; } = [];
}
