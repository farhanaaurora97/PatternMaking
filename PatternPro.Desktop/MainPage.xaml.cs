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

        blazorWebView.ZIndex = 0;
        _nativeCanvas.ZIndex = 1;
        RootGrid.Children.Add(_nativeCanvas);
        canvasHost.Attach(_nativeCanvas);
    }
}
