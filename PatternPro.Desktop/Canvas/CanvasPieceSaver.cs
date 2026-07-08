using CoreModel = Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Desktop.Helpers;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasPieceSaver
{
    public static (bool Ok, string? Error, int Saved) SaveAll(
        IPieceService pieceService,
        int patternId,
        string styleKey,
        IReadOnlyList<CoreModel.PieceDefinition> pieces)
    {
        var style = FitStyleKeys.Normalize(styleKey);
        var errors = new List<string>();
        var saved = 0;

        foreach (var piece in pieces)
        {
            if (piece.Points.Count < 3)
            {
                errors.Add($"{piece.Name}: need at least 3 points");
                continue;
            }

            var pts = piece.Points
                .Select(p => new[] { p[0], p[1] })
                .ToList();

            var (ok, error) = patternId > 0
                ? pieceService.UpdatePatternPieceGeometry(
                    patternId, style, piece.Name,
                    pts, piece.OffsetX, piece.OffsetY,
                    piece.Grain, piece.Cf, piece.Notches,
                    piece.SeamAllowance, piece.SeamAllowanceJoin)
                : pieceService.UpdatePieceGeometry(
                    style, piece.Name,
                    pts, piece.OffsetX, piece.OffsetY,
                    piece.Grain, piece.Cf, piece.Notches,
                    piece.SeamAllowance, piece.SeamAllowanceJoin);

            if (ok) saved++;
            else if (!string.IsNullOrWhiteSpace(error))
                errors.Add($"{piece.Name}: {error}");
        }

        return errors.Count == 0
            ? (true, null, saved)
            : (false, string.Join("; ", errors), saved);
    }
}
