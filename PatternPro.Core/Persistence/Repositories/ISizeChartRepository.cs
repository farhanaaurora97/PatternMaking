using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface ISizeChartRepository
{
    SizeChartStore Load();

    void Save(SizeChartStore store);
}
