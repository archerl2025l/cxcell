using System.IO;
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
            var overlayExecutable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the CxCell executable path.");
            var installDirectory = Path.GetDirectoryName(overlayExecutable)
                ?? throw new InvalidOperationException("Unable to resolve the CxCell install directory.");
            var watcherExecutable = Path.Combine(installDirectory, "CxCellWatcher.exe");

            File.Copy(overlayExecutable, watcherExecutable, overwrite: true);
            StartupRegistration.Install(watcherExecutable, overlayExecutable);
            CompanionWatcher.StartDetached(watcherExecutable, overlayExecutable);
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

            var overlayExecutable = GetArgumentValue(args, "--overlay-exe")
                ?? Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? Environment.CurrentDirectory,
                    "CxCell.exe");

            _ = Task.Run(async () =>
            {
                var exitCode = await CompanionWatcher.RunAsync(overlayExecutable, _watcherShutdown.Token);
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

    private static string? GetArgumentValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
