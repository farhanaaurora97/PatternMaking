using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface ISizeChartService
{
    SizeChartSnapshot GetSnapshot(int? patternId = null);

    IReadOnlyList<string> GetColumnLabels(int? patternId = null);

    IReadOnlyList<SizeRow> GetAll(int? patternId = null);

    string ExportCsv(int? patternId = null);

    (bool Ok, string? Error) TryAddSizeColumn(string label, int? patternId = null);

    (bool Ok, string? Error) TryAddMeasurementRow(string measurementPoint, string? copyFromMeasurementPoint, int? patternId = null);

    (bool Ok, string? Error) TryDeleteMeasurementRow(string measurementPoint, int? patternId = null);

    (bool Ok, string? Error) TryDeleteSizeColumn(int columnIndex, int? patternId = null);

    (bool Ok, string? Error) TryUpdateCell(string measurementPoint, int columnIndex, decimal value, int? patternId = null);

    (bool Ok, string? Error) TryUpdateRowMeta(string measurementPoint, decimal toleranceCm, string? measurementMethod, int? patternId = null);

    (bool Ok, string? Error) SetChartSettings(int patternId, bool useCustomChart, string chartMode);

    (bool Ok, string? Error) CopyGlobalToPattern(int patternId);

    (bool Ok, string? Error) InitializeGarmentTemplate(int patternId);

    IReadOnlyList<MeasurementProfile> GetMeasurementProfiles();

    (bool Ok, string? Error) SaveMeasurementProfile(string name, IReadOnlyDictionary<string, decimal> measurements);
}
