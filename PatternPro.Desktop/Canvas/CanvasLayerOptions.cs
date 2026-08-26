namespace PatternPro.Desktop.Canvas;

public enum CanvasToolMode
{
    Select,
    Pan,
    Point,
    DeletePoint,
    Notch,
    Draw,
    Curve,
    Smooth,
    Measure,
    Arc,
    InternalLine,
    WalkSeam,
}

public sealed class CanvasLayerOptions
{
    public bool ShowSeamAllowance { get; set; } = true;
    public bool ShowGrain { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public bool ShowNotches { get; set; } = true;
    public bool ShowInternalLines { get; set; } = true;
}
