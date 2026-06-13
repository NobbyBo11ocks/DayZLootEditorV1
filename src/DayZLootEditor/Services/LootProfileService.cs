using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public sealed class LootProfileService : ILootProfileService
{
    private static readonly IReadOnlyList<LootProfileTemplate> Templates =
    [
        new LootProfileTemplate
        {
            Id = "vanilla-plus",
            Name = "Vanilla+",
            Description = "Light quality-of-life bump. Small boosts to food, medical, and build supplies while keeping overall balance close to vanilla."
        },
        new LootProfileTemplate
        {
            Id = "boosted-pvp",
            Name = "Boosted PvP",
            Description = "Higher combat tempo. Weapons, ammo, armor, and medical spawns get a stronger boost and restock faster."
        },
        new LootProfileTemplate
        {
            Id = "hardcore",
            Name = "Hardcore",
            Description = "Lean economy. Lower spawn counts, slower restocks, and scarcer top-tier combat gear."
        }
    ];

    public IReadOnlyList<LootProfileTemplate> GetTemplates() => Templates;

    public string ApplyTemplate(string templateId, IReadOnlyCollection<DayzTypeEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "No visible rows were available for the selected profile.";
        }

        var normalizedId = (templateId ?? string.Empty).Trim().ToLowerInvariant();
        var changed = 0;

        foreach (var entry in entries)
        {
            changed += normalizedId switch
            {
                "vanilla-plus" => ApplyVanillaPlus(entry),
                "boosted-pvp" => ApplyBoostedPvp(entry),
                "hardcore" => ApplyHardcore(entry),
                _ => 0
            };
        }

        var templateName = Templates.FirstOrDefault(template => template.Id == normalizedId)?.Name ?? "custom profile";
        return changed == 0
            ? $"{templateName} made no changes to the current visible rows."
            : $"{templateName} updated {changed:N0} field value(s) across {entries.Count:N0} visible row(s).";
    }

    private static int ApplyVanillaPlus(DayzTypeEntry entry)
    {
        var changed = 0;
        var tags = Normalize(entry.TagsCsv);
        var values = Normalize(entry.ValuesCsv);
        var category = entry.Category;

        var nominalScale = 1.0m;
        var minScale = 1.0m;
        var restockScale = 1.0m;

        if (Matches(category, "food", "medical", "tools"))
        {
            nominalScale = 1.20m;
            minScale = 1.15m;
        }

        if (Contains(values, "tier1", "tier2") || Contains(tags, "civilian", "shelves"))
        {
            nominalScale = Max(nominalScale, 1.10m);
        }

        if (Matches(category, "containers", "materials"))
        {
            nominalScale = Max(nominalScale, 1.15m);
            minScale = Max(minScale, 1.10m);
        }

        if (Matches(category, "weapons", "explosives"))
        {
            nominalScale = Min(nominalScale, 1.05m);
        }

        changed += ApplyScale(entry, nominalScale, minScale, restockScale);
        return changed;
    }

    private static int ApplyBoostedPvp(DayzTypeEntry entry)
    {
        var changed = 0;
        var category = entry.Category;
        var usages = Normalize(entry.UsagesCsv);
        var values = Normalize(entry.ValuesCsv);

        var nominalScale = 1.0m;
        var minScale = 1.0m;
        var restockScale = 1.0m;

        if (Matches(category, "weapons", "magazines", "explosives", "clothes"))
        {
            nominalScale = 1.40m;
            minScale = 1.30m;
            restockScale = 0.80m;
        }

        if (Matches(category, "medical"))
        {
            nominalScale = Max(nominalScale, 1.25m);
            minScale = Max(minScale, 1.20m);
            restockScale = Min(restockScale, 0.85m);
        }

        if (Contains(usages, "military", "police", "hunting") || Contains(values, "tier3", "tier4"))
        {
            nominalScale = Max(nominalScale, 1.30m);
            minScale = Max(minScale, 1.20m);
        }

        changed += ApplyScale(entry, nominalScale, minScale, restockScale);
        return changed;
    }

    private static int ApplyHardcore(DayzTypeEntry entry)
    {
        var changed = 0;
        var category = entry.Category;
        var usages = Normalize(entry.UsagesCsv);
        var values = Normalize(entry.ValuesCsv);

        var nominalScale = 0.70m;
        var minScale = 0.70m;
        var restockScale = 1.25m;

        if (Matches(category, "food", "medical"))
        {
            nominalScale = 0.80m;
            minScale = 0.80m;
        }

        if (Matches(category, "weapons", "magazines", "explosives") || Contains(usages, "military", "police"))
        {
            nominalScale = 0.55m;
            minScale = 0.55m;
            restockScale = 1.40m;
        }

        if (Contains(values, "tier4"))
        {
            nominalScale = Min(nominalScale, 0.50m);
            minScale = Min(minScale, 0.50m);
        }

        changed += ApplyScale(entry, nominalScale, minScale, restockScale);
        return changed;
    }

    private static int ApplyScale(DayzTypeEntry entry, decimal nominalScale, decimal minScale, decimal restockScale)
    {
        var changed = 0;

        changed += SetIfChanged(entry, entry.Nominal, Scale(entry.Nominal, nominalScale), value => entry.Nominal = value);
        changed += SetIfChanged(entry, entry.Min, Scale(entry.Min, minScale), value => entry.Min = value);

        if (entry.Restock > 0)
        {
            changed += SetIfChanged(entry, entry.Restock, Math.Max(0, (int)Math.Round(entry.Restock * restockScale, MidpointRounding.AwayFromZero)), value => entry.Restock = value);
        }

        return changed;
    }

    private static int SetIfChanged(DayzTypeEntry entry, int current, int next, Action<int> setter)
    {
        if (current == next)
        {
            return 0;
        }

        setter(next);
        return 1;
    }

    private static int Scale(int value, decimal scale) => Math.Max(0, (int)Math.Round(value * scale, MidpointRounding.AwayFromZero));
    private static decimal Max(decimal left, decimal right) => left >= right ? left : right;
    private static decimal Min(decimal left, decimal right) => left <= right ? left : right;

    private static bool Matches(string? category, params string[] values) =>
        values.Any(value => string.Equals(category?.Trim(), value, StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> Normalize(string? csv) =>
        (csv ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool Contains(HashSet<string> haystack, params string[] values) =>
        values.Any(haystack.Contains);
}
