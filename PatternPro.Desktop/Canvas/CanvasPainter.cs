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
        return (bounds.Width, bounds.Height, PiecePathBuilder.Perimeter(piece));
    }

    public static void Paint(
        SKCanvas canvas,
        SKImageInfo info,
        IEnumerable<PieceDefinition> pieces,
        CanvasViewport viewport,
        IReadOnlySet<int> selectedIndices,
        int? primaryIndex,
        CanvasLayerOptions layers,
        CanvasDrawOverlay? drawOverlay = null,
        CanvasMeasureOverlay? measureOverlay = null,
        CanvasEditorOverlay? editorOverlay = null)
    {
        canvas.Clear(SKColors.Transparent);

        canvas.Save();
        canvas.Translate(viewport.PanX, viewport.PanY);
        canvas.Scale(viewport.Scale);

        var pieceList = pieces as IList<PieceDefinition> ?? pieces.ToList();

        for (var i = 0; i < pieceList.Count; i++)
            DrawPiece(canvas, pieceList[i], selected: selectedIndices.Contains(i), viewport.Scale, layers);

        if (primaryIndex is >= 0 and var si && si < pieceList.Count)
        {
            DrawHandles(canvas, pieceList[si], viewport.Scale, editorOverlay?.SelectedVertexIndex);
            DrawCurveHandles(canvas, pieceList[si], viewport.Scale);
            if (editorOverlay?.WalkSeamEdgeA is int wsa)
                DrawHighlightedEdge(canvas, pieceList[si], wsa, viewport.Scale, new SKColor(234, 88, 12));
            if (editorOverlay?.WalkSeamEdgeB is int wsb)
            {
                var walkColor = editorOverlay.WalkSeam?.Match == true
                    ? new SKColor(22, 163, 74)
                    : new SKColor(220, 38, 38);
                DrawHighlightedEdge(canvas, pieceList[si], wsb, viewport.Scale, walkColor);
            }
            else if (editorOverlay?.HighlightEdgeIndex is int he)
                DrawHighlightedEdge(canvas, pieceList[si], he, viewport.Scale);
        }

        if (drawOverlay is not null)
            DrawInProgress(canvas, drawOverlay, viewport.Scale);

        if (measureOverlay is not null)
            DrawMeasure(canvas, measureOverlay, viewport.Scale);

        if (editorOverlay is not null)
            DrawEditorOverlay(canvas, editorOverlay, pieceList, viewport.Scale);

        canvas.Restore();
    }

    private static void DrawMeasure(SKCanvas canvas, CanvasMeasureOverlay overlay, float scale)
    {
        using var stroke = new SKPaint
        {
            Color = new SKColor(37, 99, 235),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f / scale,
            IsAntialias = true,
        };
        using var dot = new SKPaint
        {
            Color = new SKColor(37, 99, 235),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var textPaint = new SKPaint { Color = new SKColor(30, 64, 175), IsAntialias = true };
        using var font = new SKFont(CanvasFontCache.PieceLabel, Math.Max(9f, 11f / scale));

        var r = 4f / scale;
        if (overlay.Ax is float ax && overlay.Ay is float ay)
            canvas.DrawCircle(ax, ay, r, dot);

        var bx = overlay.Bx ?? overlay.CursorX;
        var by = overlay.By ?? overlay.CursorY;
        if (overlay.Ax is float x1 && overlay.Ay is float y1 && bx is float x2 && by is float y2)
        {
            canvas.DrawLine(x1, y1, x2, y2, stroke);
            canvas.DrawCircle(x2, y2, r, dot);
            var dist = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            canvas.DrawText(CanvasUnits.FormatCm(dist), (x1 + x2) / 2f, (y1 + y2) / 2f - 6f / scale, SKTextAlign.Center, font, textPaint);
        }
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

    private static void DrawHandles(SKCanvas canvas, PieceDefinition piece, float scale, int? selectedVertexIndex)
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
        using var selectedFill = new SKPaint
        {
            Color = new SKColor(37, 99, 235),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        var r = 5f / scale;
        for (var i = 0; i < piece.Points.Count; i++)
        {
            var pt = piece.Points[i];
            if (pt.Length < 2) continue;
            var x = pt[0] + piece.OffsetX;
            var y = pt[1] + piece.OffsetY;
            var fill = selectedVertexIndex == i ? selectedFill : handleFill;
            canvas.DrawCircle(x, y, r, fill);
            canvas.DrawCircle(x, y, r, handleStroke);
        }
    }

    private static void DrawPiece(SKCanvas canvas, PieceDefinition piece, bool selected, float scale, CanvasLayerOptions layers)
    {
        if (piece.Points.Count < 3) return;

        var col = ParseColor(piece.Color);

        if (layers.ShowSeamAllowance && PieceSeamAllowanceHelper.EffectiveSeamAllowance(piece) > 0.01)
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

        if (layers.ShowInternalLines && piece.InternalLines is { Count: > 0 })
        {
            using var guideStroke = new SKPaint
            {
                Color = new SKColor(192, 38, 211, 210),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f / scale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([5f / scale, 4f / scale], 0),
            };
            using var labelPaint = new SKPaint { Color = new SKColor(126, 34, 206), IsAntialias = true };
            using var guideFont = new SKFont(CanvasFontCache.PieceLabel, Math.Max(7f, 9f / scale));

            foreach (var line in piece.InternalLines)
            {
                var x1 = line.X1 + piece.OffsetX;
                var y1 = line.Y1 + piece.OffsetY;
                var x2 = line.X2 + piece.OffsetX;
                var y2 = line.Y2 + piece.OffsetY;
                canvas.DrawLine(x1, y1, x2, y2, guideStroke);
                var mx = (x1 + x2) / 2f;
                var my = (y1 + y2) / 2f;
                canvas.DrawText(line.Label ?? "Guide", mx, my - 4f / scale, SKTextAlign.Center, guideFont, labelPaint);
            }
        }

        if (!layers.ShowLabels || scale <= 0.3f) return;

        var cx = piece.Points.Average(p => p[0]) + piece.OffsetX;
        var cy = piece.Points.Average(p => p[1]) + piece.OffsetY;
        using var text = new SKPaint { Color = new SKColor(51, 65, 85), IsAntialias = true };
        using var font = new SKFont(CanvasFontCache.PieceLabel, Math.Max(8f, 10f * scale));
        canvas.DrawText(piece.Name, (float)cx, (float)cy, SKTextAlign.Center, font, text);
    }

    private static void DrawCurveHandles(SKCanvas canvas, PieceDefinition piece, float scale)
    {
        if (piece.Edges is null) return;

        using var linePaint = new SKPaint
        {
            Color = new SKColor(59, 130, 246, 160),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f / scale,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([4f / scale, 3f / scale], 0),
        };
        using var handleFill = new SKPaint
        {
            Color = new SKColor(37, 99, 235),
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

        var ox = piece.OffsetX;
        var oy = piece.OffsetY;
        var r = 4f / scale;

        for (var i = 0; i < piece.Edges.Count; i++)
        {
            if (!PiecePathBuilder.IsCurved(piece, i)) continue;
            var edge = piece.Edges[i];
            var a = piece.Points[i];
            var b = piece.Points[(i + 1) % piece.Points.Count];
            if (edge.C1 is not { Length: >= 2 } c1) continue;

            var ax = a[0] + ox;
            var ay = a[1] + oy;
            var bx = b[0] + ox;
            var by = b[1] + oy;
            var c1x = c1[0] + ox;
            var c1y = c1[1] + oy;

            canvas.DrawLine(ax, ay, c1x, c1y, linePaint);
            canvas.DrawLine(c1x, c1y, bx, by, linePaint);
            canvas.DrawCircle(c1x, c1y, r, handleFill);
            canvas.DrawCircle(c1x, c1y, r, handleStroke);

            if (edge.Kind == "cubic" && edge.C2 is { Length: >= 2 } c2)
            {
                var c2x = c2[0] + ox;
                var c2y = c2[1] + oy;
                canvas.DrawLine(bx, by, c2x, c2y, linePaint);
                canvas.DrawCircle(c2x, c2y, r, handleFill);
                canvas.DrawCircle(c2x, c2y, r, handleStroke);
            }
        }
    }

    private static SKPath BuildPath(PieceDefinition piece) => PiecePathBuilder.BuildPath(piece);

    private static void DrawHighlightedEdge(SKCanvas canvas, PieceDefinition piece, int edgeIndex, float scale, SKColor? color = null)
    {
        var pts = PiecePathBuilder.TessellateEdge(piece, edgeIndex);
        if (pts.Count < 2) return;

        using var paint = new SKPaint
        {
            Color = color ?? new SKColor(37, 99, 235, 220),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f / scale,
            IsAntialias = true,
        };

        using var path = new SKPath();
        path.MoveTo(pts[0].X + piece.OffsetX, pts[0].Y + piece.OffsetY);
        for (var i = 1; i < pts.Count; i++)
            path.LineTo(pts[i].X + piece.OffsetX, pts[i].Y + piece.OffsetY);
        canvas.DrawPath(path, paint);
    }

    private static void DrawEditorOverlay(
        SKCanvas canvas,
        CanvasEditorOverlay overlay,
        IList<PieceDefinition> pieces,
        float scale)
    {
        if (overlay.SymmetryAxisWorldX is float axisX)
        {
            var bounds = ComputeBounds(pieces);
            using var axisPaint = new SKPaint
            {
                Color = new SKColor(124, 58, 237, 180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / scale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([8f / scale, 6f / scale], 0),
            };
            canvas.DrawLine(axisX, bounds.Top - 20, axisX, bounds.Bottom + 20, axisPaint);
        }

        if (overlay.SnapX is float sx && overlay.SnapY is float sy && overlay.SnapKind != SnapKind.None)
        {
            var color = overlay.SnapKind switch
            {
                SnapKind.Vertex => new SKColor(220, 38, 38),
                SnapKind.Midpoint => new SKColor(217, 119, 6),
                SnapKind.Edge => new SKColor(37, 99, 235),
                _ => new SKColor(100, 116, 139),
            };
            using var snapPaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / scale,
                IsAntialias = true,
            };
            var r = 6f / scale;
            canvas.DrawLine(sx - r, sy, sx + r, sy, snapPaint);
            canvas.DrawLine(sx, sy - r, sx, sy + r, snapPaint);
        }

        if (overlay.InternalLineStartX is float ilx && overlay.InternalLineStartY is float ily)
        {
            using var stroke = new SKPaint
            {
                Color = new SKColor(192, 38, 211, 220),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f / scale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash([5f / scale, 4f / scale], 0),
            };
            using var dot = new SKPaint
            {
                Color = new SKColor(192, 38, 211),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            canvas.DrawCircle(ilx, ily, 4f / scale, dot);
            if (overlay.InternalLineCursorX is float cx && overlay.InternalLineCursorY is float cy)
                canvas.DrawLine(ilx, ily, cx, cy, stroke);
        }

        if (overlay.WalkSeam is { } walk && overlay.PrimaryPieceIndex is int wpi
            && wpi >= 0 && wpi < pieces.Count)
        {
            var walkPiece = pieces[wpi];
            var mx = (float)(walkPiece.Points.Average(p => p[0]) + walkPiece.OffsetX);
            var my = (float)(walkPiece.Points.Min(p => p[1]) + walkPiece.OffsetY - 24f / scale);
            var label = walk.Match
                ? $"Walk seam OK — Δ {CanvasUnits.FormatCm(Math.Abs(walk.DeltaPx))}"
                : $"Walk seam Δ {CanvasUnits.FormatCm(Math.Abs(walk.DeltaPx))} ({walk.DeltaPercent:0.#}%)";
            using var bg = new SKPaint
            {
                Color = walk.Match ? new SKColor(22, 163, 74, 230) : new SKColor(220, 38, 38, 230),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var walkFont = new SKFont(CanvasFontCache.PieceLabel, Math.Max(8f, 10f / scale));
            var width = walkFont.MeasureText(label, textPaint) + 10f / scale;
            var height = 16f / scale;
            canvas.DrawRoundRect(mx - width / 2f, my - height / 2f, width, height, 3f / scale, 3f / scale, bg);
            canvas.DrawText(label, mx, my + 4f / scale, SKTextAlign.Center, walkFont, textPaint);
        }

        if (overlay.PrimaryPieceIndex is not int pi || pi < 0 || pi >= pieces.Count
            || overlay.LiveMeasurements is not { } live)
            return;

        var piece = pieces[pi];
        if (live.IsLegPiece)
        {
            DrawMeasurementLabel(canvas, piece, live.WaistEdgeIndex, "Waist " + CanvasUnits.FormatCm(live.WaistArcPx), scale, new SKColor(16, 185, 129));
            DrawMeasurementLabel(canvas, piece, live.InseamEdgeIndex, "Inseam " + CanvasUnits.FormatCm(live.InseamPx), scale, new SKColor(2, 132, 199));
        }

        if (overlay.HighlightEdgeIndex is int he && he != live.WaistEdgeIndex && he != live.InseamEdgeIndex)
            DrawMeasurementLabel(canvas, piece, he, "Edge " + CanvasUnits.FormatCm(live.SelectedEdgePx), scale, new SKColor(37, 99, 235));
    }

    private static void DrawMeasurementLabel(
        SKCanvas canvas,
        PieceDefinition piece,
        int? edgeIndex,
        string label,
        float scale,
        SKColor color)
    {
        if (edgeIndex is not int ei) return;
        var pts = PiecePathBuilder.TessellateEdge(piece, ei);
        if (pts.Count == 0) return;

        var mx = pts.Average(p => p.X) + piece.OffsetX;
        var my = pts.Average(p => p.Y) + piece.OffsetY;
        using var bg = new SKPaint { Color = color.WithAlpha(230), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(CanvasFontCache.PieceLabel, Math.Max(8f, 10f / scale));
        var width = font.MeasureText(label, textPaint) + 8f / scale;
        var height = 14f / scale;
        canvas.DrawRoundRect(
            mx - width / 2f,
            my - height / 2f - 10f / scale,
            width,
            height,
            3f / scale,
            3f / scale,
            bg);
        canvas.DrawText(label, mx, my - 8f / scale, SKTextAlign.Center, font, textPaint);
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
