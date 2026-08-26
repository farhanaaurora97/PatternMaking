namespace Pattern.Web.Model;

public class ExportViewModel
{
    public int PatternId { get; set; }
    public string PatternDisplayName { get; set; } = "DN-001 Skinny Classic";
    public string StyleLabel { get; set; } = "Skinny Fit";
    public int PieceCount { get; set; } = 9;
    public int SizeCount { get; set; } = 6;
    public string SizesCsv { get; set; } = "XS,S,M,L,XL,XXL";
    public int TotalFiles => PieceCount * SizeCount;
    public string SelectedFormat { get; set; } = "DXF";

    /// <summary>When set, canvas ZIP exports one graded file per size using this pattern row base size as the edited master.</summary>
    public string? CanvasGradeBaseSize { get; set; }

    public bool ApprovedForCutting { get; set; }
    public bool CutterTestPassed { get; set; }
    public bool CanExportToFactory { get; set; }
    public string? ApprovedBy { get; set; }
    public string? CutterTestedBy { get; set; }
    public decimal ShrinkagePercent { get; set; }

    public static IReadOnlyList<ExportFormatViewModel> Formats =>
    [
        new("DXF", "📐", "AAMA-style DXF for Optitex 24. Units: centimeters (cm). Closed polylines, one block per piece.", true),
        new("HPGL", "🖨", "Hewlett-Packard Graphics Language for plotters and many CAM cutters. Pen layers: CUT, SA, GRAIN, NOTCH.", false),
        new("PLT", "✂", "HPGL command stream with .plt extension for legacy plotter and cutter drivers.", false),
        new("PDF", "🖨", "One PDF per piece — print paper patterns (mm scale). Open in Reader and print at 100%.", false),
    ];
}

public record ExportFormatViewModel(string Name, string Icon, string Description, bool Recommended);
