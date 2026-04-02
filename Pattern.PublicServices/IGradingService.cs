using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

public interface IGradingService
{
    IReadOnlyList<GradingRow> GetGradingTable(string styleKey);
    string                    GetStyleLabel(string styleKey);
    string                    ExportCsv(string styleKey);
}