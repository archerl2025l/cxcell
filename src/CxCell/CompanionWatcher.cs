using System.Diagnostics;

namespace CxCell;

public static class CompanionWatcher
{
    public const int UserExitCode = 42;
    private const string WatcherMutexName = @"Local\CxCell.Watcher.Singleton";

    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(initiallyOwned: true, WatcherMutexName, out var ownsMutex);
        if (!ownsMutex)
            return 0;

        Process? overlay = null;
        var suppressedUntilHostStops = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var hostRunning = CodexHostProcess.IsRunning();

                if (!hostRunning)
                {
                    suppressedUntilHostStops = false;
                    if (overlay is not null)
                    {
                        await StopOverlayAsync(overlay);
                        overlay.Dispose();
                        overlay = null;
                    }
                }
                else
                {
                    if (overlay is { HasExited: true })
                    {
                        if (overlay.ExitCode == UserExitCode)
                            suppressedUntilHostStops = true;

                        overlay.Dispose();
                        overlay = null;
                    }

                    if (overlay is null && !suppressedUntilHostStops)
                        overlay = StartOverlay();
                }

                await Task.Delay(750, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (overlay is not null)
            {
                await StopOverlayAsync(overlay);
                overlay.Dispose();
            }
        }

        return 0;
    }

    public static void StartDetached()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve the CxCell executable path.");

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "--watcher",
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory
        });
    }

    private static Process StartOverlay()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve the CxCell executable path.");

        return Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "--overlay-managed",
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory
        }) ?? throw new InvalidOperationException("Unable to start the CxCell overlay.");
    }

    private static async Task StopOverlayAsync(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            if (process.CloseMainWindow())
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                    return;
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort during host shutdown or Windows logoff.
        }
    }
}
