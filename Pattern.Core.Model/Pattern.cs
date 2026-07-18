namespace Pattern.Core.Model;

/// <summary>One bottom-wear pattern row (dashboard / CRUD).</summary>
public class Pattern
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    /// <summary>PLM revision label (e.g. Proto-1, SMS-2, Bulk-1).</summary>
    public string Revision { get; set; } = "Proto-1";
    public string Name { get; set; } = string.Empty;
    /// <summary>Stretch fabric factor (%) applied when drafting from block ease.</summary>
    public decimal FabricStretchPercent { get; set; }
    public string Style { get; set; } = string.Empty;
    public string BaseSize { get; set; } = string.Empty;
    public int PieceCount { get; set; }
    public string Status { get; set; } = "Draft";
    public string Date { get; set; } = string.Empty;
    public string Designer { get; set; } = string.Empty;

    /// <summary>PLM season (e.g. SS26, FW25).</summary>
    public string Season { get; set; } = string.Empty;

    /// <summary>Merchandiser or style owner (who owns the PLM row).</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>PLM lifecycle: Idea → Sampling → Bulk → Cancelled (<see cref="StyleLifecycle"/>).</summary>
    public string LifecycleStatus { get; set; } = StyleLifecycle.Idea;

    /// <summary>Product line / pant type (e.g. Denim, Chinos, Trousers).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>When the pattern row was created (used for week-over-week stats).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional collection / milestone deadline.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Design signed off for factory cutting (2D export allowed when QC + cutter test pass).</summary>
    public bool ApprovedForCutting { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>Physical plotter/cutter trial succeeded on factory equipment.</summary>
    public bool CutterTestPassed { get; set; }

    public DateTime? CutterTestedAt { get; set; }
    public string CutterTestedBy { get; set; } = string.Empty;
    public string CutterTestNotes { get; set; } = string.Empty;

    /// <summary>Optional CLO3D drape review completed (informational).</summary>
    public bool CloReviewCompleted { get; set; }

    public string CloReviewNotes { get; set; } = string.Empty;

    /// <summary>Wash/shrink allowance applied on factory export (percent).</summary>
    public decimal ShrinkagePercent { get; set; }

    /// <summary>Body vs finished garment chart interpretation.</summary>
    public string ChartMode { get; set; } = MeasurementChartMode.Body;

    /// <summary>When true, this style uses its own size-chart table instead of the global chart.</summary>
    public bool UseCustomSizeChart { get; set; }
}
