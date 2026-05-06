using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IPatternService
{
    IReadOnlyList<Pattern.Core.Model.Pattern> GetAll();
    StyleDefinition                    GetStyleDefinition(string styleKey);
    Pattern.Core.Model.Pattern         Create(string name, string styleKey, string baseSize, string designer, string categoryKey);
    Pattern.Core.Model.Pattern?        CycleStatus(int id);
    Pattern.Core.Model.Pattern?        SetStatus(int id, string status);
    bool                               Delete(int id);
    Pattern.Core.Model.Pattern?        Duplicate(int id);
    IReadOnlyList<Pattern.Core.Model.Pattern> Search(string? query);
    IReadOnlyList<Pattern.Core.Model.Pattern> Sort(IEnumerable<Pattern.Core.Model.Pattern> patterns, string column, bool ascending);
    Pattern.Core.Model.Pattern?        SetDueDate(int id, DateTime? date);
}

