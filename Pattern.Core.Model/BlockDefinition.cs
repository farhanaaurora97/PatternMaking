namespace Pattern.Core.Model;

public class BlockDefinition
{
    public string                    Label       { get; set; } = string.Empty;
    public string                    FitProfile  { get; set; } = string.Empty;
    public Dictionary<string,decimal> DefaultEase { get; set; } = new();
    public List<EaseFormula>         Formulas    { get; set; } = [];
}