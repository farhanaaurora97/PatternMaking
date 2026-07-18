using Pattern.Core.Model;

namespace PatternPro.Core.Persistence;

/// <summary>
/// Application persistence for pattern rows and piece geometry (JSON files or database).
/// </summary>
public interface IAppDataStore
{
    PiecesStore LoadPieces();

    void SavePieces(PiecesStore store);

    PatternsStore? LoadPatternsStore();

    void SavePatterns(IEnumerable<Pattern.Core.Model.Pattern> patterns, int nextId);

    IReadOnlyList<MeasurementProfile> LoadMeasurementProfiles();

    void SaveMeasurementProfiles(IEnumerable<MeasurementProfile> profiles);

    SizeChartStore LoadSizeChart();

    void SaveSizeChart(SizeChartStore store);

    SizeChartStore? LoadPatternSizeChart(int patternId);

    void SavePatternSizeChart(int patternId, SizeChartStore store);

    void DeletePatternSizeChart(int patternId);

    GradingStore LoadGrading();

    void SaveGrading(GradingStore store);

    EaseOverridesStore LoadEaseOverrides();

    void SaveEaseOverrides(EaseOverridesStore store);
}
