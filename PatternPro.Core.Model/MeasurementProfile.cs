namespace Pattern.Core.Model;

public class MeasurementProfile
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> Measurements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
