namespace PatternPro.Desktop.Canvas;

public sealed class CanvasDrawOverlay
{
    public IReadOnlyList<(float X, float Y)> Points { get; init; } = [];
    public float? CursorX { get; init; }
    public float? CursorY { get; init; }
}

public sealed class PendingNewPiece
{
    public required List<int[]> Points { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
}
