using System.Text.Json;

namespace DayZLootEditor.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private const int MaxItems = 10;
    private readonly string _settingsPath;

    public RecentFilesService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DayZLootEditor",
            "recent-files.json"))
    {
    }

    public RecentFilesService(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException("A settings file path is required.", nameof(settingsPath));
        }

        _settingsPath = settingsPath;
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public IReadOnlyList<string> GetRecentTypesFiles() => Load().RecentTypesFiles;
    public IReadOnlyList<string> GetRecentMissionFolders() => Load().RecentMissionFolders;
    public string GetLastWorkspace() => Load().LastWorkspace;
    public bool GetHasCompletedFirstMissionLoad() => Load().HasCompletedFirstMissionLoad;

    public Task AddRecentTypesFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        var normalizedPath = Path.GetFullPath(path);
        var data = Load();
        Update(data.RecentTypesFiles, normalizedPath);
        Save(data);
        return Task.CompletedTask;
    }

    public Task AddRecentMissionFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        var normalizedPath = Path.GetFullPath(path);
        var data = Load();
        Update(data.RecentMissionFolders, normalizedPath);
        Save(data);
        return Task.CompletedTask;
    }

    public Task SetLastWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        var normalizedWorkspace = string.IsNullOrWhiteSpace(workspace) ? "Loot Editor" : workspace.Trim();
        var data = Load();
        data.LastWorkspace = normalizedWorkspace;
        Save(data);
        return Task.CompletedTask;
    }

    public Task SetHasCompletedFirstMissionLoadAsync(bool value, CancellationToken cancellationToken = default)
    {
        var data = Load();
        data.HasCompletedFirstMissionLoad = value;
        Save(data);
        return Task.CompletedTask;
    }

    private RecentFilesData Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new RecentFilesData();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<RecentFilesData>(json) ?? new RecentFilesData();
        }
        catch
        {
            return new RecentFilesData();
        }
    }

    private void Save(RecentFilesData data)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = Path.Combine(
            directory ?? Path.GetDirectoryName(Path.GetFullPath(_settingsPath)) ?? Path.GetTempPath(),
            $"{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");

        File.WriteAllText(tempPath, json);

        try
        {
            if (File.Exists(_settingsPath))
            {
                File.Replace(tempPath, _settingsPath, null);
            }
            else
            {
                File.Move(tempPath, _settingsPath);
            }
        }
        catch
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                }

                File.Move(tempPath, _settingsPath);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }

                throw;
            }
        }
    }

    private static void Update(List<string> values, string path)
    {
        values.RemoveAll(value => string.Equals(value, path, StringComparison.OrdinalIgnoreCase));
        values.Insert(0, path);
        values.RemoveAll(value => string.IsNullOrWhiteSpace(value));
        while (values.Count > MaxItems)
        {
            values.RemoveAt(values.Count - 1);
        }
    }

    private sealed class RecentFilesData
    {
        public List<string> RecentTypesFiles { get; set; } = [];
        public List<string> RecentMissionFolders { get; set; } = [];
        public string LastWorkspace { get; set; } = "Loot Editor";
        public bool HasCompletedFirstMissionLoad { get; set; }
    }
}
