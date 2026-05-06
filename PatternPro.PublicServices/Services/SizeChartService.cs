using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class SizeChartService : ISizeChartService
{
    private readonly object _lock = new();
    private readonly IGradingService _gradingService;
    private readonly List<MeasurementProfile> _profiles = [];

    private readonly List<string> _columns;

    private readonly List<SizeRow> _rows;

    public SizeChartService(IGradingService gradingService)
    {
        _gradingService = gradingService;

        _columns =
        [
            "XS", "S", "M", "L", "XL", "XXL",
        ];

        _rows =
        [
            new() { MeasurementPoint = "Waist",        Values = [60, 64, 68, 72, 76, 80] },
            new() { MeasurementPoint = "Hip",          Values = [84, 88, 92, 96, 100, 106] },
            new() { MeasurementPoint = "Front Rise",   Values = [25, 25.5m, 26, 26.5m, 27, 27.5m] },
            new() { MeasurementPoint = "Back Rise",    Values = [34, 35, 36, 37, 38, 39] },
            new() { MeasurementPoint = "Crotch Depth", Values = [26, 27, 28, 29, 30, 31] },
            new() { MeasurementPoint = "Thigh",        Values = [50, 53, 56, 59, 62, 66] },
            new() { MeasurementPoint = "Knee",         Values = [34, 36, 38, 40, 42, 44] },
            new() { MeasurementPoint = "Ankle",        Values = [29, 31, 33, 35, 37, 39] },
            new() { MeasurementPoint = "Inseam",       Values = [77, 78, 79, 80, 80, 80] },
            new() { MeasurementPoint = "Outseam",      Values = [103, 104.5m, 106, 107.5m, 109, 110] },
        ];
    }

    public IReadOnlyList<string> GetColumnLabels()
    {
        lock (_lock)
            return _columns.ToList();
    }

    public IReadOnlyList<SizeRow> GetAll()
    {
        lock (_lock)
        {
            return _rows
                .Select(r => new SizeRow
                {
                    MeasurementPoint = r.MeasurementPoint,
                    Values = [.. r.Values],
                })
                .ToList();
        }
    }

    public string ExportCsv()
    {
        lock (_lock)
        {
            var header = "Measurement," + string.Join(",", _columns);
            var lines = new List<string> { header };
            foreach (var r in _rows)
                lines.Add($"{r.MeasurementPoint},{string.Join(",", r.Values)}");
            return string.Join("\n", lines);
        }
    }

    public (bool Ok, string? Error) TryAddSizeColumn(string label)
    {
        var clean = label.Trim();
        if (string.IsNullOrEmpty(clean))
            return (false, "Enter a size label (e.g. 3XL).");

        lock (_lock)
        {
            if (_columns.Any(c => c.Equals(clean, StringComparison.OrdinalIgnoreCase)))
                return (false, "That size already exists in the chart.");

            foreach (var row in _rows)
            {
                var v = row.Values;
                decimal next;
                if (v.Count >= 2)
                    next = 2 * v[^1] - v[^2];
                else if (v.Count == 1)
                    next = v[0];
                else
                    next = 0;

                v.Add(next);
            }

            _columns.Add(clean);
        }

        // Keep grading tables in sync — call outside our lock to avoid lock ordering issues.
        _gradingService.AddColumn(clean);

        return (true, null);
    }

    public (bool Ok, string? Error) TryAddMeasurementRow(string measurementPoint, string? copyFromMeasurementPoint)
    {
        var name = measurementPoint.Trim();
        if (string.IsNullOrEmpty(name))
            return (false, "Enter a measurement name.");

        lock (_lock)
        {
            if (_rows.Any(r => r.MeasurementPoint.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return (false, "A row with that name already exists.");

            var copyFrom = copyFromMeasurementPoint?.Trim() ?? string.Empty;
            var source = _rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(copyFrom, StringComparison.OrdinalIgnoreCase));
            if (source is null)
                source = _rows[0];

            if (source.Values.Count != _columns.Count)
                return (false, "Chart is inconsistent; refresh the page.");

            _rows.Add(new SizeRow
            {
                MeasurementPoint = name,
                Values = [.. source.Values],
            });
        }

        return (true, null);
    }

    public IReadOnlyList<MeasurementProfile> GetMeasurementProfiles()
    {
        lock (_lock)
        {
            return _profiles
                .Select(p => new MeasurementProfile
                {
                    Name = p.Name,
                    Measurements = new Dictionary<string, decimal>(p.Measurements, StringComparer.OrdinalIgnoreCase),
                })
                .ToList();
        }
    }

    public (bool Ok, string? Error) SaveMeasurementProfile(string name, IReadOnlyDictionary<string, decimal> measurements)
    {
        var clean = name.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return (false, "Profile name is required.");
        if (measurements.Count == 0)
            return (false, "At least one measurement is required.");

        lock (_lock)
        {
            var existing = _profiles.FirstOrDefault(p => p.Name.Equals(clean, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _profiles.Add(new MeasurementProfile
                {
                    Name = clean,
                    Measurements = new Dictionary<string, decimal>(measurements, StringComparer.OrdinalIgnoreCase),
                });
            }
            else
            {
                existing.Measurements = new Dictionary<string, decimal>(measurements, StringComparer.OrdinalIgnoreCase);
            }
        }

        return (true, null);
    }
}
