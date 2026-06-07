using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class SizeChartService : ISizeChartService
{
    private readonly object _lock = new();
    private readonly IGradingService _gradingService;
    private readonly ISizeChartRepository _sizeChart;
    private readonly IMeasurementProfileRepository _profilesRepo;
    private readonly List<MeasurementProfile> _profiles;
    private readonly List<string> _columns;
    private readonly List<SizeRow> _rows;

    public SizeChartService(
        IGradingService gradingService,
        ISizeChartRepository sizeChart,
        IMeasurementProfileRepository profilesRepo)
    {
        _gradingService = gradingService;
        _sizeChart      = sizeChart;
        _profilesRepo   = profilesRepo;
        _profiles       = profilesRepo.Load().Select(CloneProfile).ToList();

        var persisted = sizeChart.Load();
        var source = persisted.Rows.Count > 0 ? persisted : AppDataDefaults.CreateDefaultSizeChart();
        if (persisted.Rows.Count == 0)
            sizeChart.Save(source);

        _columns = [.. source.Columns];
        _rows    = source.Rows.Select(CloneRow).ToList();
    }

    public IReadOnlyList<string> GetColumnLabels()
    {
        lock (_lock)
            return _columns.ToList();
    }

    public IReadOnlyList<SizeRow> GetAll()
    {
        lock (_lock)
            return _rows.Select(CloneRow).ToList();
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
            PersistSizeChart();
        }

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

            PersistSizeChart();
        }

        return (true, null);
    }

    public (bool Ok, string? Error) TryUpdateCell(string measurementPoint, int columnIndex, decimal value)
    {
        lock (_lock)
        {
            var row = _rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(measurementPoint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (row is null) return (false, "Measurement row not found.");
            if (columnIndex < 0 || columnIndex >= row.Values.Count)
                return (false, "Invalid size column.");

            row.Values[columnIndex] = value;
            PersistSizeChart();
            return (true, null);
        }
    }

    public (bool Ok, string? Error) TryUpdateRowMeta(string measurementPoint, decimal toleranceCm, string? measurementMethod)
    {
        lock (_lock)
        {
            var row = _rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(measurementPoint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (row is null) return (false, "Measurement row not found.");

            row.ToleranceCm = Math.Max(0, toleranceCm);
            row.MeasurementMethod = measurementMethod?.Trim() ?? string.Empty;
            PersistSizeChart();
            return (true, null);
        }
    }

    public IReadOnlyList<MeasurementProfile> GetMeasurementProfiles()
    {
        lock (_lock)
            return _profiles.Select(CloneProfile).ToList();
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

            PersistProfiles();
        }

        return (true, null);
    }

    private void PersistSizeChart()
    {
        _sizeChart.Save(new SizeChartStore
        {
            Columns = [.. _columns],
            Rows    = _rows.Select(CloneRow).ToList(),
        });
    }

    private void PersistProfiles() =>
        _profilesRepo.Save(_profiles.Select(CloneProfile));

    private static SizeRow CloneRow(SizeRow r) =>
        new()
        {
            MeasurementPoint = r.MeasurementPoint,
            ToleranceCm = r.ToleranceCm,
            MeasurementMethod = r.MeasurementMethod,
            Values = [.. r.Values],
        };

    private static MeasurementProfile CloneProfile(MeasurementProfile p) =>
        new()
        {
            Name = p.Name,
            Measurements = new Dictionary<string, decimal>(p.Measurements, StringComparer.OrdinalIgnoreCase),
        };
}
