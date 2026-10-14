namespace PatternPro.Desktop.Services;

/// <summary>Cross-component shell actions (topbar → dashboard, etc.).</summary>
public sealed class DesktopShellEvents
{
    public event Action? NewPatternRequested;

    public void RequestNewPattern() => NewPatternRequested?.Invoke();
}
