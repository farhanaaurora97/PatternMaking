using CoreModel = Pattern.Core.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.Controls;
using Pattern.Web.Model;
using PatternPro.Desktop.Canvas;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

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
    private int? _selectedIndex;
    private Func<Task>? _onPiecesChanged;
    private Func<int?, Task>? _onSelectedChanged;
    private Func<Task>? _onHostChanged;
    private readonly CanvasPieceHistory _history = new();

    private bool _panning;
    private bool _dragging;
    private float _lastX, _lastY;
    private int? _dragVertex;
    private float _pieceStartWx, _pieceStartWy;
    private int _pieceOrigOx, _pieceOrigOy;
    private long _lastInvalidateMs;
    private bool _invalidatePending;

    private readonly List<(float X, float Y)> _drawPoints = [];
    private float? _drawCursorX, _drawCursorY;
    private PendingNewPiece? _pendingNewPiece;

    private IReadOnlyList<int[]> _nestBasePoints = [];
    private IReadOnlyList<NestSizeViewModel> _nestSizes = [];
    private IReadOnlyList<bool> _nestVisible = [];
    private float _nestScale = 1f;

    public double SlotWidth { get; private set; }
    public double SlotHeight { get; private set; }
    public CanvasLayerOptions Layers { get; } = new();
    public CanvasToolMode ToolMode { get; set; } = CanvasToolMode.Select;
    public float ZoomPercent => _viewport.Scale * 100f;
    public bool CanUndo => _history.CanUndo;
    public PendingNewPiece? PendingNewPiece => _pendingNewPiece;
    public int DrawPointCount => _drawPoints.Count;

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

    public void ConfigurePattern(
        IList<CoreModel.PieceDefinition> pieces,
        CanvasViewport viewport,
        int? selectedIndex,
        Func<Task>? onPiecesChanged,
        Func<int?, Task>? onSelectedChanged,
        Func<Task>? onHostChanged = null)
    {
        _pieces = pieces;
        _viewport = viewport;
        _selectedIndex = selectedIndex;
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
        _selectedIndex = restoredIndex;
        if (_onSelectedChanged is not null)
            await _onSelectedChanged(restoredIndex);
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
                CanvasPainter.Paint(e.Surface.Canvas, e.Info, _pieces, _viewport, _selectedIndex, Layers, overlay);
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

        var (wx, wy) = CanvasGeometryHelper.ScreenToWorld(x, y, _viewport);

        if (ToolMode == CanvasToolMode.Draw)
        {
            HandleDrawClick(x, y, wx, wy);
            return;
        }

        if (ToolMode == CanvasToolMode.Point)
        {
            if (_selectedIndex is not int pi || pi < 0 || pi >= _pieces.Count) return;
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
            if (_selectedIndex is not int ni || ni < 0 || ni >= _pieces.Count) return;
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

        if (_selectedIndex is int si && si >= 0 && si < _pieces.Count)
        {
            var piece = _pieces[si];
            var vertex = CanvasGeometryHelper.HitVertex(piece, wx, wy, _viewport.Scale);
            if (vertex is not null)
            {
                PushHistory(si);
                _dragVertex = vertex;
                _dragging = true;
                return;
            }

            if (CanvasGeometryHelper.HitPieceBody(piece, wx, wy))
            {
                PushHistory(si);
                _dragging = true;
                _pieceStartWx = wx;
                _pieceStartWy = wy;
                _pieceOrigOx = piece.OffsetX;
                _pieceOrigOy = piece.OffsetY;
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

    private void OnPointerMove(float x, float y)
    {
        if (ToolMode == CanvasToolMode.Draw)
        {
            var (wx, wy) = CanvasGeometryHelper.ScreenToWorld(x, y, _viewport);
            _drawCursorX = wx;
            _drawCursorY = wy;
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
            var (lwx, lwy) = CanvasGeometryHelper.ScreenToWorld(x, y, _viewport);
            _lastWorldX = lwx;
            _lastWorldY = lwy;
        }

        if (!_dragging || _selectedIndex is not int si || si < 0 || si >= _pieces.Count)
            return;

        var piece = _pieces[si];
        var (wx2, wy2) = CanvasGeometryHelper.ScreenToWorld(x, y, _viewport);

        if (_dragVertex is int vi && vi < piece.Points.Count)
        {
            var pt = piece.Points[vi];
            pt[0] = (int)Math.Round(wx2 - piece.OffsetX);
            pt[1] = (int)Math.Round(wy2 - piece.OffsetY);
            RequestInvalidate();
            return;
        }

        if (_dragVertex is null)
        {
            piece.OffsetX = _pieceOrigOx + (int)Math.Round(wx2 - _pieceStartWx);
            piece.OffsetY = _pieceOrigOy + (int)Math.Round(wy2 - _pieceStartWy);
            RequestInvalidate();
        }
    }

    private async Task OnPointerUpAsync(float x, float y)
    {
        var edited = _dragging && (_dragVertex is not null || _selectedIndex is not null);
        _panning = false;
        _dragging = false;
        _dragVertex = null;
        _invalidatePending = false;
        RequestInvalidate(force: true);

        if (edited && _onPiecesChanged is not null)
            await _onPiecesChanged();
    }

    private void HandleDrawClick(float sx, float sy, float wx, float wy)
    {
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

        _drawPoints.Add((wx, wy));
        _drawCursorX = wx;
        _drawCursorY = wy;
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
        if (_selectedIndex is not int si || si < 0 || si >= _pieces.Count) return false;
        var piece = _pieces[si];
        if (piece.Points.Count <= 3) return false;
        var vi = CanvasGeometryHelper.HitVertex(piece, _lastWorldX, _lastWorldY, _viewport.Scale);
        if (vi is null) return false;
        PushHistory(si);
        piece.Points.RemoveAt(vi.Value);
        await NotifyPiecesChangedAsync();
        Invalidate();
        return true;
    }

    public void CloseDrawShapeFromUi() => CloseDrawShape();

    private void PushHistory(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= _pieces.Count) return;
        _history.Push(_pieces[pieceIndex], pieceIndex);
    }

    private async Task SelectAsync(int index)
    {
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        if (_onSelectedChanged is not null)
            await _onSelectedChanged(index);
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
