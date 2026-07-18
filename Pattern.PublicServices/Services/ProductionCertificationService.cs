using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class ProductionCertificationService(
    IPatternService patternService,
    IPieceService pieceService,
    ISeamValidationService seamValidation) : IProductionCertificationService
{
    public ProductionValidationReport ValidateForFactory(int patternId, string styleKey)
    {
        var style = NormalizeStyleKey(styleKey);
        var pattern = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);

        if (patternId <= 0)
            return Blocked(patternId, style, pattern, [Error("NO_PATTERN", "Select a saved pattern before factory export.")]);

        if (pattern is null)
            return Blocked(patternId, style, null, [Error("PATTERN_NOT_FOUND", $"Pattern {patternId} was not found.")]);

        var errors = new List<ProductionValidationIssue>();
        var warnings = new List<ProductionValidationIssue>();

        if (!pattern.ApprovedForCutting)
            errors.Add(Error("NOT_APPROVED", "Pattern is not approved for cutting.", "Approve in Production QC panel."));

        if (!pattern.CutterTestPassed)
            errors.Add(Error("CUTTER_TEST", "Cutter/plotter test not recorded as passed.", "Record cutter test after trial on factory machine."));

        var pieces = pieceService.GetPieceDefinitions(patternId, style).ToList();
        var (seamErrors, seamWarnings) = seamValidation.ValidatePieces(pieces, style);
        errors.AddRange(seamErrors);
        warnings.AddRange(seamWarnings);

        return Finish(patternId, style, pattern, errors, warnings);
    }

    public Pattern.Core.Model.Pattern? ApproveForCutting(int patternId, string approvedBy)
    {
        var pattern = patternService.GetAll().FirstOrDefault(p => p.Id == patternId);
        if (pattern is null) return null;

        var style = NormalizeStyleFromPattern(pattern);
        var pieces = pieceService.GetPieceDefinitions(patternId, style).ToList();
        var (errors, _) = seamValidation.ValidatePieces(pieces, style);
        if (errors.Count > 0)
            return null;

        return patternService.ApproveForCutting(patternId, approvedBy);
    }

    public Pattern.Core.Model.Pattern? RevokeCuttingApproval(int patternId) =>
        patternService.RevokeCuttingApproval(patternId);

    public Pattern.Core.Model.Pattern? RecordCutterTest(int patternId, bool passed, string testedBy, string? notes) =>
        patternService.RecordCutterTest(patternId, passed, testedBy, notes);

    public ProductionValidationReport CompleteFactoryCertification(int patternId, string styleKey, string approvedBy)
    {
        var style = NormalizeStyleKey(styleKey);
        pieceService.ApplyDefaultSeamAllowances(patternId, style, 1.0);

        var pre = ValidateForFactory(patternId, style);
        var qcErrors = pre.Issues.Where(i => i.Code is not "NOT_APPROVED" and not "CUTTER_TEST").ToList();
        if (qcErrors.Count > 0)
            return pre;

        if (!pre.ApprovedForCutting)
            ApproveForCutting(patternId, approvedBy);

        return ValidateForFactory(patternId, style);
    }

    private static ProductionValidationReport Blocked(
        int patternId, string style, Pattern.Core.Model.Pattern? pattern, List<ProductionValidationIssue> errors) =>
        new()
        {
            PatternId = patternId,
            StyleKey = style,
            ApprovedForCutting = pattern?.ApprovedForCutting ?? false,
            CutterTestPassed = pattern?.CutterTestPassed ?? false,
            Issues = errors,
            Warnings = [],
            CanExportToFactory = false,
        };

    private static ProductionValidationReport Finish(
        int patternId, string style, Pattern.Core.Model.Pattern pattern,
        List<ProductionValidationIssue> errors,
        List<ProductionValidationIssue> warnings) =>
        new()
        {
            PatternId = patternId,
            StyleKey = style,
            ApprovedForCutting = pattern.ApprovedForCutting,
            CutterTestPassed = pattern.CutterTestPassed,
            Issues = errors,
            Warnings = warnings,
            CanExportToFactory = errors.Count == 0,
        };

    private static ProductionValidationIssue Error(string code, string message, string? detail = null) =>
        new() { Code = code, Message = message, Detail = detail };

    private static string NormalizeStyleKey(string styleKey)
    {
        if (string.IsNullOrWhiteSpace(styleKey)) return "skinny";
        var s = styleKey.Trim();
        return s.Equals("wide leg", StringComparison.OrdinalIgnoreCase) ? "wideLeg" : s.ToLowerInvariant();
    }

    private static string NormalizeStyleFromPattern(Pattern.Core.Model.Pattern pattern) => pattern.Style.Trim().ToLowerInvariant() switch
    {
        "wide leg" => "wideLeg",
        "skinny" => "skinny",
        "slim" => "slim",
        "straight" => "straight",
        "bootcut" => "bootcut",
        _ => "skinny",
    };
}
