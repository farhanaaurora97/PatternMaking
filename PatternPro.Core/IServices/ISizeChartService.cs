using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface ISizeChartService
{
    IReadOnlyList<string> GetColumnLabels();

    IReadOnlyList<SizeRow> GetAll();

    string ExportCsv();

    /// <summary>Appends a size column; new cells extrapolate from the last two grades per row.</summary>
    (bool Ok, string? Error) TryAddSizeColumn(string label);

    /// <summary>Adds a measurement row; values are copied from an existing row’s grade when provided.</summary>
    (bool Ok, string? Error) TryAddMeasurementRow(string measurementPoint, string? copyFromMeasurementPoint);

    (bool Ok, string? Error) TryUpdateCell(string measurementPoint, int columnIndex, decimal value);

    (bool Ok, string? Error) TryUpdateRowMeta(string measurementPoint, decimal toleranceCm, string? measurementMethod);

    IReadOnlyList<MeasurementProfile> GetMeasurementProfiles();
    (bool Ok, string? Error) SaveMeasurementProfile(string name, IReadOnlyDictionary<string, decimal> measurements);
}

