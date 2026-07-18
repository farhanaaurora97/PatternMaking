using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class SeamValidationService : ISeamValidationService
{
    private const double SeamToleranceCm = 0.75;
    private static readonly string[] RequiredPieces = ["Front Leg", "Back Leg", "Waistband"];

    public (IReadOnlyList<ProductionValidationIssue> Errors, IReadOnlyList<ProductionValidationIssue> Warnings)
        ValidatePieces(IReadOnlyList<PieceDefinition> pieces, string styleKey)
    {
        var errors = new List<ProductionValidationIssue>();
        var warnings = new List<ProductionValidationIssue>();

        if (pieces.Count == 0)
        {
            errors.Add(Issue("NO_PIECES", "No pattern pieces to export.", "Save geometry on the Canvas editor first."));
            return (errors, warnings);
        }

        foreach (var name in RequiredPieces)
        {
            var piece = SeamGeometry.FindPiece(pieces, name);
            if (piece is null)
                errors.Add(Issue("MISSING_PIECE", $"Required piece '{name}' is missing."));
            else if (piece.Points.Count < 3)
                errors.Add(Issue("INVALID_PIECE", $"'{name}' has fewer than 3 points."));
            else if (piece.Grain is null || piece.Grain.Count < 2)
                warnings.Add(Issue("NO_GRAIN", $"Grain line missing on '{name}'.", "Sewing floor needs grain direction."));
        }

        foreach (var pair in StyleAssemblyCatalog.GetSeamPairs(styleKey))
        {
            var a = SeamGeometry.FindPiece(pieces, pair.PieceA);
            var b = SeamGeometry.FindPiece(pieces, pair.PieceB);
            if (a is null || b is null || a.Points.Count < 2 || b.Points.Count < 2)
                continue;

            var lenA = SeamGeometry.EdgeLengthPx(a, pair.EdgeIndexA);
            var lenB = SeamGeometry.EdgeLengthPx(b, pair.EdgeIndexB);
            var diffCm = SeamGeometry.ToCm(Math.Abs(lenA - lenB));
            if (diffCm > SeamToleranceCm)
            {
                warnings.Add(Issue(
                    "SEAM_LENGTH",
                    $"{pair.Label}: edge lengths differ by {diffCm:0.##} cm (tolerance {SeamToleranceCm} cm).",
                    $"{pair.PieceA} edge {pair.EdgeIndexA} vs {pair.PieceB} edge {pair.EdgeIndexB}"));
            }
        }

        var front = SeamGeometry.FindPiece(pieces, "Front Leg");
        var back = SeamGeometry.FindPiece(pieces, "Back Leg");
        var wb = SeamGeometry.FindPiece(pieces, "Waistband");
        if (front is not null && back is not null && wb is not null && front.Points.Count > 0 && back.Points.Count > 0)
        {
            var waistFront = SeamGeometry.EdgeLengthPx(front, 0);
            var waistBack = SeamGeometry.EdgeLengthPx(back, 0);
            var wbLen = SeamGeometry.EdgeLengthPx(wb, 0);
            var totalWaist = waistFront + waistBack;
            var waistDiffCm = SeamGeometry.ToCm(Math.Abs(totalWaist - wbLen));
            if (waistDiffCm > SeamToleranceCm * 2)
            {
                warnings.Add(Issue(
                    "WAIST_BALANCE",
                    $"Waist attach length ({SeamGeometry.ToCm(totalWaist):0.##} cm) vs waistband edge ({SeamGeometry.ToCm(wbLen):0.##} cm) differs by {waistDiffCm:0.##} cm.",
                    "Check waist curve and waistband length before bulk cut."));
            }
        }

        foreach (var piece in pieces)
        {
            if (piece.SeamAllowance > 0)
                continue;
            if (piece.Category.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
                continue;
            warnings.Add(Issue("NO_SA", $"No seam allowance on '{piece.Name}'.", "Use Prepare for factory or set SA on Canvas."));
        }

        return (errors, warnings);
    }

    private static ProductionValidationIssue Issue(string code, string message, string? detail = null) =>
        new() { Code = code, Message = message, Detail = detail };
}
