using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IPatternDraftingService
{
    IReadOnlyList<PieceDefinition> DraftPieces(string styleKey, string baseSize, int? patternId = null);

    IReadOnlyList<PieceDefinition> DraftProductionPieces(string styleKey, string baseSize, int? patternId = null);

    Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSet(string styleKey, IEnumerable<string> sizes, int? patternId = null);

    Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSetFromMeasurements(
        string styleKey,
        string baseSize,
        IEnumerable<string> sizes,
        IReadOnlyDictionary<string, decimal> baseMeasurements,
        int? patternId = null);

    string RecommendClosestSize(string baseSize, IReadOnlyDictionary<string, decimal> baseMeasurements, int? patternId = null);

    IReadOnlyList<PieceDefinition> GradeCanvasPiecesForSize(
        IReadOnlyList<PieceDefinition> canvasPieces,
        string styleKey,
        string baseSize,
        string targetSize,
        int? patternId = null);
}
