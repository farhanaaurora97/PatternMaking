using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IPieceRepository
{
    PiecesStore Load();

    void Save(PiecesStore store);
}
