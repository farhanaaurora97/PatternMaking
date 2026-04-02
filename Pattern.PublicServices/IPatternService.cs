using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

public interface IPatternService
{
    IReadOnlyList<Core.Model.Pattern>  GetAll();
    StyleDefinition                    GetStyleDefinition(string styleKey);
    Core.Model.Pattern                 Create(string name, string styleKey, string baseSize, string designer);
    Core.Model.Pattern?                CycleStatus(int id);
    bool                               Delete(int id);
    IReadOnlyList<Core.Model.Pattern>  Search(string? query);
    IReadOnlyList<Core.Model.Pattern>  Sort(IEnumerable<Core.Model.Pattern> patterns, string column, bool ascending);
}