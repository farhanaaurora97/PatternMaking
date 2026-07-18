using Pattern.Core.Model;
using PatternPro.Core.Persistence;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.DataAccess.Repositories;

internal sealed class PatternRepository(IAppDataStore data) : IPatternRepository
{
    public PatternsStore? Load() => data.LoadPatternsStore();

    public void Save(IEnumerable<Pattern.Core.Model.Pattern> patterns, int nextId) =>
        data.SavePatterns(patterns, nextId);
}

internal sealed class PieceRepository(IAppDataStore data) : IPieceRepository
{
    public PiecesStore Load() => data.LoadPieces();

    public void Save(PiecesStore store) => data.SavePieces(store);
}

internal sealed class SizeChartRepository(IAppDataStore data) : ISizeChartRepository
{
    public SizeChartStore Load() => data.LoadSizeChart();

    public void Save(SizeChartStore store) => data.SaveSizeChart(store);

    public SizeChartStore? LoadForPattern(int patternId) => data.LoadPatternSizeChart(patternId);

    public void SaveForPattern(int patternId, SizeChartStore store) =>
        data.SavePatternSizeChart(patternId, store);

    public void DeleteForPattern(int patternId) => data.DeletePatternSizeChart(patternId);
}

internal sealed class GradingRepository(IAppDataStore data) : IGradingRepository
{
    public GradingStore Load() => data.LoadGrading();

    public void Save(GradingStore store) => data.SaveGrading(store);
}

internal sealed class EaseOverridesRepository(IAppDataStore data) : IEaseOverridesRepository
{
    public EaseOverridesStore Load() => data.LoadEaseOverrides();

    public void Save(EaseOverridesStore store) => data.SaveEaseOverrides(store);
}

internal sealed class MeasurementProfileRepository(IAppDataStore data) : IMeasurementProfileRepository
{
    public IReadOnlyList<MeasurementProfile> Load() => data.LoadMeasurementProfiles();

    public void Save(IEnumerable<MeasurementProfile> profiles) =>
        data.SaveMeasurementProfiles(profiles);
}
