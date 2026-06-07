using System.Text.Json;
using Pattern.Core.Model;
using PatternPro.Core.Persistence;

namespace PatternPro.DataAccess;

/// <summary>
/// File-based <see cref="IAppDataStore"/> using JSON files under <c>App_Data/</c>.
/// </summary>
public sealed class JsonAppDataStore : IAppDataStore, IDataAccessLayer
{
    private readonly string _piecesPath;
    private readonly string _patternsPath;
    private readonly string _profilesPath;
    private readonly string _sizeChartPath;
    private readonly string _gradingPath;
    private readonly string _easePath;

    public JsonAppDataStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _piecesPath     = Path.Combine(dataDirectory, "pieces.json");
        _patternsPath   = Path.Combine(dataDirectory, "patterns.json");
        _profilesPath   = Path.Combine(dataDirectory, "measurement-profiles.json");
        _sizeChartPath  = Path.Combine(dataDirectory, "size-chart.json");
        _gradingPath    = Path.Combine(dataDirectory, "grading.json");
        _easePath       = Path.Combine(dataDirectory, "ease-overrides.json");
    }

    public PiecesStore LoadPieces() =>
        ReadJson(_piecesPath, () => new PiecesStore());

    public void SavePieces(PiecesStore store) =>
        WriteJson(_piecesPath, store);

    public PatternsStore? LoadPatternsStore()
    {
        if (!File.Exists(_patternsPath))
            return null;
        try
        {
            var json = File.ReadAllText(_patternsPath);
            return JsonSerializer.Deserialize<PatternsStore>(json, PersistenceJson.Options);
        }
        catch
        {
            return null;
        }
    }

    public void SavePatterns(IEnumerable<Pattern.Core.Model.Pattern> patterns, int nextId)
    {
        var wrapper = new PatternsStore { NextId = nextId, Patterns = patterns.ToList() };
        WriteJson(_patternsPath, wrapper);
    }

    public IReadOnlyList<MeasurementProfile> LoadMeasurementProfiles()
    {
        var store = ReadJson(_profilesPath, () => new MeasurementProfilesStore());
        return store.Profiles;
    }

    public void SaveMeasurementProfiles(IEnumerable<MeasurementProfile> profiles)
    {
        WriteJson(_profilesPath, new MeasurementProfilesStore { Profiles = profiles.ToList() });
    }

    public SizeChartStore LoadSizeChart() =>
        ReadJson(_sizeChartPath, () => new SizeChartStore());

    public void SaveSizeChart(SizeChartStore store) =>
        WriteJson(_sizeChartPath, store);

    public GradingStore LoadGrading() =>
        ReadJson(_gradingPath, () => new GradingStore());

    public void SaveGrading(GradingStore store) =>
        WriteJson(_gradingPath, store);

    public EaseOverridesStore LoadEaseOverrides() =>
        ReadJson(_easePath, () => new EaseOverridesStore());

    public void SaveEaseOverrides(EaseOverridesStore store) =>
        WriteJson(_easePath, store);

    private static T ReadJson<T>(string path, Func<T> fallback) where T : class
    {
        if (!File.Exists(path))
            return fallback();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, PersistenceJson.Options) ?? fallback();
        }
        catch
        {
            return fallback();
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, PersistenceJson.Options));
        }
        catch
        {
            // Same behavior as legacy JsonDataStore: avoid crashing the app on IO errors.
        }
    }
}
