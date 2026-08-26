using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class SizeChartService : ISizeChartService, IReloadableAppData
{
    private readonly object _lock = new();
    private readonly IGradingService _gradingService;
    private readonly ISizeChartRepository _sizeChart;
    private readonly IMeasurementProfileRepository _profilesRepo;
    private readonly IPatternRepository _patterns;
    private readonly List<MeasurementProfile> _profiles;
    private readonly List<string> _globalColumns;
    private readonly List<SizeRow> _globalRows;

    public SizeChartService(
        IGradingService gradingService,
        ISizeChartRepository sizeChart,
        IMeasurementProfileRepository profilesRepo,
        IPatternRepository patterns)
    {
        _gradingService = gradingService;
        _sizeChart      = sizeChart;
        _profilesRepo   = profilesRepo;
        _patterns       = patterns;
        _profiles       = profilesRepo.Load().Select(CloneProfile).ToList();

        var persisted = sizeChart.Load();
        var source = ResolveStartupSizeChart(persisted);
        if (AppDataDefaults.NeedsDefaultSeed(persisted) || AppDataDefaults.IsLegacyDefaultSizeChart(persisted))
            sizeChart.Save(source);

        _globalColumns = [.. source.Columns];
        _globalRows    = source.Rows.Select(CloneRow).ToList();
    }

    private static SizeChartStore ResolveStartupSizeChart(SizeChartStore persisted)
    {
        if (AppDataDefaults.NeedsDefaultSeed(persisted))
            return AppDataDefaults.CreateDefaultSizeChart();
        if (AppDataDefaults.IsLegacyDefaultSizeChart(persisted))
            return AppDataDefaults.CreateDefaultSizeChart();
        return persisted;
    }

    public void ReloadFromStore()
    {
        lock (_lock)
        {
            _profiles.Clear();
            _profiles.AddRange(_profilesRepo.Load().Select(CloneProfile));

            var persisted = _sizeChart.Load();
            var source = ResolveStartupSizeChart(persisted);
            _globalColumns.Clear();
            _globalColumns.AddRange(source.Columns);
            _globalRows.Clear();
            _globalRows.AddRange(source.Rows.Select(CloneRow));
        }
    }

    private void EnsureFreshGlobal()
    {
        lock (_lock)
        {
            var persisted = _sizeChart.Load();
            var source = ResolveStartupSizeChart(persisted);
            _globalColumns.Clear();
            _globalColumns.AddRange(source.Columns);
            _globalRows.Clear();
            _globalRows.AddRange(source.Rows.Select(CloneRow));
        }
    }

    public SizeChartSnapshot GetSnapshot(int? patternId = null)
    {
        if (patternId is null or <= 0)
            EnsureFreshGlobal();
        return BuildSnapshot(patternId, LoadMutableScope(patternId));
    }

    public IReadOnlyList<string> GetColumnLabels(int? patternId = null)
    {
        if (patternId is null or <= 0)
            EnsureFreshGlobal();
        lock (_lock)
            return LoadMutableScope(patternId).columns.ToList();
    }

    public IReadOnlyList<SizeRow> GetAll(int? patternId = null)
    {
        if (patternId is null or <= 0)
            EnsureFreshGlobal();
        lock (_lock)
            return LoadMutableScope(patternId).rows.Select(CloneRow).ToList();
    }

    public string ExportCsv(int? patternId = null)
    {
        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            var header = "Measurement,± cm,Method," + string.Join(",", scope.columns);
            var lines = new List<string> { header };
            foreach (var r in scope.rows)
                lines.Add($"{r.MeasurementPoint},{r.ToleranceCm},{EscapeCsv(r.MeasurementMethod)},{string.Join(",", r.Values)}");
            return string.Join("\n", lines);
        }
    }

    public (bool Ok, string? Error) TryAddSizeColumn(string label, int? patternId = null)
    {
        var clean = label.Trim();
        if (string.IsNullOrEmpty(clean))
            return (false, "Enter a size label (e.g. 3XL or 40).");

        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            if (scope.columns.Any(c => c.Equals(clean, StringComparison.OrdinalIgnoreCase)))
                return (false, "That size already exists in the chart.");

            foreach (var row in scope.rows)
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

            scope.columns.Add(clean);
            PersistScope(patternId, scope);
        }

        if (patternId is null)
            _gradingService.AddColumn(clean);
        return (true, null);
    }

    public (bool Ok, string? Error) TryAddMeasurementRow(string measurementPoint, string? copyFromMeasurementPoint, int? patternId = null)
    {
        var name = measurementPoint.Trim();
        if (string.IsNullOrEmpty(name))
            return (false, "Enter a measurement name.");

        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            if (scope.rows.Any(r => r.MeasurementPoint.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return (false, "A row with that name already exists.");

            var copyFrom = copyFromMeasurementPoint?.Trim() ?? string.Empty;
            var source = scope.rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(copyFrom, StringComparison.OrdinalIgnoreCase));
            if (source is null && scope.rows.Count > 0)
                source = scope.rows[0];

            var values = source?.Values ?? scope.columns.Select(_ => 0m).ToList();
            if (values.Count != scope.columns.Count)
                return (false, "Chart is inconsistent; refresh the page.");

            scope.rows.Add(new SizeRow
            {
                MeasurementPoint = name,
                ToleranceCm = source?.ToleranceCm ?? 0m,
                MeasurementMethod = source?.MeasurementMethod ?? string.Empty,
                Values = [.. values],
            });

            PersistScope(patternId, scope);
        }

        return (true, null);
    }

    public (bool Ok, string? Error) TryDeleteMeasurementRow(string measurementPoint, int? patternId = null)
    {
        var name = measurementPoint.Trim();
        if (string.IsNullOrEmpty(name))
            return (false, "Select a measurement row to delete.");

        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            if (scope.rows.Count <= 1)
                return (false, "At least one measurement row must remain.");

            var idx = scope.rows.FindIndex(r =>
                r.MeasurementPoint.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return (false, "Measurement row not found.");

            scope.rows.RemoveAt(idx);
            PersistScope(patternId, scope);
        }

        return (true, null);
    }

    public (bool Ok, string? Error) TryDeleteSizeColumn(int columnIndex, int? patternId = null)
    {
        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            if (scope.columns.Count <= 1)
                return (false, "At least one size column must remain.");
            if (columnIndex < 0 || columnIndex >= scope.columns.Count)
                return (false, "Invalid size column.");

            var protectedLabel = patternId is > 0
                ? ResolvePattern(patternId)?.BaseSize
                : "M";
            if (!string.IsNullOrWhiteSpace(protectedLabel)
                && scope.columns[columnIndex].Equals(protectedLabel, StringComparison.OrdinalIgnoreCase))
                return (false, $"Cannot delete base size column ({protectedLabel}).");

            foreach (var row in scope.rows)
            {
                if (columnIndex < row.Values.Count)
                    row.Values.RemoveAt(columnIndex);
            }

            scope.columns.RemoveAt(columnIndex);
            PersistScope(patternId, scope);
        }

        if (patternId is null)
        {
            var (ok, err) = _gradingService.TryDeleteColumn(columnIndex);
            if (!ok) return (false, err);
        }

        return (true, null);
    }

    public (bool Ok, string? Error) TryUpdateCell(string measurementPoint, int columnIndex, decimal value, int? patternId = null)
    {
        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            var row = scope.rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(measurementPoint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (row is null) return (false, "Measurement row not found.");
            if (columnIndex < 0 || columnIndex >= row.Values.Count)
                return (false, "Invalid size column.");

            row.Values[columnIndex] = value;
            PersistScope(patternId, scope);
            return (true, null);
        }
    }

    public (bool Ok, string? Error) TryUpdateRowMeta(string measurementPoint, decimal toleranceCm, string? measurementMethod, int? patternId = null)
    {
        lock (_lock)
        {
            var scope = LoadMutableScope(patternId);
            var row = scope.rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(measurementPoint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (row is null) return (false, "Measurement row not found.");

            row.ToleranceCm = Math.Max(0, toleranceCm);
            row.MeasurementMethod = measurementMethod?.Trim() ?? string.Empty;
            PersistScope(patternId, scope);
            return (true, null);
        }
    }

    public (bool Ok, string? Error) SetChartSettings(int patternId, bool useCustomChart, string chartMode)
    {
        if (patternId <= 0)
            return (false, "Select a valid style.");

        var mode = MeasurementChartMode.IsGarment(chartMode) ? MeasurementChartMode.Garment : MeasurementChartMode.Body;

        lock (_lock)
        {
            var store = _patterns.Load();
            var pattern = store?.Patterns.FirstOrDefault(p => p.Id == patternId);
            if (pattern is null)
                return (false, "Style not found.");

            pattern.UseCustomSizeChart = useCustomChart;
            pattern.ChartMode = mode;
            _patterns.Save(store!.Patterns, store.NextId);
        }

        return (true, null);
    }

    public (bool Ok, string? Error) CopyGlobalToPattern(int patternId)
    {
        lock (_lock)
        {
            var copy = new SizeChartStore
            {
                Columns = [.. _globalColumns],
                Rows = _globalRows.Select(CloneRow).ToList(),
            };
            _sizeChart.SaveForPattern(patternId, copy);

            var store = _patterns.Load();
            var pattern = store?.Patterns.FirstOrDefault(p => p.Id == patternId);
            if (pattern is null)
                return (false, "Style not found.");
            pattern.UseCustomSizeChart = true;
            _patterns.Save(store!.Patterns, store.NextId);
        }

        return (true, null);
    }

    public (bool Ok, string? Error) InitializeGarmentTemplate(int patternId)
    {
        lock (_lock)
        {
            var template = AppDataDefaults.CreateGarmentSizeChartTemplate();
            _sizeChart.SaveForPattern(patternId, template);

            var store = _patterns.Load();
            var pattern = store?.Patterns.FirstOrDefault(p => p.Id == patternId);
            if (pattern is null)
                return (false, "Style not found.");
            pattern.UseCustomSizeChart = true;
            pattern.ChartMode = MeasurementChartMode.Garment;
            _patterns.Save(store!.Patterns, store.NextId);
        }

        return (true, null);
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

    private SizeChartSnapshot BuildSnapshot(int? patternId, (List<string> columns, List<SizeRow> rows) scope)
    {
        var pattern = ResolvePattern(patternId);
        var chartMode = pattern?.ChartMode ?? MeasurementChartMode.Body;
        var useCustom = pattern?.UseCustomSizeChart == true && patternId is > 0;

        return new SizeChartSnapshot
        {
            PatternId = patternId,
            PatternCode = pattern?.Code,
            ChartMode = chartMode,
            UseCustomChart = useCustom,
            Columns = scope.columns.ToList(),
            Rows = scope.rows.Select(CloneRow).ToList(),
        };
    }

    private (List<string> columns, List<SizeRow> rows) LoadMutableScope(int? patternId)
    {
        if (patternId is null or <= 0)
        {
            EnsureGlobalChartLoaded();
            return (_globalColumns, _globalRows);
        }

        var pattern = ResolvePattern(patternId);
        if (pattern?.UseCustomSizeChart != true)
        {
            EnsureGlobalChartLoaded();
            return (_globalColumns, _globalRows);
        }

        var persisted = _sizeChart.LoadForPattern(patternId.Value);
        if (persisted is null || AppDataDefaults.NeedsDefaultSeed(persisted))
        {
            EnsureGlobalChartLoaded();
            var copy = new SizeChartStore
            {
                Columns = [.. _globalColumns],
                Rows = _globalRows.Select(CloneRow).ToList(),
            };
            _sizeChart.SaveForPattern(patternId.Value, copy);
            return (copy.Columns.ToList(), copy.Rows.Select(CloneRow).ToList());
        }

        return (
            persisted.Columns.ToList(),
            persisted.Rows.Select(CloneRow).ToList());
    }

    private void EnsureGlobalChartLoaded()
    {
        if (_globalRows.Count > 0 && _globalColumns.Count > 0)
            return;

        var persisted = _sizeChart.Load();
        var source = ResolveStartupSizeChart(persisted);
        if (AppDataDefaults.NeedsDefaultSeed(persisted) || AppDataDefaults.IsLegacyDefaultSizeChart(persisted))
            _sizeChart.Save(source);

        _globalColumns.Clear();
        _globalColumns.AddRange(source.Columns);
        _globalRows.Clear();
        _globalRows.AddRange(source.Rows.Select(CloneRow));
    }

    private void PersistScope(int? patternId, (List<string> columns, List<SizeRow> rows) scope)
    {
        if (patternId is null or <= 0 && scope.rows.Count == 0)
        {
            EnsureGlobalChartLoaded();
            return;
        }

        var store = new SizeChartStore
        {
            Columns = [.. scope.columns],
            Rows = scope.rows.Select(CloneRow).ToList(),
        };

        if (patternId is null or <= 0)
        {
            _globalColumns.Clear();
            _globalColumns.AddRange(scope.columns);
            _globalRows.Clear();
            _globalRows.AddRange(scope.rows.Select(CloneRow));
            _sizeChart.Save(store);
            return;
        }

        _sizeChart.SaveForPattern(patternId.Value, store);
    }

    private Pattern.Core.Model.Pattern? ResolvePattern(int? patternId)
    {
        if (patternId is null or <= 0)
            return null;
        return _patterns.Load()?.Patterns.FirstOrDefault(p => p.Id == patternId);
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

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
