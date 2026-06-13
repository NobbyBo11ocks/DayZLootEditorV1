using DayZLootForge.Models;
using DayZLootForge.Services;

namespace DayZLootEditor.Tests;

public sealed class ValidationServiceTests
{
    [Fact]
    public void ValidateTypes_FlagsMinGreaterThanNominalAsError()
    {
        var service = new ValidationService();
        var entry = new DayzTypeEntry
        {
            Name = "AKM",
            Nominal = 1,
            Min = 2,
            Lifetime = 3600,
            Restock = 0,
            QuantMin = -1,
            QuantMax = -1,
            Cost = 100
        };

        var issues = service.ValidateTypes([entry]);

        Assert.Contains(issues, issue => issue.Message.Contains("Minimum quantity cannot be greater than nominal.", StringComparison.Ordinal));
        Assert.Equal("FIX", entry.ValidationState);
    }
}
