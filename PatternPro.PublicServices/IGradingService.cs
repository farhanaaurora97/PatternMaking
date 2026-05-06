using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

public interface IGradingService
{
    IReadOnlyList<string>     GetColumnLabels();
    IReadOnlyList<GradingRow> GetGradingTable(string styleKey);
    string                    GetStyleLabel(string styleKey);
    string                    ExportCsv(string styleKey);

    /// <summary>
    /// Appends a new size column to every fit's grading table, extrapolating
    /// the delta from each row's last two existing deltas.
    /// </summary>
    void AddColumn(string label);

    /// <summary>
    /// Adds a new measurement row to a specific fit's grading table.
    /// Deltas can be seeded by copying from an existing row, or left as zeros.
    /// </summary>
    (bool Ok, string? Error) AddRow(string styleKey, string measurementPoint, string? copyFromPoint);
}