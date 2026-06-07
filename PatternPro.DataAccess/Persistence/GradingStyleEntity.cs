namespace PatternPro.DataAccess.Persistence;

public class GradingStyleEntity
{
    public string StyleKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ICollection<GradingRowEntity> Rows { get; set; } = [];
}
