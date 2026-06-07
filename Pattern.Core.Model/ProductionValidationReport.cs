namespace Pattern.Core.Model;

public sealed class ProductionValidationReport
{
    public int PatternId { get; set; }
    public string StyleKey { get; set; } = string.Empty;
    public bool CanExportToFactory { get; set; }
    public bool ApprovedForCutting { get; set; }
    public bool CutterTestPassed { get; set; }
    public IReadOnlyList<ProductionValidationIssue> Issues { get; set; } = [];
    public IReadOnlyList<ProductionValidationIssue> Warnings { get; set; } = [];
}

public sealed class ProductionValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
