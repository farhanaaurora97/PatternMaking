using SkiaSharp;

namespace PatternPro.Desktop.Canvas;

public sealed class CanvasViewport
{
    public float PanX { get; set; }
    public float PanY { get; set; } = 20;
    public float Scale { get; set; } = 1f;

    public void FitToBounds(SKRect bounds, float width, float height, float padding = 48)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || width <= 0 || height <= 0)
            return;

        var sx = (width - padding * 2) / bounds.Width;
        var sy = (height - padding * 2) / bounds.Height;
        Scale = Math.Clamp(Math.Min(sx, sy), 0.05f, 8f);
        PanX = (width - bounds.Width * Scale) / 2f - bounds.Left * Scale;
        PanY = (height - bounds.Height * Scale) / 2f - bounds.Top * Scale;
    }

    public void ZoomAt(float factor, float screenX, float screenY)
    {
        var worldX = (screenX - PanX) / Scale;
        var worldY = (screenY - PanY) / Scale;
        Scale = Math.Clamp(Scale * factor, 0.05f, 12f);
        PanX = screenX - worldX * Scale;
        PanY = screenY - worldY * Scale;
    }
}
