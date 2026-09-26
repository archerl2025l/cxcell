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
    private const int OverlayWidth = 50;
    private const int SidebarCenterOffset = 38;
    private const int OverlayBottomOffset = 116;

    private readonly CodexUsageClient _usageClient = new();
    private readonly CodexWindowLocator _windowLocator = new();
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly CancellationTokenSource _shutdown = new();

    private QuotaSnapshot _snapshot = new(null, null, null);
    private bool _refreshInProgress;

    public MainWindow()
    {
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
        if (_refreshInProgress)
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
        if (!_windowLocator.TryGetForegroundCodexWindow(out var bounds))
        {
            Opacity = 0;
            IsHitTestVisible = false;
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this);
        var leftPx = bounds.Left + SidebarCenterOffset - OverlayWidth / 2;
        var topPx = bounds.Bottom - OverlayBottomOffset - (int)ActualHeight;

        Left = leftPx / scale.DpiScaleX;
        Top = topPx / scale.DpiScaleY;
        Opacity = 1;
        IsHitTestVisible = true;
    }

    private void RenderBatteries(QuotaSnapshot snapshot)
    {
        BatteryStack.Children.Clear();

        if (snapshot.FiveHour is not null)
        {
            BatteryStack.Children.Add(CreateBattery("5H", snapshot.FiveHour));
        }

        if (snapshot.Weekly is not null)
        {
            if (BatteryStack.Children.Count > 0)
            {
                BatteryStack.Children.Add(new Border { Height = 6 });
            }

            BatteryStack.Children.Add(CreateBattery("W", snapshot.Weekly));
        }

        if (BatteryStack.Children.Count == 0)
        {
            var text = new TextBlock
            {
                Text = "—",
                Foreground = Brushes.White,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            text.ToolTip = "No supported Codex usage window is currently reported.";
            BatteryStack.Children.Add(text);
        }
    }

    private UIElement CreateBattery(string label, QuotaWindow quota)
    {
        var remaining = (int)Math.Round(quota.RemainingPercent);
        var shell = new Grid
        {
            Width = 38,
            Height = 25,
            ToolTip = BuildTooltip(label, quota)
        };

        shell.ColumnDefinitions.Add(new ColumnDefinition());
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });

        var body = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 1, 2, 1),
            ClipToBounds = true
        };

        var bodyGrid = new Grid();
        var fill = new Border
        {
            Background = BrushFor(remaining),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 29 * remaining / 100d,
            Opacity = 0.82
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
            Height = 10,
            CornerRadius = new CornerRadius(0, 2, 2, 0),
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(terminal, 1);

        shell.Children.Add(body);
        shell.Children.Add(terminal);

        var container = new StackPanel();
        container.Children.Add(shell);
        var caption = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        };
        container.Children.Add(caption);
        return container;
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
        var text = new TextBlock
        {
            Text = "!",
            Foreground = Brushes.OrangeRed,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        text.ToolTip = $"CxCell could not read Codex usage.\n{message}";
        BatteryStack.Children.Add(text);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        _positionTimer.Stop();
        _refreshTimer.Stop();
        _shutdown.Cancel();
        await _usageClient.DisposeAsync();
        _shutdown.Dispose();
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
