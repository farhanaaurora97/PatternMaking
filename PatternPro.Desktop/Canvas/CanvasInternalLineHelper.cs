using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasInternalLineHelper
{
    public static void EnsureList(CoreModel.PieceDefinition piece) =>
        piece.InternalLines ??= [];

    public static void AddLine(CoreModel.PieceDefinition piece, int x1, int y1, int x2, int y2, string label)
    {
        EnsureList(piece);
        piece.InternalLines!.Add(new CoreModel.PieceInternalLine
        {
            Label = label,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
        });
    }

    public static int? HitLine(CoreModel.PieceDefinition piece, float wx, float wy, float scale, float hitPx = 10f)
    {
        if (piece.InternalLines is not { Count: > 0 }) return null;
        var best = float.MaxValue;
        int? bestIdx = null;
        var ox = piece.OffsetX;
        var oy = piece.OffsetY;

        for (var i = 0; i < piece.InternalLines.Count; i++)
        {
            var line = piece.InternalLines[i];
            var (_, d) = CanvasGeometryHelper.ClosestOnSegment(
                wx - ox, wy - oy,
                line.X1, line.Y1,
                line.X2, line.Y2);
            if (d >= best || d > hitPx / scale) continue;
            best = d;
            bestIdx = i;
        }

        return bestIdx;
    }

    public static void TransformLines(
        CoreModel.PieceDefinition piece,
        Func<int, int, (int X, int Y)> map)
    {
        if (piece.InternalLines is null) return;
        foreach (var line in piece.InternalLines)
        {
            var (x1, y1) = map(line.X1, line.Y1);
            var (x2, y2) = map(line.X2, line.Y2);
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
        }
    }
}
