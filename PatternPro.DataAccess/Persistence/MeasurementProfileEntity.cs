namespace PatternPro.DataAccess.Persistence;

public class MeasurementProfileEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<MeasurementProfileValueEntity> Values { get; set; } = [];
}
