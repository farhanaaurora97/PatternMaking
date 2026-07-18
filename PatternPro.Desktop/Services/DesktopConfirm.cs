using Microsoft.JSInterop;

namespace PatternPro.Desktop.Services;

internal static class DesktopConfirm
{
    public static Task<bool> DeleteAsync(IJSRuntime js, string itemLabel) =>
        js.InvokeAsync<bool>("confirm", $"Delete {itemLabel}? This cannot be undone.").AsTask();
}
