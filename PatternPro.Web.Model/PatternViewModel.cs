namespace Pattern.Web.Model;

public class PatternViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Style    { get; set; } = string.Empty;
    /// <summary>Lowercase fit key used in route params (e.g. "skinny", "wideLeg").</summary>
    public string StyleKey { get; set; } = "skinny";
    public string BaseSize { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusCssClass => $"st-{Status}";
    public string Date { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Designer { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public string LifecycleLabel { get; set; } = string.Empty;
    public string LifecycleCssClass => $"lc-{LifecycleStatus}";
    public string Revision { get; set; } = "Proto-1";
    /// <summary>Short due date for table (e.g. Apr 5) or em dash.</summary>
    public string DueDateLabel { get; set; } = "—";
    /// <summary>ISO date for the date input value (yyyy-MM-dd) or empty string when no due date.</summary>
    public string DueDateIso { get; set; } = string.Empty;
    /// <summary>Product line for dashboard category tabs (e.g. Denim).</summary>
    public string Category { get; set; } = "Denim";

    public bool ApprovedForCutting { get; set; }
    public bool CutterTestPassed { get; set; }
    /// <summary>True when approved and cutter test passed (factory-ready).</summary>
    public bool IsProductionCertified { get; set; }
    /// <summary>Short badge for dashboard table; empty when not applicable.</summary>
    public string ProductionBadgeLabel { get; set; } = string.Empty;
    public string ProductionBadgeCss { get; set; } = "tag-gold";

    public string DisplayName => $"{Code} {Name}";
}
