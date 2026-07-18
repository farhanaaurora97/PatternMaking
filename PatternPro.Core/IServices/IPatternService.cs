using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IPatternService
{
    IReadOnlyList<Pattern.Core.Model.Pattern> GetAll();
    StyleDefinition                    GetStyleDefinition(string styleKey);
    Pattern.Core.Model.Pattern         Create(string name, string styleKey, string baseSize, string designer, string categoryKey,
        string? season = null, string? owner = null, string? lifecycleStatus = null);
    Pattern.Core.Model.Pattern?        CycleStatus(int id);
    Pattern.Core.Model.Pattern?        SetStatus(int id, string status);
    bool                               Delete(int id);
    Pattern.Core.Model.Pattern?        Duplicate(int id);
    IReadOnlyList<Pattern.Core.Model.Pattern> Search(string? query);
    IReadOnlyList<Pattern.Core.Model.Pattern> Sort(IEnumerable<Pattern.Core.Model.Pattern> patterns, string column, bool ascending);
    Pattern.Core.Model.Pattern?        SetDueDate(int id, DateTime? date);

    Pattern.Core.Model.Pattern?        SetLifecycleStatus(int id, string lifecycleStatus);

    Pattern.Core.Model.Pattern?        UpdateStyleSheet(int id, string? season, string? owner, string? designer);

    Pattern.Core.Model.Pattern?        SetRevision(int id, string revision);

    Pattern.Core.Model.Pattern?        ApproveForCutting(int id, string approvedBy);

    Pattern.Core.Model.Pattern?        RevokeCuttingApproval(int id);

    Pattern.Core.Model.Pattern?        RecordCutterTest(int id, bool passed, string testedBy, string? notes);

    Pattern.Core.Model.Pattern?        SetCloReview(int id, bool completed, string? notes);

    Pattern.Core.Model.Pattern?        SetShrinkagePercent(int id, decimal percent);
}

