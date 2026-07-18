using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IPatternRepository
{
    PatternsStore? Load();

    void Save(IEnumerable<Pattern.Core.Model.Pattern> patterns, int nextId);
}
