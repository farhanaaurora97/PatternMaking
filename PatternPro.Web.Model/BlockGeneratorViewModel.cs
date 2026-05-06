namespace Pattern.Web.Model;

public class BlockGeneratorViewModel
{
    public string StyleKey { get; set; } = "skinny";
    public string StyleLabel { get; set; } = "Skinny Fit";
    public string FitProfile { get; set; } = string.Empty;
    public Dictionary<string, decimal> EaseValues { get; set; } = new();
    public IReadOnlyList<DraftingFormulaViewModel> Formulas { get; set; } = [];
}

public class DraftingFormulaViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Equation { get; set; } = string.Empty;
}
