using CoreModel = Pattern.Core.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.Controls;
using Pattern.Web.Model;
using PatternPro.Desktop.Canvas;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

using Pattern.Core.Model;

namespace PatternPro.Desktop.Services;

public enum CanvasSurfaceMode
{
    None,
    Pattern,
    Nest,
}

public sealed class DesktopCanvasHost
{
    private const int MoveThrottleMs = 16;
    private const int DrawClosePx = 14;

    private SKCanvasView? _view;
    private CanvasSurfaceMode _mode = CanvasSurfaceMode.None;
    private int _activeSlots;

    private IList<CoreModel.PieceDefinition> _pieces = [];
    private CanvasViewport _viewport = new();
    private int? _primaryIndex;
    private IReadOnlySet<int> _selectedIndices = new HashSet<int>();
    private Func<Task>? _onPiecesChanged;
    private Func<int?, IReadOnlySet<int>, Task>? _onSelectedChanged;
    private Func<Task>? _onHostChanged;
    private readonly CanvasPieceHistory _history = new();

    private bool _panning;
    private bool _dragging;
    private float _lastX, _lastY;
    private int? _dragVertex;
    private float _pieceStartWx, _pieceStartWy;
    private int _pieceOrigOx, _pieceOrigOy;
    private readonly Dictionary<int, (int Ox, int Oy)> _multiDragOrigins = new();
    private bool _multiPieceDrag;
    private long _lastInvalidateMs;
    private bool _invalidatePending;

    private readonly List<(float X, float Y)> _drawPoints = [];
    private float? _drawCursorX, _drawCursorY;
    private PendingNewPiece? _pendingNewPiece;

    private float? _measureAx, _measureAy, _measureBx, _measureBy;
    private float? _measureCursorX, _measureCursorY;
    private int? _dragCurveEdge;
    private int? _dragCurveHandle;
    private float _cursorWorldX, _cursorWorldY;
    private SnapResult _lastSnap;
    private int? _selectedVertexIndex;
    private int? _selectedEdgeIndex;
    private float? _ilineAx, _ilineAy;
    private float? _ilineCursorX, _ilineCursorY;
    private int? _selectedInternalLineIndex;
    private int? _walkSeamEdgeA;
    private int? _walkSeamEdgeB;
    private CanvasWalkSeamResult? _walkSeamResult;

    public string InternalLineLabel { get; set; } = "Guide";
    public CanvasWalkSeamResult? WalkSeamResult => _walkSeamResult;
    public int? SelectedInternalLineIndex => _selectedInternalLineIndex;

    private IReadOnlyList<int[]> _nestBasePoints = [];
    private IReadOnlyList<NestSizeViewModel> _nestSizes = [];
    private IReadOnlyList<bool> _nestVisible = [];
    private float _nestScale = 1f;

    public double SlotWidth { get; private set; }
    public double SlotHeight { get; private set; }
    public CanvasLayerOptions Layers { get; } = new();
    public CanvasEditorOptions EditorOptions { get; } = new();
    public CanvasToolMode ToolMode { get; set; } = CanvasToolMode.Select;
    public float ZoomPercent => _viewport.Scale * 100f;
    public float CursorWorldX => _cursorWorldX;
    public float CursorWorldY => _cursorWorldY;
    public bool CanUndo => _history.CanUndo;
    public PendingNewPiece? PendingNewPiece => _pendingNewPiece;
    public int DrawPointCount => _drawPoints.Count;
    public float? MeasureDistance =>
        _measureAx is float ax && _measureAy is float ay && _measureBx is float bx && _measureBy is float by
            ? MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))
            : null;

    public string MeasureDistanceDisplay =>
        MeasureDistance is float d ? CanvasUnits.FormatCm(d) : "—";

    public string CursorPositionDisplay =>
        CanvasUnits.FormatCm(_cursorWorldX) + " × " + CanvasUnits.FormatCm(_cursorWorldY);

    public int? SelectedVertexIndex => _selectedVertexIndex;
    public int? SelectedEdgeIndex => _selectedEdgeIndex;

    public string SnapKindDisplay => _lastSnap.Kind switch
    {
        SnapKind.Vertex => "Snap: point",
        SnapKind.Midpoint => "Snap: midpoint",
        SnapKind.Edge => "Snap: edge",
        SnapKind.Grid => "Snap: grid",
        _ => EditorOptions.SnapEnabled ? "Snap: —" : "Snap: off",
    };

    public CanvasLiveMeasurements? GetLiveMeasurements() =>
        _primaryIndex is int pi && pi >= 0 && pi < _pieces.Count
            ? CanvasMeasurementsHelper.Compute(_pieces[pi], _selectedEdgeIndex)
            : null;

    public string SelectedVertexDisplay
    {
        get
        {
            if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
                || _selectedVertexIndex is not int vi || vi < 0 || vi >= _pieces[si].Points.Count)
                return "—";
            var pt = _pieces[si].Points[vi];
            return $"#{vi + 1}  {CanvasUnits.FormatCm(pt[0])} × {CanvasUnits.FormatCm(pt[1])}";
        }
    }

    public void ToggleSymmetry()
    {
        EditorOptions.SymmetryEnabled = !EditorOptions.SymmetryEnabled;
        if (EditorOptions.SymmetryEnabled && _primaryIndex is int pi && pi >= 0 && pi < _pieces.Count)
            EditorOptions.SymmetryAxisWorldX = CanvasSymmetryHelper.ComputeAxisWorldX(_pieces[pi]);
        else
            EditorOptions.SymmetryAxisWorldX = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public async Task ApplyNumericVertexPositionAsync(double localXCm, double localYCm)
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
            || _selectedVertexIndex is not int vi || vi < 0 || vi >= _pieces[si].Points.Count)
            return;

        PushHistory(si);
        var piece = _pieces[si];
        var pt = piece.Points[vi];
        pt[0] = (int)Math.Round(CanvasUnits.ToPixels(localXCm));
        pt[1] = (int)Math.Round(CanvasUnits.ToPixels(localYCm));
        if (EditorOptions.SymmetryEnabled && EditorOptions.SymmetryAxisWorldX is float axis)
            CanvasSymmetryHelper.ApplyMirrorAfterVertexEdit(piece, vi, axis);
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public async Task MoveSelectedVertexByCmAsync(double dxCm, double dyCm)
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
            || _selectedVertexIndex is not int vi || vi < 0 || vi >= _pieces[si].Points.Count)
            return;

        PushHistory(si);
        var piece = _pieces[si];
        var pt = piece.Points[vi];
        pt[0] += (int)Math.Round(CanvasUnits.ToPixels(dxCm));
        pt[1] += (int)Math.Round(CanvasUnits.ToPixels(dyCm));
        if (EditorOptions.SymmetryEnabled && EditorOptions.SymmetryAxisWorldX is float axis)
            CanvasSymmetryHelper.ApplyMirrorAfterVertexEdit(piece, vi, axis);
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public async Task SetSelectedEdgeSeamAllowanceCmAsync(double cm)
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
            || _selectedEdgeIndex is not int ei || ei < 0 || ei >= _pieces[si].Points.Count)
            return;

        PushHistory(si);
        var piece = _pieces[si];
        PiecePathBuilder.EnsureEdges(piece);
        piece.Edges![ei].SeamAllowance = Math.Max(0, CanvasUnits.ToPixels(cm));
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public string EdgeSeamAllowanceCmDisplay()
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
            || _selectedEdgeIndex is not int ei || ei < 0 || ei >= _pieces[si].Points.Count)
            return "";

        var sa = PieceSeamAllowanceHelper.ResolveEdgeSeamAllowance(_pieces[si], ei);
        return CanvasUnits.ToCm(sa).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Attach(SKCanvasView view)
    {
        _view = view;
        _view.IsVisible = false;
        _view.EnableTouchEvents = true;
        _view.PaintSurface += OnPaintSurface;
        _view.Touch += OnTouch;
    }

    public void Activate(CanvasSurfaceMode mode)
    {
        _activeSlots++;
        _mode = mode;
        UpdateVisibility();
        Invalidate();
    }

    public void Deactivate(CanvasSurfaceMode mode)
    {
        _activeSlots = Math.Max(0, _activeSlots - 1);
        if (_activeSlots == 0)
            _mode = CanvasSurfaceMode.None;
        UpdateVisibility();
    }

    /// <summary>Hide native Skia overlay when Blazor navigates off Canvas/Nest (avoids ghost drawing over other pages).</summary>
    public void HideOverlayForNavigation()
    {
        _activeSlots = 0;
        _mode = CanvasSurfaceMode.None;
        UpdateVisibility();
    }

    public void ConfigurePattern(
        IList<CoreModel.PieceDefinition> pieces,
        CanvasViewport viewport,
        int? primaryIndex,
        IReadOnlySet<int> selectedIndices,
        Func<Task>? onPiecesChanged,
        Func<int?, IReadOnlySet<int>, Task>? onSelectedChanged,
        Func<Task>? onHostChanged = null)
    {
        _pieces = pieces;
        _viewport = viewport;
        _primaryIndex = primaryIndex;
        _selectedIndices = selectedIndices;
        _onPiecesChanged = onPiecesChanged;
        _onSelectedChanged = onSelectedChanged;
        _onHostChanged = onHostChanged;
        Invalidate();
    }

    public void ClearHistory() => _history.Clear();

    public void ConfigureNest(
        IReadOnlyList<int[]> basePoints,
        IReadOnlyList<NestSizeViewModel> sizes,
        IReadOnlyList<bool> visible,
        float nestScale)
    {
        _nestBasePoints = basePoints;
        _nestSizes = sizes;
        _nestVisible = visible;
        _nestScale = nestScale;
        Invalidate();
    }

    public void SetNestScale(float scale)
    {
        _nestScale = scale;
        Invalidate();
    }

    public void Invalidate() => RequestInvalidate(force: true);

    public void ZoomIn()
    {
        var cx = (float)(SlotWidth / 2);
        var cy = (float)(SlotHeight / 2);
        _viewport.ZoomAt(1.12f, cx, cy);
        Invalidate();
    }

    public void ZoomOut()
    {
        var cx = (float)(SlotWidth / 2);
        var cy = (float)(SlotHeight / 2);
        _viewport.ZoomAt(0.89f, cx, cy);
        Invalidate();
    }

    public async Task<bool> UndoAsync()
    {
        if (!_history.TryPop(_pieces, out var restoredIndex)) return false;
        _primaryIndex = restoredIndex;
        var restored = new HashSet<int>();
        if (restoredIndex >= 0)
            restored.Add(restoredIndex);
        if (_onSelectedChanged is not null)
            await _onSelectedChanged(restoredIndex, restored);
        if (_onPiecesChanged is not null)
            await _onPiecesChanged();
        await NotifyHostChangedAsync();
        Invalidate();
        return true;
    }

    public void CancelDrawSession()
    {
        _drawPoints.Clear();
        _drawCursorX = _drawCursorY = null;
        _pendingNewPiece = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public void ClearMeasure()
    {
        _measureAx = _measureAy = _measureBx = _measureBy = null;
        _measureCursorX = _measureCursorY = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public void ClearWalkSeam()
    {
        _walkSeamEdgeA = _walkSeamEdgeB = null;
        _walkSeamResult = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public void CancelInternalLineDraft()
    {
        _ilineAx = _ilineAy = _ilineCursorX = _ilineCursorY = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public async Task DeleteSelectedInternalLineAsync()
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count
            || _selectedInternalLineIndex is not int li)
            return;

        var piece = _pieces[si];
        if (piece.InternalLines is null || li < 0 || li >= piece.InternalLines.Count)
            return;

        PushHistory(si);
        piece.InternalLines.RemoveAt(li);
        if (piece.InternalLines.Count == 0)
            piece.InternalLines = null;
        _selectedInternalLineIndex = null;
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public void StraightenSelectedEdge()
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count) return;
        var edge = PiecePathBuilder.HitEdge(_pieces[si], _lastWorldX, _lastWorldY, _viewport.Scale);
        if (edge is null) return;
        PushHistory(si);
        PiecePathBuilder.SetLineEdge(_pieces[si], edge.Value);
        _ = NotifyPiecesChangedAsync();
        Invalidate();
    }

    public async Task MirrorSelectedHorizontalAsync()
    {
        var targets = GetTransformTargets();
        if (targets.Count == 0) return;
        foreach (var si in targets)
        {
            PushHistory(si);
            CanvasPieceTransform.MirrorHorizontal(_pieces[si]);
        }
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public async Task MirrorSelectedVerticalAsync()
    {
        var targets = GetTransformTargets();
        if (targets.Count == 0) return;
        foreach (var si in targets)
        {
            PushHistory(si);
            CanvasPieceTransform.MirrorVertical(_pieces[si]);
        }
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    public async Task RotateSelected90Async()
    {
        var targets = GetTransformTargets();
        if (targets.Count == 0) return;
        foreach (var si in targets)
        {
            PushHistory(si);
            CanvasPieceTransform.Rotate90Clockwise(_pieces[si]);
        }
        await NotifyPiecesChangedAsync();
        Invalidate();
    }

    private IReadOnlyList<int> GetTransformTargets()
    {
        if (_selectedIndices.Count > 0)
            return _selectedIndices.Where(i => i >= 0 && i < _pieces.Count).OrderBy(i => i).ToList();
        if (_primaryIndex is int pi && pi >= 0 && pi < _pieces.Count)
            return [pi];
        return [];
    }

    public void ToggleSnap()
    {
        EditorOptions.SnapEnabled = !EditorOptions.SnapEnabled;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    public void CancelPendingPiece()
    {
        _pendingNewPiece = null;
        _ = NotifyHostChangedAsync();
    }

    public async Task UpdateSlotBoundsAsync(ElementReference host, IJSRuntime js)
    {
        if (_view is null) return;
        try
        {
            var rect = await js.InvokeAsync<DomRect>("patternPro.getElementRect", host);
            SlotWidth = Math.Max(1, rect.Width);
            SlotHeight = Math.Max(1, rect.Height);
            _view.Margin = new Thickness(rect.X, rect.Y, 0, 0);
            _view.WidthRequest = SlotWidth;
            _view.HeightRequest = SlotHeight;
            _view.HorizontalOptions = LayoutOptions.Start;
            _view.VerticalOptions = LayoutOptions.Start;
            UpdateVisibility();
            Invalidate();
        }
        catch
        {
            // WebView not ready during navigation.
        }
    }

    private void UpdateVisibility()
    {
        if (_view is null) return;
        _view.IsVisible = _mode != CanvasSurfaceMode.None
            && _view.WidthRequest > 1
            && _view.HeightRequest > 1;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        switch (_mode)
        {
            case CanvasSurfaceMode.Pattern:
                CanvasDrawOverlay? overlay = null;
                if (ToolMode == CanvasToolMode.Draw && (_drawPoints.Count > 0 || _drawCursorX is not null))
                {
                    overlay = new CanvasDrawOverlay
                    {
                        Points = _drawPoints,
                        CursorX = _drawCursorX,
                        CursorY = _drawCursorY,
                    };
                }
                CanvasPainter.Paint(
                    e.Surface.Canvas,
                    e.Info,
                    _pieces,
                    _viewport,
                    _selectedIndices,
                    _primaryIndex,
                    Layers,
                    overlay,
                    BuildMeasureOverlay(),
                    BuildEditorOverlay());
                break;
            case CanvasSurfaceMode.Nest:
                NestCanvasPainter.Paint(e.Surface.Canvas, e.Info, _nestBasePoints, _nestSizes, _nestVisible, _nestScale);
                break;
        }
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (_mode != CanvasSurfaceMode.Pattern)
        {
            e.Handled = true;
            return;
        }

        var x = e.Location.X;
        var y = e.Location.Y;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _ = OnPointerDownAsync(x, y, e.MouseButton);
                break;
            case SKTouchAction.Moved:
                OnPointerMove(x, y);
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _ = OnPointerUpAsync(x, y);
                break;
        }

        e.Handled = true;
    }

    private async Task OnPointerDownAsync(float x, float y, SKMouseButton button)
    {
        _lastX = x;
        _lastY = y;

        if (ToolMode == CanvasToolMode.Pan || button == SKMouseButton.Right)
        {
            _panning = true;
            return;
        }

        var (wx, wy) = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));

        if (ToolMode == CanvasToolMode.Arc)
        {
            if (_primaryIndex is not int ai || ai < 0 || ai >= _pieces.Count) return;
            var piece = _pieces[ai];
            var edge = PiecePathBuilder.HitEdge(piece, wx, wy, _viewport.Scale);
            if (edge is null) return;
            PushHistory(ai);
            var lx = (int)Math.Round(wx - piece.OffsetX);
            var ly = (int)Math.Round(wy - piece.OffsetY);
            PiecePathBuilder.SetCubicEdgeWithTangents(piece, edge.Value, lx, ly);
            PiecePathBuilder.PromoteAdjacentCurvesToCubic(piece, edge.Value);
            _dragging = true;
            _dragCurveEdge = edge;
            _dragCurveHandle = 0;
            await NotifyPiecesChangedAsync();
            Invalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.InternalLine)
        {
            if (_primaryIndex is not int ili || ili < 0 || ili >= _pieces.Count) return;
            var piece = _pieces[ili];
            if (_ilineAx is null || _ilineAy is null)
            {
                var hitLine = CanvasInternalLineHelper.HitLine(piece, wx, wy, _viewport.Scale);
                if (hitLine is int hl)
                {
                    _selectedInternalLineIndex = hl;
                    _ = NotifyHostChangedAsync();
                    Invalidate();
                    return;
                }

                _selectedInternalLineIndex = null;
                _ilineAx = wx;
                _ilineAy = wy;
                _ilineCursorX = wx;
                _ilineCursorY = wy;
                Invalidate();
                _ = NotifyHostChangedAsync();
                return;
            }

            PushHistory(ili);
            var lx1 = (int)Math.Round(_ilineAx.Value - piece.OffsetX);
            var ly1 = (int)Math.Round(_ilineAy.Value - piece.OffsetY);
            var lx2 = (int)Math.Round(wx - piece.OffsetX);
            var ly2 = (int)Math.Round(wy - piece.OffsetY);
            CanvasInternalLineHelper.AddLine(piece, lx1, ly1, lx2, ly2, InternalLineLabel);
            _ilineAx = _ilineAy = _ilineCursorX = _ilineCursorY = null;
            await NotifyPiecesChangedAsync();
            Invalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.WalkSeam)
        {
            if (_primaryIndex is not int wi || wi < 0 || wi >= _pieces.Count) return;
            var piece = _pieces[wi];
            var edge = PiecePathBuilder.HitEdge(piece, wx, wy, _viewport.Scale);
            if (edge is null) return;

            if (_walkSeamEdgeA is null)
            {
                _walkSeamEdgeA = edge;
                _walkSeamEdgeB = null;
                _walkSeamResult = null;
            }
            else if (_walkSeamEdgeB is null && edge != _walkSeamEdgeA)
            {
                _walkSeamEdgeB = edge;
                _walkSeamResult = CanvasWalkSeamHelper.Compare(piece, _walkSeamEdgeA.Value, edge.Value);
            }
            else
            {
                _walkSeamEdgeA = edge;
                _walkSeamEdgeB = null;
                _walkSeamResult = null;
            }

            _selectedEdgeIndex = edge;
            Invalidate();
            _ = NotifyHostChangedAsync();
            return;
        }

        if (ToolMode == CanvasToolMode.Draw)
        {
            HandleDrawClick(x, y, wx, wy);
            return;
        }

        if (ToolMode == CanvasToolMode.Measure)
        {
            HandleMeasureClick(wx, wy);
            return;
        }

        if (ToolMode == CanvasToolMode.DeletePoint)
        {
            if (_primaryIndex is not int di || di < 0 || di >= _pieces.Count) return;
            var delVertex = CanvasGeometryHelper.HitVertex(_pieces[di], wx, wy, _viewport.Scale);
            if (delVertex is null || _pieces[di].Points.Count <= 3) return;
            PushHistory(di);
            _pieces[di].Points.RemoveAt(delVertex.Value);
            PiecePathBuilder.RemoveVertexEdges(_pieces[di], delVertex.Value);
            await NotifyPiecesChangedAsync();
            Invalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.Smooth)
        {
            if (_primaryIndex is not int smi || smi < 0 || smi >= _pieces.Count) return;
            var vertex = CanvasGeometryHelper.HitVertex(_pieces[smi], wx, wy, _viewport.Scale);
            if (vertex is null) return;
            PushHistory(smi);
            PiecePathBuilder.SmoothVertex(_pieces[smi], vertex.Value);
            await NotifyPiecesChangedAsync();
            Invalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.Curve)
        {
            if (_primaryIndex is not int ci || ci < 0 || ci >= _pieces.Count) return;
            var piece = _pieces[ci];
            var handle = PiecePathBuilder.HitCurveHandle(piece, wx, wy, _viewport.Scale);
            if (handle is { } h)
            {
                PushHistory(ci);
                _dragging = true;
                _dragCurveEdge = h.EdgeIndex;
                _dragCurveHandle = h.HandleIndex;
                return;
            }

            var edge = PiecePathBuilder.HitEdge(piece, wx, wy, _viewport.Scale);
            if (edge is null) return;
            PushHistory(ci);
            var lx = (int)Math.Round(wx - piece.OffsetX);
            var ly = (int)Math.Round(wy - piece.OffsetY);
            PiecePathBuilder.SetCubicEdgeWithTangents(piece, edge.Value, lx, ly);
            PiecePathBuilder.PromoteAdjacentCurvesToCubic(piece, edge.Value);
            _dragging = true;
            _dragCurveEdge = edge;
            _dragCurveHandle = 0;
            await NotifyPiecesChangedAsync();
            Invalidate();
            return;
        }

        if (TryBeginCurveHandleDrag(wx, wy))
            return;

        if (ToolMode == CanvasToolMode.Point)
        {
            if (_primaryIndex is not int pi || pi < 0 || pi >= _pieces.Count) return;
            PushHistory(pi);
            if (CanvasGeometryHelper.TryInsertPointOnEdge(_pieces[pi], wx, wy, _viewport.Scale))
            {
                await NotifyPiecesChangedAsync();
                Invalidate();
            }
            else
                _history.TryPop(_pieces, out _);
            return;
        }

        if (ToolMode == CanvasToolMode.Notch)
        {
            if (_primaryIndex is not int ni || ni < 0 || ni >= _pieces.Count) return;
            var piece = _pieces[ni];
            var notchHit = CanvasGeometryHelper.HitNotch(piece, wx, wy, _viewport.Scale);
            PushHistory(ni);
            if (notchHit is int nIdx)
            {
                piece.Notches?.RemoveAt(nIdx);
                await NotifyPiecesChangedAsync();
                Invalidate();
            }
            else if (CanvasGeometryHelper.TryAddNotchOnEdge(piece, wx, wy, _viewport.Scale))
            {
                await NotifyPiecesChangedAsync();
                Invalidate();
            }
            else
                _history.TryPop(_pieces, out _);
            return;
        }

        if (_primaryIndex is int si && si >= 0 && si < _pieces.Count)
        {
            var piece = _pieces[si];
            var vertex = CanvasGeometryHelper.HitVertex(piece, wx, wy, _viewport.Scale);
            if (vertex is not null)
            {
                _selectedVertexIndex = vertex;
                _selectedEdgeIndex = null;
                PushHistory(si);
                _dragVertex = vertex;
                _dragging = true;
                _ = NotifyHostChangedAsync();
                return;
            }

            var edge = PiecePathBuilder.HitEdge(piece, wx, wy, _viewport.Scale);
            if (edge is not null && ToolMode == CanvasToolMode.Select)
            {
                _selectedEdgeIndex = edge;
                _selectedVertexIndex = null;
                _ = NotifyHostChangedAsync();
                Invalidate();
            }

            if (CanvasGeometryHelper.HitPieceBody(piece, wx, wy))
            {
                BeginPieceDrag(si, wx, wy);
                return;
            }
        }

        for (var i = _pieces.Count - 1; i >= 0; i--)
        {
            if (!CanvasGeometryHelper.HitPieceBody(_pieces[i], wx, wy)) continue;
            await SelectAsync(i);
            return;
        }
    }

    private void BeginPieceDrag(int si, float wx, float wy)
    {
        var dragSet = _selectedIndices.Count > 1 && _selectedIndices.Contains(si)
            ? _selectedIndices
            : new HashSet<int> { si };

        foreach (var idx in dragSet)
            PushHistory(idx);

        _dragging = true;
        _dragVertex = null;
        _multiPieceDrag = dragSet.Count > 1;
        _multiDragOrigins.Clear();
        _pieceStartWx = wx;
        _pieceStartWy = wy;

        if (_multiPieceDrag)
        {
            foreach (var idx in dragSet)
            {
                var p = _pieces[idx];
                _multiDragOrigins[idx] = (p.OffsetX, p.OffsetY);
            }
            return;
        }

        var piece = _pieces[si];
        _pieceOrigOx = piece.OffsetX;
        _pieceOrigOy = piece.OffsetY;
    }

    private void OnPointerMove(float x, float y)
    {
        if (ToolMode == CanvasToolMode.Draw)
        {
            var snapped = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));
            _drawCursorX = snapped.Wx;
            _drawCursorY = snapped.Wy;
            RequestInvalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.InternalLine && _ilineAx is float ax && _ilineAy is float ay)
        {
            var snapped = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));
            _ilineCursorX = snapped.Wx;
            _ilineCursorY = snapped.Wy;
            RequestInvalidate();
            return;
        }

        if (ToolMode == CanvasToolMode.Measure || ToolMode == CanvasToolMode.Arc)
        {
            var snapped = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));
            if (ToolMode == CanvasToolMode.Measure)
            {
                _measureCursorX = snapped.Wx;
                _measureCursorY = snapped.Wy;
            }

            _cursorWorldX = snapped.Wx;
            _cursorWorldY = snapped.Wy;
            RequestInvalidate();
            if (ToolMode == CanvasToolMode.Measure)
                return;
        }

        if (_dragging && _dragCurveEdge is int ce && _dragCurveHandle is int ch
            && (_primaryIndex is int csi && csi >= 0 && csi < _pieces.Count))
        {
            var snapped = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));
            var cp = _pieces[csi];
            var lx = (int)Math.Round(snapped.Wx - cp.OffsetX);
            var ly = (int)Math.Round(snapped.Wy - cp.OffsetY);
            if (ToolMode == CanvasToolMode.Arc)
            {
                PiecePathBuilder.SetCubicEdgeWithTangents(cp, ce, lx, ly);
            }
            else
            {
                PiecePathBuilder.EnsureEdges(cp);
                var edge = cp.Edges![ce];
                if (ch == 0)
                    edge.C1 = [lx, ly];
                else
                    edge.C2 = [lx, ly];
            }
            RequestInvalidate();
            return;
        }

        if (_panning)
        {
            _viewport.PanX += x - _lastX;
            _viewport.PanY += y - _lastY;
            _lastX = x;
            _lastY = y;
            RequestInvalidate();
            return;
        }

        if (!_dragging)
        {
            var snapped = ApplySnap(CanvasGeometryHelper.ScreenToWorld(x, y, _viewport));
            _lastWorldX = _cursorWorldX = snapped.Wx;
            _lastWorldY = _cursorWorldY = snapped.Wy;
            _ = NotifyHostChangedAsync();
        }

        if (!_dragging || _primaryIndex is not int si || si < 0 || si >= _pieces.Count)
            return;

        var piece = _pieces[si];
        var (wx2, wy2) = CanvasGeometryHelper.ScreenToWorld(x, y, _viewport);

        if (_dragVertex is int vi && vi < piece.Points.Count)
        {
            var snapped = ApplySnap((wx2, wy2));
            var pt = piece.Points[vi];
            pt[0] = (int)Math.Round(snapped.Wx - piece.OffsetX);
            pt[1] = (int)Math.Round(snapped.Wy - piece.OffsetY);
            if (EditorOptions.SymmetryEnabled && EditorOptions.SymmetryAxisWorldX is float axis)
                CanvasSymmetryHelper.ApplyMirrorAfterVertexEdit(piece, vi, axis);
            RequestInvalidate();
            return;
        }

        if (_dragVertex is null)
        {
            var dx = (int)Math.Round(wx2 - _pieceStartWx);
            var dy = (int)Math.Round(wy2 - _pieceStartWy);
            if (_multiPieceDrag)
            {
                foreach (var (idx, orig) in _multiDragOrigins)
                {
                    _pieces[idx].OffsetX = orig.Ox + dx;
                    _pieces[idx].OffsetY = orig.Oy + dy;
                }
            }
            else
            {
                piece.OffsetX = _pieceOrigOx + dx;
                piece.OffsetY = _pieceOrigOy + dy;
            }
            RequestInvalidate();
        }
    }

    private async Task OnPointerUpAsync(float x, float y)
    {
        var edited = _dragging && (_dragVertex is not null || _dragCurveEdge is not null || _primaryIndex is not null);
        _panning = false;
        _dragging = false;
        _dragVertex = null;
        _dragCurveEdge = null;
        _dragCurveHandle = null;
        _multiPieceDrag = false;
        _multiDragOrigins.Clear();
        _invalidatePending = false;
        RequestInvalidate(force: true);

        if (edited && _onPiecesChanged is not null)
            await _onPiecesChanged();
    }

    private (float Wx, float Wy) ApplySnap((float Wx, float Wy) world)
    {
        _lastSnap = CanvasSnapHelper.SnapWorld(
            world.Wx, world.Wy, EditorOptions, _pieces as IReadOnlyList<CoreModel.PieceDefinition>, _viewport.Scale);
        return (_lastSnap.X, _lastSnap.Y);
    }

    private CanvasEditorOverlay? BuildEditorOverlay()
    {
        CanvasLiveMeasurements? live = null;
        if (_primaryIndex is int pi && pi >= 0 && pi < _pieces.Count)
            live = CanvasMeasurementsHelper.Compute(_pieces[pi], _selectedEdgeIndex);

        return new CanvasEditorOverlay
        {
            SnapKind = _lastSnap.Kind,
            SnapX = _lastSnap.Kind == SnapKind.None ? null : _lastSnap.X,
            SnapY = _lastSnap.Kind == SnapKind.None ? null : _lastSnap.Y,
            SymmetryAxisWorldX = EditorOptions.SymmetryEnabled ? EditorOptions.SymmetryAxisWorldX : null,
            LiveMeasurements = live,
            HighlightEdgeIndex = ToolMode == CanvasToolMode.WalkSeam
                ? _walkSeamEdgeA ?? _selectedEdgeIndex
                : _selectedEdgeIndex ?? (live?.IsLegPiece == true ? live.WaistEdgeIndex : null),
            SelectedVertexIndex = _selectedVertexIndex,
            PrimaryPieceIndex = _primaryIndex,
            WalkSeam = _walkSeamResult,
            WalkSeamEdgeA = _walkSeamEdgeA,
            WalkSeamEdgeB = _walkSeamEdgeB,
            InternalLineStartX = _ilineAx,
            InternalLineStartY = _ilineAy,
            InternalLineCursorX = _ilineCursorX,
            InternalLineCursorY = _ilineCursorY,
            SelectedInternalLineIndex = _selectedInternalLineIndex,
        };
    }

    private void HandleDrawClick(float sx, float sy, float wx, float wy)
    {
        var (snapWx, snapWy) = ApplySnap((wx, wy));
        if (_drawPoints.Count >= 3)
        {
            var fsx = _viewport.PanX + _drawPoints[0].X * _viewport.Scale;
            var fsy = _viewport.PanY + _drawPoints[0].Y * _viewport.Scale;
            if (Math.Abs(sx - fsx) < DrawClosePx && Math.Abs(sy - fsy) < DrawClosePx)
            {
                CloseDrawShape();
                return;
            }
        }

        _drawPoints.Add((snapWx, snapWy));
        _drawCursorX = snapWx;
        _drawCursorY = snapWy;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    private void CloseDrawShape()
    {
        if (_drawPoints.Count < 3) return;

        var minX = _drawPoints.Min(p => p.X);
        var minY = _drawPoints.Min(p => p.Y);
        _pendingNewPiece = new PendingNewPiece
        {
            Points = _drawPoints.Select(p => new[]
            {
                (int)Math.Round(p.X - minX),
                (int)Math.Round(p.Y - minY),
            }).ToList(),
            OffsetX = (int)Math.Round(minX),
            OffsetY = (int)Math.Round(minY),
        };
        _drawPoints.Clear();
        _drawCursorX = _drawCursorY = null;
        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    private float _lastWorldX, _lastWorldY;

    public async Task<bool> TryDeleteVertexAtCursorAsync()
    {
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count) return false;
        var piece = _pieces[si];
        if (piece.Points.Count <= 3) return false;
        var vi = CanvasGeometryHelper.HitVertex(piece, _lastWorldX, _lastWorldY, _viewport.Scale);
        if (vi is null) return false;
        PushHistory(si);
        piece.Points.RemoveAt(vi.Value);
        PiecePathBuilder.RemoveVertexEdges(piece, vi.Value);
        await NotifyPiecesChangedAsync();
        Invalidate();
        return true;
    }

    public void CloseDrawShapeFromUi() => CloseDrawShape();

    private bool TryBeginCurveHandleDrag(float wx, float wy)
    {
        if (ToolMode != CanvasToolMode.Select && ToolMode != CanvasToolMode.Curve && ToolMode != CanvasToolMode.Arc) return false;
        if (_primaryIndex is not int si || si < 0 || si >= _pieces.Count) return false;
        var handle = PiecePathBuilder.HitCurveHandle(_pieces[si], wx, wy, _viewport.Scale);
        if (handle is null) return false;
        PushHistory(si);
        _dragging = true;
        _dragCurveEdge = handle.Value.EdgeIndex;
        _dragCurveHandle = handle.Value.HandleIndex;
        return true;
    }

    private void HandleMeasureClick(float wx, float wy)
    {
        if (_measureAx is null || _measureAy is null)
        {
            _measureAx = wx;
            _measureAy = wy;
            _measureBx = _measureBy = null;
        }
        else if (_measureBx is null || _measureBy is null)
        {
            _measureBx = wx;
            _measureBy = wy;
        }
        else
        {
            _measureAx = wx;
            _measureAy = wy;
            _measureBx = _measureBy = null;
        }

        Invalidate();
        _ = NotifyHostChangedAsync();
    }

    private CanvasMeasureOverlay? BuildMeasureOverlay()
    {
        if (ToolMode != CanvasToolMode.Measure && _measureAx is null)
            return null;

        return new CanvasMeasureOverlay
        {
            Ax = _measureAx,
            Ay = _measureAy,
            Bx = _measureBx,
            By = _measureBy,
            CursorX = ToolMode == CanvasToolMode.Measure ? _measureCursorX : null,
            CursorY = ToolMode == CanvasToolMode.Measure ? _measureCursorY : null,
        };
    }

    private void PushHistory(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= _pieces.Count) return;
        _history.Push(_pieces[pieceIndex], pieceIndex);
    }

    private async Task SelectAsync(int index)
    {
        _primaryIndex = index;
        _selectedVertexIndex = null;
        _selectedEdgeIndex = null;
        if (EditorOptions.SymmetryEnabled && index >= 0 && index < _pieces.Count)
            EditorOptions.SymmetryAxisWorldX = CanvasSymmetryHelper.ComputeAxisWorldX(_pieces[index]);
        var set = new HashSet<int> { index };
        if (_onSelectedChanged is not null)
            await _onSelectedChanged(index, set);
        RequestInvalidate(force: true);
    }

    private async Task NotifyPiecesChangedAsync()
    {
        if (_onPiecesChanged is not null)
            await _onPiecesChanged();
        await NotifyHostChangedAsync();
    }

    private async Task NotifyHostChangedAsync()
    {
        if (_onHostChanged is not null)
            await _onHostChanged();
    }

    private void RequestInvalidate(bool force = false)
    {
        if (_view is null) return;

        if (force || (!_panning && !_dragging))
        {
            _invalidatePending = false;
            _lastInvalidateMs = Environment.TickCount64;
            SafeInvalidate();
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastInvalidateMs >= MoveThrottleMs)
        {
            _lastInvalidateMs = now;
            _invalidatePending = false;
            SafeInvalidate();
            return;
        }

        if (_invalidatePending) return;
        _invalidatePending = true;
        _ = FlushInvalidateAsync();
    }

    private void SafeInvalidate()
    {
        try { _view?.InvalidateSurface(); }
        catch { /* disposed */ }
    }

    private async Task FlushInvalidateAsync()
    {
        try
        {
            var delay = MoveThrottleMs - (int)(Environment.TickCount64 - _lastInvalidateMs);
            if (delay > 0)
                await Task.Delay(delay);

            if (!_panning && !_dragging)
            {
                _invalidatePending = false;
                return;
            }

            _invalidatePending = false;
            _lastInvalidateMs = Environment.TickCount64;
            SafeInvalidate();
        }
        catch
        {
            _invalidatePending = false;
        }
    }

    private sealed record DomRect(double X, double Y, double Width, double Height);
}
