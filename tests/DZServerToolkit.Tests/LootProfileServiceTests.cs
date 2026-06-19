using DZServerToolkit.Models;
using DZServerToolkit.Services;

namespace DZServerToolkit.Tests;

public sealed class LootProfileServiceTests
{
    [Fact]
    public void ApplyTemplate_Hardcore_ReducesTopTierCombatLoot()
    {
        var service = new LootProfileService();
        var entry = new DayzTypeEntry
        {
            Name = "M4A1",
            Category = "weapons",
            UsagesCsv = "Military",
            ValuesCsv = "Tier4",
            Nominal = 10,
            Min = 8,
            Restock = 100
        };

        var message = service.ApplyTemplate("hardcore", new[] { entry });

        Assert.Contains("Hardcore", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(entry.Nominal < 10);
        Assert.True(entry.Min < 8);
        Assert.True(entry.Restock > 100);
    }
}
