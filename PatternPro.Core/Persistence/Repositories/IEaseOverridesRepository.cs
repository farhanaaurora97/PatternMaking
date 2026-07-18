using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IEaseOverridesRepository
{
    EaseOverridesStore Load();

    void Save(EaseOverridesStore store);
}
