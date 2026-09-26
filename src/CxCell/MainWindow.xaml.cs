using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CxCell;

public partial class MainWindow : Window
{
    // The Codex/ChatGPT left rail uses one visual slot per icon. Batteries occupy the
    // two slots immediately above Help instead of being positioned from the overlay bottom.
    private const double OverlayWidthDip = 44;

    // Desktop rail measurements are expressed in DIPs. On the user's 150% Windows scale
    // these map to ~39 px from the client-left edge and ~102 px from client-bottom,
    // matching the native Help icon center in both maximized and restored windows.
    private const double SidebarIconCenterOffsetDip = 26;
    private const double HelpIconCenterOffsetFromBottomDip = 68;
    private const double NavigationIconPitchDip = 44;

    private readonly CodexUsageClient _usageClient = new();
    private readonly CodexWindowLocator _windowLocator = new();
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly bool _managedLifecycle;

    private QuotaSnapshot _snapshot = new(null, null, null);
    private bool _refreshInProgress;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    public MainWindow(bool managedLifecycle = false)
    {
        _managedLifecycle = managedLifecycle;
        InitializeComponent();

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _positionTimer.Tick += (_, _) => TrackCodexWindow();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _refreshTimer.Tick += async (_, _) => await RefreshUsageAsync();

        Loaded += async (_, _) =>
        {
            _positionTimer.Start();
            _refreshTimer.Start();
            await RefreshUsageAsync();
        };

        Closing += OnClosing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
    }

    private async Task RefreshUsageAsync()
    {
        if (_refreshInProgress || _shutdownStarted)
            return;

        _refreshInProgress = true;
        try
        {
            _snapshot = await _usageClient.ReadAsync(_shutdown.Token);
            RenderBatteries(_snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RenderError(ex.Message);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void TrackCodexWindow()
    {
        if (_managedLifecycle && !CodexHostProcess.IsRunning())
        {
            Close();
            return;
        }

        if (!_windowLocator.TryGetForegroundCodexWindow(out var bounds))
        {
            Opacity = 0;
            IsHitTestVisible = false;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var slotCount = Math.Max(1, BatteryStack.Children.Count);

        // Keep one 48-DIP navigation slot per battery. With two batteries their centers are
        // exactly one and two sidebar-icon pitches above the Help icon center.
        var targetHeightDip = slotCount * NavigationIconPitchDip;
        if (Math.Abs(Height - targetHeightDip) > 0.1)
            Height = targetHeightDip;

        var sidebarCenterXPx = bounds.Left + SidebarIconCenterOffsetDip * dpi.DpiScaleX;
        var helpCenterYPx = bounds.Bottom - HelpIconCenterOffsetFromBottomDip * dpi.DpiScaleY;
        var overlayWidthPx = OverlayWidthDip * dpi.DpiScaleX;

        // For N slots, top = HelpCenter - (N + 0.5) pitches. Therefore the bottom slot
        // center remains exactly one pitch above Help and every additional slot follows
        // the same spacing as native sidebar icons.
        var topPx = helpCenterYPx -
                    (slotCount + 0.5) * NavigationIconPitchDip * dpi.DpiScaleY;
        var leftPx = sidebarCenterXPx - overlayWidthPx / 2;

        Left = leftPx / dpi.DpiScaleX;
        Top = topPx / dpi.DpiScaleY;
        Opacity = 1;
        IsHitTestVisible = true;
    }

    private void RenderBatteries(QuotaSnapshot snapshot)
    {
        BatteryStack.Children.Clear();

        if (snapshot.FiveHour is not null)
            BatteryStack.Children.Add(CreateBatterySlot("5H", snapshot.FiveHour));

        if (snapshot.Weekly is not null)
            BatteryStack.Children.Add(CreateBatterySlot("W", snapshot.Weekly));

        if (BatteryStack.Children.Count == 0)
        {
            BatteryStack.Children.Add(CreateStatusSlot(
                "—",
                "No supported Codex usage window is currently reported.",
                Brushes.White));
        }

        Height = Math.Max(1, BatteryStack.Children.Count) * NavigationIconPitchDip;
        TrackCodexWindow();
    }

    private UIElement CreateBatterySlot(string label, QuotaWindow quota)
    {
        var slot = new Grid
        {
            Width = OverlayWidthDip,
            Height = NavigationIconPitchDip,
            Background = Brushes.Transparent,
            ToolTip = BuildTooltip(label, quota)
        };

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(CreateBatteryBody(quota));

        content.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });

        slot.Children.Add(content);
        return slot;
    }

    private UIElement CreateBatteryBody(QuotaWindow quota)
    {
        var remaining = (int)Math.Round(quota.RemainingPercent);
        var shell = new Grid
        {
            Width = 36,
            Height = 22
        };

        shell.ColumnDefinitions.Add(new ColumnDefinition());
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });

        var body = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
            Margin = new Thickness(0, 1, 2, 1),
            ClipToBounds = true
        };

        var bodyGrid = new Grid();
        var fill = new Border
        {
            Background = BrushFor(remaining),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 27 * remaining / 100d,
            Opacity = 0.9
        };

        var text = new TextBlock
        {
            Text = remaining.ToString(),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        bodyGrid.Children.Add(fill);
        bodyGrid.Children.Add(text);
        body.Child = bodyGrid;
        Grid.SetColumn(body, 0);

        var terminal = new Border
        {
            Width = 3,
            Height = 9,
            CornerRadius = new CornerRadius(0, 2, 2, 0),
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(terminal, 1);

        shell.Children.Add(body);
        shell.Children.Add(terminal);
        return shell;
    }

    private static Brush BrushFor(int remaining) =>
        remaining switch
        {
            >= 50 => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            >= 20 => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            _ => new SolidColorBrush(Color.FromRgb(239, 68, 68))
        };

    private static string BuildTooltip(string label, QuotaWindow quota)
    {
        var reset = quota.ResetsAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown";
        var name = label == "5H" ? "5-hour" : "weekly";
        return $"{name} remaining: {quota.RemainingPercent:0}%\nResets: {reset}";
    }

    private void RenderError(string message)
    {
        BatteryStack.Children.Clear();
        BatteryStack.Children.Add(CreateStatusSlot(
            "!",
            $"CxCell could not read Codex usage.\n{message}",
            Brushes.OrangeRed));
        Height = NavigationIconPitchDip;
        TrackCodexWindow();
    }

    private static UIElement CreateStatusSlot(string value, string tooltip, Brush foreground)
    {
        var slot = new Grid
        {
            Width = OverlayWidthDip,
            Height = NavigationIconPitchDip,
            Background = Brushes.Transparent,
            ToolTip = tooltip
        };

        slot.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = foreground,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        return slot;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownCompleted)
            return;

        e.Cancel = true;
        if (_shutdownStarted)
            return;

        _shutdownStarted = true;
        _positionTimer.Stop();
        _refreshTimer.Stop();
        _shutdown.Cancel();

        try
        {
            await _usageClient.DisposeAsync();
        }
        finally
        {
            _shutdown.Dispose();
            _shutdownCompleted = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr newLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, newLong)
            : SetWindowLong32(hWnd, nIndex, newLong);
}
