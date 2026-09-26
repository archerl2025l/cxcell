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

            if (!CodexHostMatcher.IsSupportedHost(processName, title))
                return false;

            // GetWindowRect includes non-client/resizable frame offsets that vary between
            // maximized and restored windows. The left rail is part of the client area, so
            // anchor against client coordinates converted to screen pixels.
            if (!GetClientRect(hwnd, out var clientRect))
                return false;

            var topLeft = new Point { X = clientRect.Left, Y = clientRect.Top };
            var bottomRight = new Point { X = clientRect.Right, Y = clientRect.Bottom };
            if (!ClientToScreen(hwnd, ref topLeft) || !ClientToScreen(hwnd, ref bottomRight))
                return false;

            bounds = new WindowBounds(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
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
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}

public readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom);
