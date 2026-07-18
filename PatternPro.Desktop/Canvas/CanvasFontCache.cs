using SkiaSharp;

namespace PatternPro.Desktop.Canvas;

internal static class CanvasFontCache
{
    public static readonly SKTypeface PieceLabel =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default;

    public static readonly SKTypeface NestLabel =
        SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold) ?? SKTypeface.Default;
}
