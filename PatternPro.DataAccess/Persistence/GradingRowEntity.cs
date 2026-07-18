namespace PatternPro.DataAccess.Persistence;

public class GradingRowEntity
{
    public int Id { get; set; }
    public string StyleKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string MeasurementPoint { get; set; } = string.Empty;
    public int BaseIndex { get; set; }
    public GradingStyleEntity Style { get; set; } = null!;
    public ICollection<GradingDeltaEntity> Deltas { get; set; } = [];
}
