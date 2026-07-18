using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface ISeamValidationService
{
    /// <summary>QC checks on saved canvas geometry (errors block factory export).</summary>
    (IReadOnlyList<ProductionValidationIssue> Errors, IReadOnlyList<ProductionValidationIssue> Warnings)
        ValidatePieces(IReadOnlyList<PieceDefinition> pieces, string styleKey);
}
