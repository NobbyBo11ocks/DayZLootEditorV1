using System.Xml.Linq;
using DZServerToolkit.Models;
using DZServerToolkit.Services;

namespace DZServerToolkit.Tests;

public sealed class TypesXmlServiceTests
{
    [Fact]
    public async Task SaveAsync_PreservesUnknownNodesAndComments()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "types.xml");
        await File.WriteAllTextAsync(path, """
<?xml version="1.0" encoding="UTF-8"?>
<types>
    <!-- keep me -->
    <type name="AKM">
        <nominal>1</nominal>
        <lifetime>3600</lifetime>
        <restock>0</restock>
        <min>0</min>
        <quantmin>-1</quantmin>
        <quantmax>-1</quantmax>
        <cost>100</cost>
        <flags count_in_cargo="0" count_in_hoarder="0" count_in_map="1" count_in_player="0" crafted="0" deloot="0" />
        <custom node="modded">value</custom>
        <category name="weapons" />
        <usage name="Military" />
    </type>
</types>
""");

        var service = new TypesXmlService();
        var result = await service.LoadAsync(path);
        var entry = Assert.Single(result.Entries);
        entry.Nominal = 5;
        entry.TagsCsv = "rare";

        await service.SaveAsync(path, result.Entries, result.SourceDocument);

        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var type = Assert.Single(document.Root!.Elements("type"));
        Assert.Equal("5", type.Element("nominal")?.Value);
        Assert.NotNull(type.Element("custom"));
        Assert.Contains(document.Root.Nodes().OfType<XComment>(), comment => comment.Value.Contains("keep me", StringComparison.Ordinal));
        Assert.Contains(type.Elements("tag"), element => (string?)element.Attribute("name") == "rare");
    }

    [Fact]
    public async Task SaveAsync_AddsNewEntries()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "types.xml");
        await File.WriteAllTextAsync(path, """
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
""");

        var service = new TypesXmlService();
        var result = await service.LoadAsync(path);
        var entries = result.Entries.ToList();
        entries.Add(new DayzTypeEntry
        {
            Name = "NewCustomItem",
            Nominal = 2,
            Min = 1,
            Lifetime = 14400,
            Restock = 0,
            QuantMin = -1,
            QuantMax = -1,
            Cost = 100,
            CountInMap = true,
            Category = "tools"
        });

        await service.SaveAsync(path, entries, result.SourceDocument);

        var document = XDocument.Load(path);
        Assert.Equal(2, document.Root!.Elements("type").Count());
    }


    [Fact]
    public async Task SaveAsync_RemovesOnlyDeletedDuplicateWhenSourceElementsMatch()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "types.xml");
        await File.WriteAllTextAsync(path, """
<?xml version="1.0" encoding="UTF-8"?>
<types>
    <type name="DuplicateItem">
        <nominal>1</nominal>
        <lifetime>3600</lifetime>
        <restock>0</restock>
        <min>0</min>
        <quantmin>-1</quantmin>
        <quantmax>-1</quantmax>
        <cost>100</cost>
        <flags count_in_cargo="0" count_in_hoarder="0" count_in_map="1" count_in_player="0" crafted="0" deloot="0" />
    </type>
    <type name="DuplicateItem">
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
""");

        var service = new TypesXmlService();
        var result = await service.LoadAsync(path);
        var entries = result.Entries.Take(1).ToList();

        await service.SaveAsync(path, entries, result.SourceDocument);

        var document = XDocument.Load(path);
        Assert.Single(document.Root!.Elements("type"));
    }



    [Fact]
    public async Task SaveAsync_CleansUpTemporaryFile_WhenReplacingTargetFails()
    {
        var tempDir = CreateTempDirectory();
        var path = Path.Combine(tempDir, "types.xml");
        Directory.CreateDirectory(path);

        var service = new TypesXmlService();
        var entries = new[]
        {
            new DayzTypeEntry
            {
                Name = "AKM",
                Nominal = 1,
                Min = 0,
                Lifetime = 3600,
                Restock = 0,
                QuantMin = -1,
                QuantMax = -1,
                Cost = 100,
                CountInMap = true
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync(path, entries));
        Assert.False(File.Exists(path + ".tmp"));
    }
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DZServerToolkitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
