using PatternPro.Desktop.Services;
using SkiaSharp.Views.Maui.Controls;

namespace PatternPro.Desktop;

public partial class MainPage : ContentPage
{
    private readonly SKCanvasView _nativeCanvas;

    public MainPage(DesktopCanvasHost canvasHost)
    {
        InitializeComponent();

        _nativeCanvas = new SKCanvasView
        {
            IsVisible = false,
            BackgroundColor = Colors.Transparent,
            InputTransparent = false,
        };

        RootGrid.Children.Add(_nativeCanvas);
        canvasHost.Attach(_nativeCanvas);
    }
}
