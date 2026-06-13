using System.Xml.Linq;
using DayZLootForge.Models;

namespace DayZLootForge.Services;

public sealed record TypesFileLoadResult(string Path, IReadOnlyList<DayzTypeEntry> Entries, XDocument SourceDocument);

public interface ITypesXmlService
{
    Task<TypesFileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(string path, IEnumerable<DayzTypeEntry> entries, XDocument? sourceDocument = null, CancellationToken cancellationToken = default);
    string BuildPreviewXml(IEnumerable<DayzTypeEntry> entries, XDocument? sourceDocument = null);
}
