using Xunit;

namespace CxCell.Tests;

public sealed class CodexHostMatcherTests
{
    [Theory]
    [InlineData("Codex", "Anything")]
    [InlineData("codex-desktop", "Anything")]
    [InlineData("ChatGPT", "GitHub开发流程验证")]
    [InlineData("chatgpt", "Anything")]
    [InlineData("SomeHost", "ChatGPT")]
    [InlineData("SomeHost", "Codex")]
    public void RecognizesSupportedHosts(string processName, string title)
    {
        Assert.True(CodexHostMatcher.IsSupportedHost(processName, title));
    }

    [Fact]
    public void RejectsUnrelatedWindows()
    {
        Assert.False(CodexHostMatcher.IsSupportedHost("powershell", "Windows PowerShell"));
    }
}
