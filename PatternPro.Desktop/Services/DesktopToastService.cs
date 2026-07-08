namespace PatternPro.Desktop.Services;

public enum DesktopToastType
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record DesktopToast(
    Guid Id,
    string Title,
    string Message,
    DesktopToastType Type,
    string Icon);

public sealed class DesktopToastService
{
    private readonly List<DesktopToast> _toasts = [];
    private readonly object _lock = new();

    public event Action? Changed;

    public IReadOnlyList<DesktopToast> Toasts
    {
        get
        {
            lock (_lock)
                return _toasts.ToList();
        }
    }

    public void Show(string title, string message, DesktopToastType type = DesktopToastType.Success, string icon = "✓")
    {
        var toast = new DesktopToast(Guid.NewGuid(), title, message, type, icon);
        lock (_lock)
            _toasts.Add(toast);
        Changed?.Invoke();
        _ = AutoDismissAsync(toast.Id);
    }

    public void Dismiss(Guid id)
    {
        lock (_lock)
            _toasts.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
    }

    private async Task AutoDismissAsync(Guid id)
    {
        await Task.Delay(4000);
        Dismiss(id);
    }
}
