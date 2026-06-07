using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IPieceService
{
    StyleDefinition                GetStyleDefinition(string styleKey);
    IReadOnlyList<string>          GetPieceList(string styleKey);

    /// <summary>Style-based piece definitions — used by Canvas (shared across all patterns of that style).</summary>
    IReadOnlyList<PieceDefinition> GetPieceDefinitions(string styleKey);

    /// <summary>Pattern-specific piece definitions — seeded from style on first access, then owned by the pattern.</summary>
    IReadOnlyList<PieceDefinition> GetPieceDefinitions(int patternId, string styleKey);

    IReadOnlyList<int[]>           GetBasePiecePoints();

    /// <summary>Adds a piece to a specific pattern's piece list.</summary>
    (bool Ok, string? Error, PieceDefinition? Piece) AddPiece(
        int patternId, string styleKey, string pieceName, string category,
        string cut, string grainLine, string color, string description);

    /// <summary>Removes a piece from a specific pattern's piece list.</summary>
    (bool Ok, string? Error) DeletePiece(int patternId, string pieceName);

    /// <summary>Creates a brand-new piece on the canvas from user-drawn points.</summary>
    (bool Ok, string? Error, PieceDefinition? Piece) CreateStylePiece(
        string styleKey, string name, string category, string cut, string color,
        List<int[]> points, int ox, int oy);
    (bool Ok, string? Error, PieceDefinition? Piece) CreatePatternPiece(
        int patternId, string styleKey, string name, string category, string cut, string color,
        List<int[]> points, int ox, int oy);

    /// <summary>Persists full geometry edits (points, offset, grain, CF, notches) for a style-shared piece.</summary>
    (bool Ok, string? Error) UpdatePieceGeometry(
        string styleKey, string pieceName,
        List<int[]> points, int offsetX, int offsetY,
        List<int[]>? grain, List<int[]>? cf, List<int[]>? notches,
        double seamAllowance = 0, string? seamAllowanceJoin = null);
    (bool Ok, string? Error) UpdatePatternPieceGeometry(
        int patternId, string styleKey, string pieceName,
        List<int[]> points, int offsetX, int offsetY,
        List<int[]>? grain, List<int[]>? cf, List<int[]>? notches,
        double seamAllowance = 0, string? seamAllowanceJoin = null);

    /// <summary>Replaces all pieces for a specific pattern with a freshly drafted set.</summary>
    void ReplacePatternPieces(int patternId, IEnumerable<PieceDefinition> pieces);

    /// <summary>Returns IDs of all patterns that have canvas geometry saved.</summary>
    IReadOnlySet<int> GetSavedPatternIds();

    /// <summary>Sets default seam allowance (cm) on cut pieces that have none; returns count updated.</summary>
    int ApplyDefaultSeamAllowances(int patternId, string styleKey, double seamAllowanceCm = 1.0);

    /// <summary>Replaces pattern-owned geometry with a fresh copy from the style template (designer reset).</summary>
    void ResetPatternFromStyle(int patternId, string styleKey);
}

