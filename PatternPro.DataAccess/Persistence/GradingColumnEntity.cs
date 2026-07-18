namespace PatternPro.DataAccess.Persistence;

public class GradingColumnEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;
}
