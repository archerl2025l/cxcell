using System.Diagnostics;

namespace CxCell;

public static class CodexHostProcess
{
    private static readonly string[] ProcessNames = ["ChatGPT", "Codex"];

    public static bool IsRunning()
    {
        foreach (var name in ProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            try
            {
                if (processes.Any(process =>
                {
                    try { return !process.HasExited; }
                    catch { return false; }
                }))
                {
                    return true;
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        return false;
    }
}
