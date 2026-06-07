namespace PatternPro.Core.IServices;

public interface IProductionCertificationService
{
    Pattern.Core.Model.ProductionValidationReport ValidateForFactory(int patternId, string styleKey);

    Pattern.Core.Model.Pattern? ApproveForCutting(int patternId, string approvedBy);

    Pattern.Core.Model.Pattern? RevokeCuttingApproval(int patternId);

    Pattern.Core.Model.Pattern? RecordCutterTest(int patternId, bool passed, string testedBy, string? notes);

    /// <summary>Apply default SA, approve, ensure cutter pass when QC allows; returns final validation.</summary>
    Pattern.Core.Model.ProductionValidationReport CompleteFactoryCertification(int patternId, string styleKey, string approvedBy);
}
