namespace DZServerToolkit.Models;

public sealed class LootProfileTemplate
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => Name;
}
