using System.Text;

namespace DayZLootForge.Services;

public static class CrashLogService
{
    private static readonly object SyncRoot = new();

    public static string LogException(string source, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(exception);

        var logPath = GetLogPath();
        var payload = new StringBuilder()
            .AppendLine("========================================")
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"Source: {source}")
            .AppendLine($"Type: {exception.GetType().FullName}")
            .AppendLine($"Message: {exception.Message}")
            .AppendLine("StackTrace:")
            .AppendLine(exception.ToString())
            .AppendLine();

        Write(logPath, payload.ToString());
        return logPath;
    }

    public static string LogMessage(string source, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var logPath = GetLogPath();
        var payload = new StringBuilder()
            .AppendLine("========================================")
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"Source: {source}")
            .AppendLine(message)
            .AppendLine();

        Write(logPath, payload.ToString());
        return logPath;
    }

    private static void Write(string path, string payload)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, payload, Encoding.UTF8);
        }
    }

    private static string GetLogPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DayZLootEditor",
            "logs");

        return Path.Combine(root, $"crash-{DateTime.UtcNow:yyyyMMdd}.log");
    }
}
