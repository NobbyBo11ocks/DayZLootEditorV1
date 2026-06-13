
using System.Reflection;
using DayZLootForge.Services;
using DayZLootForge.ViewModels;

namespace DayZLootEditor.Tests;

public sealed class TypesEditorViewModelTests
{
    [Fact]
    public void Constructor_InitializesApplyProfileTemplateCommand_WhenTemplatesAreLoaded()
    {
        var viewModel = new TypesEditorViewModel(
            new StubFileDialogService(),
            new TypesXmlService(),
            new ValidationService(),
            new BackupService(),
            new CustomCeService(),
            new LootProfileService(),
            new RecentFilesService(),
            new TextDiffService());

        Assert.NotNull(viewModel.ApplyProfileTemplateCommand);
        Assert.NotNull(viewModel.SelectedProfileTemplate);
        Assert.NotNull(viewModel.UseTypesPresetCommand);
        Assert.NotNull(viewModel.UnregisterSelectedCustomCeCommand);
        Assert.False(viewModel.AddEntryCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenMissionFolderPathAsync_ClearsStaleFileState_WhenMissionHasNoTypesFile()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "db", "types.xml"), SampleTypesXml);

        var alternateMission = sandbox.CreateSubdirectory("mpmissions/livonia");
        await File.WriteAllTextAsync(Path.Combine(alternateMission, "cfgeconomycore.xml"), "<economycore />");

        var viewModel = CreateViewModel();

        await InvokePrivateAsync(viewModel, "LoadFileAsync", Path.Combine(missionFolder, "db", "types.xml"), missionFolder);
        Assert.True(viewModel.HasWorkingFile);
        Assert.NotEmpty(viewModel.Entries);

        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", alternateMission);

        Assert.False(viewModel.HasWorkingFile);
        Assert.Empty(viewModel.FilePath);
        Assert.Empty(viewModel.Entries);
        Assert.Equal(alternateMission, viewModel.MissionFolder);
        Assert.Contains("no db/types.xml was found", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenSelectedCustomTypesAsync_PreservesMissionFolderContext_ForModTypesFile()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), """
<economycore>
    <ce folder="modtypes">
        <file name="types_custom.xml" type="types" />
    </ce>
</economycore>
""");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "db", "types.xml"), SampleTypesXml);
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes", "types_custom.xml"), SampleTypesXml);

        var viewModel = CreateViewModel();

        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", missionFolder);
        Assert.Equal(missionFolder, viewModel.MissionFolder);

        await InvokePrivateAsync(viewModel, "RefreshCustomCeAsync");
        viewModel.SelectedCustomCeFile = Assert.Single(viewModel.CustomCeFiles);
        await InvokePrivateAsync(viewModel, "OpenSelectedCustomTypesAsync");

        Assert.Equal(missionFolder, viewModel.MissionFolder);
        Assert.EndsWith(Path.Combine("modtypes", "types_custom.xml"), viewModel.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.HasWorkingFile);
    }

    [Fact]
    public async Task SearchText_DebouncesFilterRefresh_ForLargeLootTables()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        var path = Path.Combine(missionFolder, "db", "types.xml");
        await File.WriteAllTextAsync(path, """
<types>
  <type name="AKM">
    <nominal>1</nominal>
    <min>1</min>
    <lifetime>100</lifetime>
    <restock>0</restock>
    <category name="weapons" />
  </type>
  <type name="Bandage">
    <nominal>1</nominal>
    <min>1</min>
    <lifetime>100</lifetime>
    <restock>0</restock>
    <category name="medical" />
  </type>
</types>
""");

        var viewModel = CreateViewModel();
        await InvokePrivateAsync(viewModel, "LoadFileAsync", path, missionFolder);

        viewModel.SearchText = "AK";

        Assert.Equal(2, viewModel.FilteredEntries.Count);

        await Task.Delay(250);

        Assert.Single(viewModel.FilteredEntries);
        Assert.Equal("AKM", viewModel.FilteredEntries[0].Name);
    }

    [Fact]
    public async Task EditingNumericField_KeepsVisibleRowsStable_UntilValidationRuns()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        var path = Path.Combine(missionFolder, "db", "types.xml");
        await File.WriteAllTextAsync(path, SampleTypesXml);

        var viewModel = CreateViewModel();
        await InvokePrivateAsync(viewModel, "LoadFileAsync", path, missionFolder);
        viewModel.SearchText = "AK";
        await Task.Delay(250);

        var initialNames = viewModel.FilteredEntries.Select(entry => entry.Name).ToArray();
        var entryToEdit = viewModel.FilteredEntries[0];
        entryToEdit.Nominal += 5;

        var updatedNames = viewModel.FilteredEntries.Select(entry => entry.Name).ToArray();
        Assert.Equal(initialNames, updatedNames);
    }


    private static TypesEditorViewModel CreateViewModel()
    {
        return new TypesEditorViewModel(
            new StubFileDialogService(),
            new TypesXmlService(),
            new ValidationService(),
            new BackupService(),
            new CustomCeService(),
            new LootProfileService(),
            new RecentFilesService(),
            new TextDiffService());
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = method!.Invoke(target, args) as Task;
        Assert.NotNull(task);
        await task!;
    }



    [Fact]
    public async Task LoadFileAsync_ClearsStaleState_WhenNewFileIsMalformed()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        var goodPath = Path.Combine(missionFolder, "db", "types.xml");
        var badPath = Path.Combine(missionFolder, "db", "types_broken.xml");
        await File.WriteAllTextAsync(goodPath, SampleTypesXml);
        await File.WriteAllTextAsync(badPath, "<types><type name=\"Broken\"></types>");

        var viewModel = CreateViewModel();

        await InvokePrivateAsync(viewModel, "LoadFileAsync", goodPath, missionFolder);
        Assert.True(viewModel.HasWorkingFile);
        Assert.NotEmpty(viewModel.Entries);

        await InvokePrivateAsync(viewModel, "LoadFileAsync", badPath, missionFolder);

        Assert.False(viewModel.HasWorkingFile);
        Assert.Empty(viewModel.FilePath);
        Assert.Empty(viewModel.Entries);
        Assert.Equal(missionFolder, viewModel.MissionFolder);
        Assert.Contains("Load failed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenSelectedCustomTypesAsync_KeepsCustomCeWorkspace_WhenSelectedFileFailsToLoad()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), """
<economycore>
    <ce folder="modtypes">
        <file name="types_custom.xml" type="types" />
    </ce>
</economycore>
""");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes", "types_custom.xml"), "<types><type name=\"Broken\"></types>");

        var viewModel = CreateViewModel();
        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", missionFolder);
        await InvokePrivateAsync(viewModel, "RefreshCustomCeAsync");
        viewModel.SelectedCustomCeFile = Assert.Single(viewModel.CustomCeFiles);
        viewModel.ActiveFeature = "Custom CE Files";

        await InvokePrivateAsync(viewModel, "OpenSelectedCustomTypesAsync");

        Assert.Equal("Custom CE Files", viewModel.ActiveFeature);
        Assert.False(viewModel.HasWorkingFile);
        Assert.Contains("Load failed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepairSelectedCustomCeAsync_CreatesBackupBeforeMutatingFile()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), """
<economycore>
    <ce folder="modtypes">
        <file name="types_custom.xml" type="types" />
    </ce>
</economycore>
""");
        var customTypesPath = Path.Combine(missionFolder, "modtypes", "types_custom.xml");
        await File.WriteAllTextAsync(customTypesPath, "<wrongroot><type name=\"AKM\" /></wrongroot>");

        var viewModel = CreateViewModel();
        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", missionFolder);
        await InvokePrivateAsync(viewModel, "RefreshCustomCeAsync");
        viewModel.SelectedCustomCeFile = Assert.Single(viewModel.CustomCeFiles);

        await InvokePrivateAsync(viewModel, "RepairSelectedCustomCeAsync");

        var backupDirectory = Path.Combine(missionFolder, "modtypes", "DayZLootForgeBackups");
        Assert.True(Directory.Exists(backupDirectory));
        Assert.NotEmpty(Directory.GetFiles(backupDirectory, "*.bak"));
        Assert.Contains("Backup created", viewModel.CustomCeStatus, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task UnloadLoadedFileAsync_KeepsMissionFolderAndClearsWorkingFileState()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "db", "types.xml"), SampleTypesXml);

        var viewModel = CreateViewModel();
        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", missionFolder);

        Assert.True(viewModel.HasWorkingFile);
        Assert.NotEmpty(viewModel.Entries);

        await InvokePrivateAsync(viewModel, "UnloadLoadedFileAsync");

        Assert.False(viewModel.HasWorkingFile);
        Assert.Empty(viewModel.FilePath);
        Assert.Empty(viewModel.Entries);
        Assert.Equal(missionFolder, viewModel.MissionFolder);
        Assert.Contains("unloaded", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnloadLoadedFileAsync_CancelsWhenDiscardIsRejected()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/chernarusplus");
        Directory.CreateDirectory(Path.Combine(missionFolder, "db"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "db", "types.xml"), SampleTypesXml);

        var viewModel = new TypesEditorViewModel(
            new StubFileDialogService(confirmDiscardChanges: false),
            new TypesXmlService(),
            new ValidationService(),
            new BackupService(),
            new CustomCeService(),
            new LootProfileService(),
            new RecentFilesService(),
            new TextDiffService());

        await InvokePrivateAsync(viewModel, "OpenMissionFolderPathAsync", missionFolder);
        viewModel.Entries[0].Nominal += 1;

        await InvokePrivateAsync(viewModel, "UnloadLoadedFileAsync");

        Assert.True(viewModel.HasWorkingFile);
        Assert.NotEmpty(viewModel.Entries);
        Assert.Contains("cancelled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private const string SampleTypesXml = """
<?xml version="1.0" encoding="UTF-8"?>
<types>
    <type name="AKM">
        <nominal>1</nominal>
        <lifetime>3600</lifetime>
        <restock>0</restock>
        <min>0</min>
        <quantmin>-1</quantmin>
        <quantmax>-1</quantmax>
        <cost>100</cost>
        <flags count_in_cargo="0" count_in_hoarder="0" count_in_map="1" count_in_player="0" crafted="0" deloot="0" />
    </type>
</types>
""";

    private sealed class StubFileDialogService : IFileDialogService
    {
        private readonly bool _confirmDiscardChanges;

        public StubFileDialogService(bool confirmDiscardChanges = true)
        {
            _confirmDiscardChanges = confirmDiscardChanges;
        }

        public Task<string?> PickTypesFileAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickMissionFolderAsync() => Task.FromResult<string?>(null);
        public Task<string?> PickSaveTypesPathAsync(string suggestedFileName) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmDiscardChangesAsync(string title, string message) => Task.FromResult(_confirmDiscardChanges);
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DayZLootEditorTests", Guid.NewGuid().ToString("N"));

        public TestDirectory()
        {
            Directory.CreateDirectory(_root);
        }

        public string CreateSubdirectory(string relativePath)
        {
            var path = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
