using System.Runtime.InteropServices;

namespace PatternPro.Desktop;

internal static class DesktopStartupError
{
    public static void ShowAndExit(Exception ex)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PatternPro");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "startup-error.txt");
        File.WriteAllText(logPath, ex.ToString());

        var message =
            "PatternPro could not start.\n\n" +
            ex.Message +
            "\n\nIf this is a team install: make sure the main PC is ON and on the same Wi-Fi.\n" +
            "Ask your admin for an updated ZIP if the server IP changed.\n\n" +
            $"Full details:\n{logPath}";

        ShowMessage("PatternPro Desktop", message);
        Environment.Exit(1);
    }

    private static void ShowMessage(string title, string message)
    {
        if (OperatingSystem.IsWindows())
            MessageBoxW(IntPtr.Zero, message, title, 0x10); // MB_ICONERROR
        else
            Console.Error.WriteLine($"{title}: {message}");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
