using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IMeasurementProfileRepository
{
    IReadOnlyList<MeasurementProfile> Load();

    void Save(IEnumerable<MeasurementProfile> profiles);
}
