using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

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
}
