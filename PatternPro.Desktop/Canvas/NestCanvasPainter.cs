using Pattern.Web.Model;
using SkiaSharp;

namespace PatternPro.Desktop.Canvas;

internal static class NestCanvasPainter
{
    private static readonly float[] GradeScales = [0.82f, 0.91f, 1f, 1.09f, 1.18f, 1.28f];

    public static void Paint(
        SKCanvas canvas,
        SKImageInfo info,
        IReadOnlyList<int[]> basePoints,
        IReadOnlyList<NestSizeViewModel> sizes,
        IReadOnlyList<bool> visible,
        float nestScale)
    {
        canvas.Clear(SKColors.White);

        if (basePoints.Count < 3 || sizes.Count == 0)
        {
            using var emptyFont = new SKFont { Size = 14 };
            using var emptyPaint = new SKPaint { Color = new SKColor(0x94, 0xA3, 0xB8), IsAntialias = true };
            canvas.DrawText("No base piece geometry.", 24, info.Height / 2f, SKTextAlign.Left, emptyFont, emptyPaint);
            return;
        }

        var w = info.Width;
        var h = info.Height;
        var cx = w / 2f;
        var cy = h / 2f + 20f;
        var sc = Math.Min(w, h) / 420f * nestScale;

        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        for (var si = 0; si < sizes.Count && si < GradeScales.Length; si++)
        {
            if (visible.Count > si && !visible[si]) continue;

            var gsc = GradeScales[si];
            var color = ParseColor(sizes[si].Color);
            var isBase = sizes[si].IsBase;

            using var path = new SKPath();
            for (var i = 0; i < basePoints.Count; i++)
            {
                var pt = basePoints[i];
                var wx = (pt[0] - 178) * gsc * sc + cx;
                var wy = (pt[1] - 220) * gsc * sc + cy;
                if (i == 0) path.MoveTo(wx, wy);
                else path.LineTo(wx, wy);
            }
            path.Close();

            stroke.Color = color;
            stroke.StrokeWidth = isBase ? 2.5f : 1.2f;
            stroke.PathEffect = isBase ? null : SKPathEffect.CreateDash([4f, 3f], 0);
            canvas.DrawPath(path, stroke);

            var lx = (basePoints[0][0] - 178) * gsc * sc + cx + 4;
            var ly = (basePoints[0][1] - 220) * gsc * sc + cy + 4;
            using var labelFont = new SKFont(CanvasFontCache.NestLabel, Math.Max(9f, 10f * sc));
            fill.Color = color;
            canvas.DrawText(LabelShort(sizes[si].Label), lx, ly, SKTextAlign.Left, labelFont, fill);
        }
    }

    private static string LabelShort(string label)
    {
        var idx = label.IndexOf('(');
        return idx > 0 ? label[..idx].Trim() : label;
    }

    private static SKColor ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return SKColors.Gray;
        return SKColor.TryParse(hex, out var c) ? c : SKColors.Gray;
    }
}
