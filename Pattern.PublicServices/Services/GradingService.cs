using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class GradingService : IGradingService
{
    private readonly object _lock = new();
    private readonly IGradingRepository _grading;
    private readonly List<string> _columns;
    private readonly Dictionary<string, (string Label, List<GradingRow> Rows)> _data;
    private int _baseIdx;

    public GradingService(IGradingRepository grading)
    {
        _grading = grading;
        var persisted = grading.Load();
        var source = persisted.Styles.Count > 0 ? persisted : AppDataDefaults.CreateDefaultGrading();

        if (persisted.Styles.Count == 0)
            grading.Save(source);

        _columns  = [.. source.Columns];
        _baseIdx  = source.BaseIndex;
        _data     = source.Styles.ToDictionary(
            s => s.StyleKey,
            s => (s.Label, s.Rows.Select(CloneRow).ToList()),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetColumnLabels()
    {
        lock (_lock) return _columns.ToList();
    }

    public IReadOnlyList<GradingRow> GetGradingTable(string styleKey)
    {
        lock (_lock)
            return _data.TryGetValue(styleKey, out var d) ? d.Rows.Select(CloneRow).ToList() : _data["skinny"].Rows.Select(CloneRow).ToList();
    }

    public string GetStyleLabel(string styleKey)
    {
        lock (_lock)
            return _data.TryGetValue(styleKey, out var d) ? d.Label : "Skinny Fit";
    }

    public void AddColumn(string label)
    {
        lock (_lock)
        {
            foreach (var (_, rows) in _data.Values)
            {
                foreach (var row in rows)
                {
                    var d = row.Deltas;
                    double next = d.Count >= 2 ? 2 * d[^1] - d[^2]
                                : d.Count == 1 ? d[0]
                                : 0;
                    d.Add(next);
                }
            }

            _columns.Add(label);
            Persist();
        }
    }

    public string ExportCsv(string styleKey)
    {
        lock (_lock)
        {
            var rows = _data.TryGetValue(styleKey, out var d) ? d.Rows : _data["skinny"].Rows;
            var header = "Measurement," + string.Join(",", _columns.Select((c, i) =>
                i == _baseIdx ? $"{c}(Base)" : c));
            var lines = new List<string> { header };
            foreach (var r in rows)
            {
                var cells = r.Deltas.Select((v, i) =>
                    i == r.BaseIndex ? "0" : v > 0 ? $"+{v:0.##}" : v.ToString("0.##"));
                lines.Add($"{r.MeasurementPoint},{string.Join(",", cells)}");
            }

            return string.Join("\n", lines);
        }
    }

    public (bool Ok, string? Error) AddRow(string styleKey, string measurementPoint, string? copyFromPoint)
    {
        if (string.IsNullOrWhiteSpace(measurementPoint))
            return (false, "Measurement point name is required.");

        lock (_lock)
        {
            if (!_data.TryGetValue(styleKey, out var entry))
                return (false, $"Style '{styleKey}' not found.");

            var point = measurementPoint.Trim();

            if (entry.Rows.Any(r => r.MeasurementPoint.Equals(point, StringComparison.OrdinalIgnoreCase)))
                return (false, $"'{point}' already exists in this grading table.");

            List<double> deltas;

            if (!string.IsNullOrWhiteSpace(copyFromPoint))
            {
                var source = entry.Rows.FirstOrDefault(r =>
                    r.MeasurementPoint.Equals(copyFromPoint.Trim(), StringComparison.OrdinalIgnoreCase));

                if (source is null)
                    return (false, $"Copy-from row '{copyFromPoint}' not found.");

                deltas = [.. source.Deltas];
            }
            else
            {
                deltas = Enumerable.Repeat(0.0, _columns.Count).ToList();
            }

            entry.Rows.Add(new GradingRow
            {
                MeasurementPoint = point,
                BaseIndex        = _baseIdx,
                Deltas           = deltas,
            });

            Persist();
            return (true, null);
        }
    }

    public (bool Ok, string? Error) TryUpdateDelta(string styleKey, string measurementPoint, int columnIndex, double delta)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(styleKey, out var entry))
                return (false, $"Style '{styleKey}' not found.");

            var row = entry.Rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(measurementPoint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (row is null) return (false, "Measurement row not found.");
            if (columnIndex < 0 || columnIndex >= row.Deltas.Count)
                return (false, "Invalid size column.");
            if (columnIndex == row.BaseIndex)
                return (false, "Base size delta is always zero.");

            row.Deltas[columnIndex] = delta;
            Persist();
            return (true, null);
        }
    }

    private void Persist()
    {
        var store = new GradingStore
        {
            Columns   = [.. _columns],
            BaseIndex = _baseIdx,
            Styles    = _data.Select(kvp => new GradingStyleEntry
            {
                StyleKey = kvp.Key,
                Label    = kvp.Value.Label,
                Rows     = kvp.Value.Rows.Select(CloneRow).ToList(),
            }).ToList(),
        };
        _grading.Save(store);
    }

    private static GradingRow CloneRow(GradingRow r) =>
        new()
        {
            MeasurementPoint = r.MeasurementPoint,
            BaseIndex        = r.BaseIndex,
            Deltas           = [.. r.Deltas],
        };
}
