using System.Xml.Linq;
using DayZLootEditor.Services;

namespace DayZLootEditor.Tests;

public sealed class CustomCeServiceTests
{
    [Fact]
    public async Task AddOrRegisterFileAsync_CreatesXmlAndRegistration()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");

        var service = new CustomCeService();
        var entry = await service.AddOrRegisterFileAsync(missionFolder, "modtypes", "types_custom.xml", "types");

        Assert.Equal("modtypes/types_custom.xml", entry.RelativePath);
        Assert.True(File.Exists(Path.Combine(missionFolder, "modtypes", "types_custom.xml")));

        var economyCore = XDocument.Load(Path.Combine(missionFolder, "cfgeconomycore.xml"));
        var registration = economyCore.Root!
            .Elements("ce")
            .Single(x => (string?)x.Attribute("folder") == "modtypes")
            .Elements("file")
            .Single();

        Assert.Equal("types_custom.xml", (string?)registration.Attribute("name"));
        Assert.Equal("types", (string?)registration.Attribute("type"));
    }


    [Fact]
    public async Task AddOrRegisterFileAsync_Throws_WhenSamePathIsAlreadyRegisteredWithDifferentType()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(
            Path.Combine(missionFolder, "cfgeconomycore.xml"),
            """
            <economycore>
                <ce folder="modtypes">
                    <file name="types_custom.xml" type="types" />
                </ce>
            </economycore>
            """);

        var service = new CustomCeService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddOrRegisterFileAsync(missionFolder, "modtypes", "types_custom.xml", "events"));

        Assert.Contains("already registered as type 'types'", exception.Message, StringComparison.OrdinalIgnoreCase);

        var economyCore = XDocument.Load(Path.Combine(missionFolder, "cfgeconomycore.xml"));
        var registrations = economyCore.Root!
            .Elements("ce")
            .SelectMany(element => element.Elements("file"))
            .ToList();

        Assert.Single(registrations);
        Assert.Equal("types", (string?)registrations[0].Attribute("type"));
    }


    [Fact]
    public async Task AddOrRegisterFileAsync_DoesNotPersistRegistration_WhenFileCreationFails()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "cfgeconomycore.xml"), "<economycore />");
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes"), "blocking file");

        var service = new CustomCeService();

        await Assert.ThrowsAnyAsync<Exception>(() => service.AddOrRegisterFileAsync(missionFolder, "modtypes", "types_custom.xml", "types"));

        var economyCore = XDocument.Load(Path.Combine(missionFolder, "cfgeconomycore.xml"));
        Assert.Empty(economyCore.Root!.Elements("ce"));
    }

    [Fact]
    public async Task UnregisterFileAsync_RemovesRegistrationAndOptionallyDeletesFile()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(
            Path.Combine(missionFolder, "cfgeconomycore.xml"),
            """
            <economycore>
                <ce folder="modtypes">
                    <file name="types_custom.xml" type="types" />
                </ce>
            </economycore>
            """);
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes", "types_custom.xml"), "<types />");

        var service = new CustomCeService();
        var removed = await service.UnregisterFileAsync(missionFolder, "modtypes", "types_custom.xml", "types", deleteFile: true);

        Assert.True(removed);
        Assert.False(File.Exists(Path.Combine(missionFolder, "modtypes", "types_custom.xml")));

        var economyCore = XDocument.Load(Path.Combine(missionFolder, "cfgeconomycore.xml"));
        Assert.Empty(economyCore.Root!.Elements("ce"));
    }

    [Fact]
    public async Task RepairFileRootAsync_RewritesRootToExpectedElement()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes", "types_custom.xml"), "<wrongroot><type name=\"AKM\" /></wrongroot>");

        var service = new CustomCeService();
        var repaired = await service.RepairFileRootAsync(missionFolder, "modtypes", "types_custom.xml", "types");

        Assert.Equal("modtypes/types_custom.xml", repaired.RelativePath);
        var document = XDocument.Load(Path.Combine(missionFolder, "modtypes", "types_custom.xml"));
        Assert.Equal("types", document.Root!.Name.LocalName);
        Assert.Single(document.Root.Elements("type"));
    }


    [Fact]
    public async Task UnregisterFileAsync_DeletesFileWithoutLeavingStagedDeleteArtifacts()
    {
        using var sandbox = new TestDirectory();
        var missionFolder = sandbox.CreateSubdirectory("mpmissions/testmission");
        Directory.CreateDirectory(Path.Combine(missionFolder, "modtypes"));
        await File.WriteAllTextAsync(
            Path.Combine(missionFolder, "cfgeconomycore.xml"),
            """
            <economycore>
                <ce folder="modtypes">
                    <file name="types_custom.xml" type="types" />
                </ce>
            </economycore>
            """);
        await File.WriteAllTextAsync(Path.Combine(missionFolder, "modtypes", "types_custom.xml"), "<types />");

        var service = new CustomCeService();
        var removed = await service.UnregisterFileAsync(missionFolder, "modtypes", "types_custom.xml", "types", deleteFile: true);

        Assert.True(removed);
        Assert.False(File.Exists(Path.Combine(missionFolder, "modtypes", "types_custom.xml")));
        Assert.Empty(Directory.GetFiles(Path.Combine(missionFolder, "modtypes"), "*.delete-*"));
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


    [Fact]
    public async Task RepairFileRootAsync_CleansUpTemporaryFile_WhenSaveFails()
    {
        using var sandbox = new TestDirectory();
        var blockingDirectory = sandbox.CreateSubdirectory("blocking");
        var destinationDirectory = sandbox.CreateSubdirectory("blocking/occupied-path");

        var saveMethod = typeof(CustomCeService).GetMethod("SaveDocumentAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(saveMethod);

        var task = saveMethod!.Invoke(null, [XDocument.Parse("<types />"), destinationDirectory, CancellationToken.None]) as Task;
        Assert.NotNull(task);

        await Assert.ThrowsAnyAsync<Exception>(async () => await task!);

        Assert.False(File.Exists(destinationDirectory + ".tmp"));
        Assert.True(Directory.Exists(destinationDirectory));
    }

}
