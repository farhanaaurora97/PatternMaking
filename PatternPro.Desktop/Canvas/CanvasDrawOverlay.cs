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

public sealed class CanvasMeasureOverlay
{
    public float? Ax { get; init; }
    public float? Ay { get; init; }
    public float? Bx { get; init; }
    public float? By { get; init; }
    public float? CursorX { get; init; }
    public float? CursorY { get; init; }
}

public sealed class CanvasEditorOverlay
{
    public SnapKind SnapKind { get; init; }
    public float? SnapX { get; init; }
    public float? SnapY { get; init; }
    public float? SymmetryAxisWorldX { get; init; }
    public CanvasLiveMeasurements? LiveMeasurements { get; init; }
    public int? HighlightEdgeIndex { get; init; }
    public int? SelectedVertexIndex { get; init; }
    public int? PrimaryPieceIndex { get; init; }
    public CanvasWalkSeamResult? WalkSeam { get; init; }
    public int? WalkSeamEdgeA { get; init; }
    public int? WalkSeamEdgeB { get; init; }
    public float? InternalLineStartX { get; init; }
    public float? InternalLineStartY { get; init; }
    public float? InternalLineCursorX { get; init; }
    public float? InternalLineCursorY { get; init; }
    public int? SelectedInternalLineIndex { get; init; }
}
