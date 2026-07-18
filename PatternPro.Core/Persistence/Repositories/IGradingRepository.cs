using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IGradingRepository
{
    GradingStore Load();

    void Save(GradingStore store);
}
