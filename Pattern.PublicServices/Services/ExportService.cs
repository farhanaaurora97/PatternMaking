using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class ExportService : IExportService
{
    public IReadOnlyList<string> GetExportSteps(string format) =>
    [
        "Collecting pattern pieces",
        $"Applying seam allowances",
        $"Generating {format.ToUpper()} geometry",
        "Packaging all sizes (XS-XXL)",
        "Finalising output files",
    ];
}