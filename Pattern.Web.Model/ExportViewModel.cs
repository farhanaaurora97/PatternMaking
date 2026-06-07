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
        new("DXF", "📐", "Industry standard CAD format. Compatible with Gerber, Lectra, Optitex & all major CAM systems.", true),
        new("PDF", "📄", "Print-ready PDF at 1:1 scale. Multiple page sizes supported. Good for sampling & approval.", false),
        new("SVG", "🎨", "Vector format for digital use. Edit in Illustrator, Inkscape or any vector editor.", false),
    ];
}

public record ExportFormatViewModel(string Name, string Icon, string Description, bool Recommended);
