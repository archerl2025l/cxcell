using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CxCell;

public sealed class CodexWindowLocator
{
    public bool TryGetForegroundCodexWindow(out WindowBounds bounds)
    {
        bounds = default;
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
            return false;

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var processName = process.ProcessName;
            var title = process.MainWindowTitle;

            var looksLikeCodex =
                processName.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("codex", StringComparison.OrdinalIgnoreCase);

            if (!looksLikeCodex || !GetWindowRect(hwnd, out var rect))
                return false;

            bounds = new WindowBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom);
