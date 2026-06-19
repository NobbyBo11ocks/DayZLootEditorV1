using DZServerToolkit.Services;

namespace DZServerToolkit.Tests;

public sealed class CrashLogServiceTests
{
    [Fact]
    public void LogException_WritesExceptionDetailsToLogFile()
    {
        var path = CrashLogService.LogException("UnitTest", new InvalidOperationException("boom"));

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("UnitTest", content, StringComparison.Ordinal);
        Assert.Contains("boom", content, StringComparison.Ordinal);
    }
}
