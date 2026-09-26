namespace CxCell;

public static class CodexHostMatcher
{
    public static bool IsSupportedHost(string? processName, string? windowTitle)
    {
        return Contains(processName, "codex")
            || Contains(processName, "chatgpt")
            || Contains(windowTitle, "codex")
            || Contains(windowTitle, "chatgpt");
    }

    private static bool Contains(string? value, string expected) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(expected, StringComparison.OrdinalIgnoreCase);
}
