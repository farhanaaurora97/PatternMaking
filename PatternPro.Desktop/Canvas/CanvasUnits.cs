namespace PatternPro.Desktop.Canvas;

/// <summary>Canvas scale: 3 px = 1 cm (matches drafting + DXF export).</summary>
internal static class CanvasUnits
{
    public const double PixelsPerCm = 3.0;

    public static double ToCm(double pixels) => pixels / PixelsPerCm;

    public static double ToPixels(double cm) => cm * PixelsPerCm;

    public static string FormatCm(double pixels) =>
        pixels <= 0 ? "—" : $"{ToCm(pixels):0.##} cm";

    public static string FormatCmFromCm(double cm) =>
        cm <= 0 ? "—" : $"{cm:0.##} cm";
}
