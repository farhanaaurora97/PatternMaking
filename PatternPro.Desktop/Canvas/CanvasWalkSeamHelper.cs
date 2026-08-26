using CoreModel = Pattern.Core.Model;

namespace PatternPro.Desktop.Canvas;

public sealed class CanvasWalkSeamResult
{
    public int EdgeA { get; init; }
    public int EdgeB { get; init; }
    public float LengthA { get; init; }
    public float LengthB { get; init; }
    public float DeltaPx { get; init; }
    public float DeltaPercent { get; init; }
    public bool Match => Math.Abs(DeltaPx) <= 0.5f;
}

internal static class CanvasWalkSeamHelper
{
    public static CanvasWalkSeamResult Compare(CoreModel.PieceDefinition piece, int edgeA, int edgeB)
    {
        var lenA = PiecePathBuilder.EdgeLength(piece, edgeA);
        var lenB = PiecePathBuilder.EdgeLength(piece, edgeB);
        var delta = lenB - lenA;
        var pct = lenA > 0.01f ? delta / lenA * 100f : 0f;
        return new CanvasWalkSeamResult
        {
            EdgeA = edgeA,
            EdgeB = edgeB,
            LengthA = lenA,
            LengthB = lenB,
            DeltaPx = delta,
            DeltaPercent = pct,
        };
    }
}
