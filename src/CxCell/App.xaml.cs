using System.Windows;

namespace CxCell;

public partial class App : System.Windows.Application
{
    private CancellationTokenSource? _watcherShutdown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args ?? Array.Empty<string>();

        if (args.Contains("--install-autostart", StringComparer.OrdinalIgnoreCase))
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the CxCell executable path.");

            StartupRegistration.Install(executable);
            CompanionWatcher.StartDetached();
            Shutdown(0);
            return;
        }

        if (args.Contains("--uninstall-autostart", StringComparer.OrdinalIgnoreCase))
        {
            StartupRegistration.Uninstall();
            Shutdown(0);
            return;
        }

        if (args.Contains("--watcher", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _watcherShutdown = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                var exitCode = await CompanionWatcher.RunAsync(_watcherShutdown.Token);
                await Dispatcher.InvokeAsync(() => Shutdown(exitCode));
            });
            return;
        }

        var managedLifecycle = args.Contains("--overlay-managed", StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow(managedLifecycle);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _watcherShutdown?.Cancel();
        _watcherShutdown?.Dispose();
        base.OnExit(e);
    }
}
