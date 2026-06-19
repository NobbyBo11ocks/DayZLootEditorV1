using System.Xml.Linq;

namespace DZServerToolkit.Models;

public sealed class EditorSnapshot
{
    public required IReadOnlyList<DayzTypeEntry> Entries { get; init; }
    public required XDocument? LoadedDocument { get; init; }
    public required string FilePath { get; init; }
    public required string MissionFolder { get; init; }
    public required string StatusMessage { get; init; }
}
