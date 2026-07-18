namespace PatternPro.DataAccess.Persistence;

public class MeasurementProfileValueEntity
{
    public int ProfileId { get; set; }
    public string MeasurementPoint { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public MeasurementProfileEntity Profile { get; set; } = null!;
}
