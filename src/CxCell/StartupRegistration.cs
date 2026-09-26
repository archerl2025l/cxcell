using Microsoft.Win32;

namespace CxCell;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CxCellWatcher";

    public static void Install(string watcherExecutablePath, string overlayExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(watcherExecutablePath))
            throw new ArgumentException("Watcher executable path is required.", nameof(watcherExecutablePath));
        if (string.IsNullOrWhiteSpace(overlayExecutablePath))
            throw new ArgumentException("Overlay executable path is required.", nameof(overlayExecutablePath));

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open the current-user Run registry key.");

        var command = $"\"{watcherExecutablePath}\" --watcher --overlay-exe \"{overlayExecutablePath}\"";
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public static void Uninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
