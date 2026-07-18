using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface ISizeChartRepository
{
    SizeChartStore Load();

    void Save(SizeChartStore store);

    SizeChartStore? LoadForPattern(int patternId);

    void SaveForPattern(int patternId, SizeChartStore store);

    void DeleteForPattern(int patternId);
}
