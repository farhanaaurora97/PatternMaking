using Pattern.Core.Model;
using SkiaSharp;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasPainter
{
    public static SKRect ComputeBounds(IEnumerable<PieceDefinition> pieces)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        var any = false;

        foreach (var piece in pieces)
        {
            foreach (var pt in piece.Points)
            {
                if (pt.Length < 2) continue;
                var x = pt[0] + piece.OffsetX;
                var y = pt[1] + piece.OffsetY;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                any = true;
            }
        }

        return any ? new SKRect(minX, minY, maxX, maxY) : SKRect.Empty;
    }

    public static (float Width, float Height, float Perimeter) MeasurePiece(PieceDefinition piece)
    {
        if (piece.Points.Count < 2) return (0, 0, 0);
        var bounds = ComputeBounds([piece]);
        var perim = 0f;
        for (var i = 0; i < piece.Points.Count; i++)
        {
            var a = piece.Points[i];
            var b = piece.Points[(i + 1) % piece.Points.Count];
            if (a.Length < 2 || b.Length < 2) continue;
            var ax = a[0] + piece.OffsetX;
            var ay = a[1] + piece.OffsetY;
            var bx = b[0] + piece.OffsetX;
            var by = b[1] + piece.OffsetY;
            var dx = bx - ax;
            var dy = by - ay;
            perim += MathF.Sqrt(dx * dx + dy * dy);
        }
        return (bounds.Width, bounds.Height, perim);
    }

    public static void Paint(
        SKCanvas canvas,
        SKImageInfo info,
        IEnumerable<PieceDefinition> pieces,
        CanvasViewport viewport,
        int? selectedIndex,
        CanvasLayerOptions layers,
        CanvasDrawOverlay? drawOverlay = null)
    {
        canvas.Clear(SKColors.Transparent);

        canvas.Save();
        canvas.Translate(viewport.PanX, viewport.PanY);
        canvas.Scale(viewport.Scale);

        var pieceList = pieces as IList<PieceDefinition> ?? pieces.ToList();

        for (var i = 0; i < pieceList.Count; i++)
            DrawPiece(canvas, pieceList[i], selected: selectedIndex == i, viewport.Scale, layers);

        if (selectedIndex is >= 0 and var si && si < pieceList.Count)
            DrawHandles(canvas, pieceList[si], viewport.Scale);

        if (drawOverlay is not null)
            DrawInProgress(canvas, drawOverlay, viewport.Scale);

        canvas.Restore();
    }

    private static void DrawInProgress(SKCanvas canvas, CanvasDrawOverlay overlay, float scale)
    {
        if (overlay.Points.Count == 0 && overlay.CursorX is null) return;

        using var stroke = new SKPaint
        {
            Color = new SKColor(185, 28, 28),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f / scale,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([6f / scale, 4f / scale], 0),
        };
        using var dot = new SKPaint
        {
            Color = new SKColor(185, 28, 28),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        var pts = overlay.Points;
        if (pts.Count > 0)
        {
            using var path = new SKPath();
            path.MoveTo(pts[0].X, pts[0].Y);
            for (var i = 1; i < pts.Count; i++)
                path.LineTo(pts[i].X, pts[i].Y);
            if (overlay.CursorX is float cx && overlay.CursorY is float cy)
                path.LineTo(cx, cy);
            canvas.DrawPath(path, stroke);

            var r = 4f / scale;
            foreach (var (x, y) in pts)
                canvas.DrawCircle(x, y, r, dot);
        }

        if (overlay.CursorX is float curX && overlay.CursorY is float curY)
            canvas.DrawCircle(curX, curY, 3f / scale, dot);
    }

    private static void DrawHandles(SKCanvas canvas, PieceDefinition piece, float scale)
    {
        using var handleFill = new SKPaint
        {
            Color = new SKColor(220, 38, 38),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var handleStroke = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f / scale,
            IsAntialias = true,
        };
        var r = 5f / scale;
        foreach (var pt in piece.Points)
        {
            if (pt.Length < 2) continue;
            var x = pt[0] + piece.OffsetX;
            var y = pt[1] + piece.OffsetY;
            canvas.DrawCircle(x, y, r, handleFill);
            canvas.DrawCircle(x, y, r, handleStroke);
        }
    }

    private static void DrawPiece(SKCanvas canvas, PieceDefinition piece, bool selected, float scale, CanvasLayerOptions layers)
    {
        if (piece.Points.Count < 3) return;

        var col = ParseColor(piece.Color);

        if (layers.ShowSeamAllowance && piece.SeamAllowance > 0.01)
        {
            using var saStroke = new SKPaint
            {
                Color = new SKColor(185, 28, 28, 217),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)(1.2 + piece.SeamAllowance * 0.15) / scale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([7f / scale, 5f / scale], 0),
            };
            using var saPath = BuildPath(piece);
            canvas.DrawPath(saPath, saStroke);
        }

        using var fill = new SKPaint
        {
            Color = col.WithAlpha(selected ? (byte)40 : (byte)20),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var stroke = new SKPaint
        {
            Color = selected ? col : new SKColor(23, 23, 23, 72),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (selected ? 2.5f : 1.8f) / scale,
            IsAntialias = true,
        };

        using var path = BuildPath(piece);
        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);

        if (layers.ShowGrain && piece.Cf is { Count: >= 2 })
        {
            using var cfPaint = new SKPaint
            {
                Color = new SKColor(185, 28, 28),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f / scale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([5f / scale, 3f / scale], 0),
            };
            var c0 = piece.Cf[0];
            var c1 = piece.Cf[^1];
            canvas.DrawLine(c0[0] + piece.OffsetX, c0[1] + piece.OffsetY,
                c1[0] + piece.OffsetX, c1[1] + piece.OffsetY, cfPaint);
        }

        if (layers.ShowGrain && piece.Grain is { Count: >= 2 })
        {
            var g0 = piece.Grain[0];
            var g1 = piece.Grain[1];
            var x1 = g0[0] + piece.OffsetX;
            var y1 = g0[1] + piece.OffsetY;
            var x2 = g1[0] + piece.OffsetX;
            var y2 = g1[1] + piece.OffsetY;
            using var grainPaint = new SKPaint
            {
                Color = col.WithAlpha(128),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / scale,
                IsAntialias = true,
            };
            canvas.DrawLine(x1, y1, x2, y2, grainPaint);
            DrawArrow(canvas, x1, y1, x2, y2, col, scale);
        }

        if (layers.ShowNotches && piece.Notches is { Count: > 0 })
        {
            using var notchPaint = new SKPaint
            {
                Color = new SKColor(153, 27, 27),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / scale,
                IsAntialias = true,
            };
            var n = 5f * scale;
            foreach (var notch in piece.Notches)
            {
                if (notch.Length < 2) continue;
                var sx = notch[0] + piece.OffsetX;
                var sy = notch[1] + piece.OffsetY;
                canvas.DrawLine(sx - n, sy, sx + n, sy, notchPaint);
                canvas.DrawLine(sx, sy - n, sx, sy + n, notchPaint);
            }
        }

        if (!layers.ShowLabels || scale <= 0.3f) return;

        var cx = piece.Points.Average(p => p[0]) + piece.OffsetX;
        var cy = piece.Points.Average(p => p[1]) + piece.OffsetY;
        using var text = new SKPaint { Color = new SKColor(51, 65, 85), IsAntialias = true };
        using var font = new SKFont(CanvasFontCache.PieceLabel, Math.Max(8f, 10f * scale));
        canvas.DrawText(piece.Name, (float)cx, (float)cy, SKTextAlign.Center, font, text);
    }

    private static SKPath BuildPath(PieceDefinition piece)
    {
        var path = new SKPath();
        var pts = piece.Points;
        if (pts.Count < 3) return path;

        var first = pts[0];
        path.MoveTo(first[0] + piece.OffsetX, first[1] + piece.OffsetY);
        for (var i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            path.LineTo(p[0] + piece.OffsetX, p[1] + piece.OffsetY);
        }
        path.Close();
        return path;
    }

    private static void DrawArrow(SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor col, float scale)
    {
        var angle = MathF.Atan2(y2 - y1, x2 - x1);
        var len = 8f / scale;
        using var paint = new SKPaint { Color = col, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var path = new SKPath();
        path.MoveTo(x2, y2);
        path.LineTo(x2 - len * MathF.Cos(angle - 0.4f), y2 - len * MathF.Sin(angle - 0.4f));
        path.LineTo(x2 - len * MathF.Cos(angle + 0.4f), y2 - len * MathF.Sin(angle + 0.4f));
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static SKColor ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return new SKColor(30, 64, 175);
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return new SKColor(30, 64, 175);
    }
}
