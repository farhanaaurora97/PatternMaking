using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IPatternDraftingService
{
    IReadOnlyList<PieceDefinition> DraftPieces(string styleKey, string baseSize);
    Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSet(string styleKey, IEnumerable<string> sizes);
    Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSetFromMeasurements(
        string styleKey,
        string baseSize,
        IEnumerable<string> sizes,
        IReadOnlyDictionary<string, decimal> baseMeasurements);
    string RecommendClosestSize(string baseSize, IReadOnlyDictionary<string, decimal> baseMeasurements);

    /// <summary>
    /// Applies size-to-size vertex deltas from the measurement-driven drafter to saved canvas pieces
    /// (pattern-local outlines). Preserves <see cref="PieceDefinition.OffsetX"/> / <see cref="PieceDefinition.OffsetY"/> layout.
    /// </summary>
    IReadOnlyList<PieceDefinition> GradeCanvasPiecesForSize(
        IReadOnlyList<PieceDefinition> canvasPieces,
        string styleKey,
        string baseSize,
        string targetSize);
}

