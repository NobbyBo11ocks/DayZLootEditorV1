using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DayZLootForge.Models;

namespace DayZLootForge.Services;

public sealed class TypesXmlService : ITypesXmlService
{
    public async Task<TypesFileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A types.xml path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected types.xml file was not found.", path);
        }

        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo, cancellationToken)
            .ConfigureAwait(false);

        if (document.Root?.Name.LocalName != "types")
        {
            throw new InvalidDataException("This file is not a DayZ types.xml file. The root element must be <types>.");
        }

        var entries = document.Root
            .Elements()
            .Where(element => element.Name.LocalName == "type")
            .Select(ParseTypeElement)
            .ToList();

        foreach (var entry in entries)
        {
            entry.AcceptClean();
        }

        return new TypesFileLoadResult(path, entries, new XDocument(document));
    }

    public string BuildPreviewXml(IEnumerable<DayzTypeEntry> entries, XDocument? sourceDocument = null)
    {
        var entryList = entries.ToList();
        var document = BuildDocument(entryList, sourceDocument);
        return document.Declaration is null
            ? document.ToString()
            : $"{document.Declaration}{Environment.NewLine}{document}";
    }

    public async Task SaveAsync(
        string path,
        IEnumerable<DayzTypeEntry> entries,
        XDocument? sourceDocument = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A save path is required.", nameof(path));
        }

        var entryList = entries.ToList();
        var document = BuildDocument(entryList, sourceDocument);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        await using (var stream = File.Open(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = XmlWriter.Create(stream, settings))
        {
            await document.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, path, overwrite: true);

        foreach (var entry in entryList)
        {
            entry.SourceElement = new XElement(BuildTypeElement(entry, entry.SourceElement));
            entry.AcceptClean();
        }
    }

    private static XDocument BuildDocument(IReadOnlyList<DayzTypeEntry> entries, XDocument? sourceDocument)
    {
        if (sourceDocument?.Root?.Name.LocalName != "types")
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("types", entries.Select(entry => BuildTypeElement(entry, entry.SourceElement))));
        }

        var document = new XDocument(sourceDocument);
        var root = document.Root!;
        var entriesWithSource = entries
            .Where(entry => entry.SourceElement is not null)
            .Select(entry => (Source: entry.SourceElement!, Entry: entry))
            .ToList();

        var replacementNodes = new List<object>();
        foreach (var node in root.Nodes())
        {
            if (node is XElement element && element.Name.LocalName == "type")
            {
                var matchingIndex = entriesWithSource.FindIndex(pair => XNode.DeepEquals(pair.Source, element));
                if (matchingIndex >= 0)
                {
                    var matchingEntry = entriesWithSource[matchingIndex].Entry;
                    entriesWithSource.RemoveAt(matchingIndex);
                    replacementNodes.Add(BuildTypeElement(matchingEntry, element));
                }

                continue;
            }

            replacementNodes.Add(CloneNode(node));
        }

        var appendedEntries = entries.Where(entry => entry.SourceElement is null).ToList();
        if (appendedEntries.Count > 0)
        {
            if (replacementNodes.Count > 0 && replacementNodes[^1] is not XText)
            {
                replacementNodes.Add(new XText(Environment.NewLine + "    "));
            }

            for (var index = 0; index < appendedEntries.Count; index++)
            {
                replacementNodes.Add(BuildTypeElement(appendedEntries[index], null));
                replacementNodes.Add(new XText(index == appendedEntries.Count - 1 ? Environment.NewLine : Environment.NewLine + "    "));
            }
        }

        root.ReplaceNodes(replacementNodes);
        return document;
    }

    private static DayzTypeEntry ParseTypeElement(XElement element)
    {
        var flags = element.Elements().FirstOrDefault(e => e.Name.LocalName == "flags");
        var entry = new DayzTypeEntry
        {
            Name = (string?)element.Attribute("name") ?? string.Empty,
            Nominal = ReadInt(element, "nominal", 0),
            Lifetime = ReadInt(element, "lifetime", 0),
            Restock = ReadInt(element, "restock", 0),
            Min = ReadInt(element, "min", 0),
            QuantMin = ReadInt(element, "quantmin", -1),
            QuantMax = ReadInt(element, "quantmax", -1),
            Cost = ReadInt(element, "cost", 100),
            CountInCargo = ReadBool(flags, "count_in_cargo"),
            CountInHoarder = ReadBool(flags, "count_in_hoarder"),
            CountInMap = ReadBool(flags, "count_in_map", defaultValue: true),
            CountInPlayer = ReadBool(flags, "count_in_player"),
            Crafted = ReadBool(flags, "crafted"),
            Deloot = ReadBool(flags, "deloot"),
            Category = element.Elements().FirstOrDefault(e => e.Name.LocalName == "category")?.Attribute("name")?.Value ?? string.Empty,
            TagsCsv = ReadChildNames(element, "tag"),
            UsagesCsv = ReadChildNames(element, "usage"),
            ValuesCsv = ReadChildNames(element, "value"),
            SourceElement = new XElement(element)
        };

        entry.AcceptClean();
        return entry;
    }

    private static XElement BuildTypeElement(DayzTypeEntry entry, XElement? sourceElement)
    {
        var type = sourceElement is null
            ? new XElement("type")
            : new XElement(sourceElement);

        type.SetAttributeValue("name", entry.Name.Trim());
        SetChildValue(type, "nominal", entry.Nominal.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "lifetime", entry.Lifetime.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "restock", entry.Restock.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "min", entry.Min.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "quantmin", entry.QuantMin.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "quantmax", entry.QuantMax.ToString(CultureInfo.InvariantCulture));
        SetChildValue(type, "cost", entry.Cost.ToString(CultureInfo.InvariantCulture));

        var flags = EnsureChild(type, "flags");
        flags.SetAttributeValue("count_in_cargo", ToDayzBool(entry.CountInCargo));
        flags.SetAttributeValue("count_in_hoarder", ToDayzBool(entry.CountInHoarder));
        flags.SetAttributeValue("count_in_map", ToDayzBool(entry.CountInMap));
        flags.SetAttributeValue("count_in_player", ToDayzBool(entry.CountInPlayer));
        flags.SetAttributeValue("crafted", ToDayzBool(entry.Crafted));
        flags.SetAttributeValue("deloot", ToDayzBool(entry.Deloot));

        ReplaceNameElements(type, "category", string.IsNullOrWhiteSpace(entry.Category) ? Array.Empty<string>() : new[] { entry.Category.Trim() });
        ReplaceNameElements(type, "tag", SplitCsv(entry.TagsCsv).ToArray());
        ReplaceNameElements(type, "usage", SplitCsv(entry.UsagesCsv).ToArray());
        ReplaceNameElements(type, "value", SplitCsv(entry.ValuesCsv).ToArray());

        return type;
    }

    private static void ReplaceNameElements(XElement parent, string elementName, IReadOnlyList<string> values)
    {
        parent.Elements().Where(e => e.Name.LocalName == elementName).Remove();

        var insertAfter = parent.Elements().LastOrDefault(e =>
            e.Name.LocalName is "flags" or "nominal" or "lifetime" or "restock" or "min" or "quantmin" or "quantmax" or "cost" or "category" or "tag" or "usage" or "value");

        if (values.Count == 0)
        {
            return;
        }

        IEnumerable<XElement> newElements = elementName == "category"
            ? values.Select(value => new XElement(elementName, new XAttribute("name", value)))
            : values.Select(value => new XElement(elementName, new XAttribute("name", value)));

        if (insertAfter is null)
        {
            parent.Add(newElements);
            return;
        }

        insertAfter.AddAfterSelf(newElements);
    }

    private static void SetChildValue(XElement parent, string childName, string value)
    {
        EnsureChild(parent, childName).Value = value;
    }

    private static XElement EnsureChild(XElement parent, string childName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == childName);
        if (child is not null)
        {
            return child;
        }

        child = new XElement(childName);
        parent.Add(child);
        return child;
    }

    private static object CloneNode(XNode node) =>
        node switch
        {
            XElement element => new XElement(element),
            XComment comment => new XComment(comment.Value),
            XCData cdata => new XCData(cdata.Value),
            XText text => new XText(text.Value),
            XProcessingInstruction pi => new XProcessingInstruction(pi.Target, pi.Data),
            XDocumentType docType => new XDocumentType(docType.Name, docType.PublicId, docType.SystemId, docType.InternalSubset),
            _ => new XText(node.ToString())
        };

    private static int ReadInt(XElement parent, string childName, int defaultValue)
    {
        var raw = parent.Elements().FirstOrDefault(e => e.Name.LocalName == childName)?.Value;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    private static bool ReadBool(XElement? element, string attributeName, bool defaultValue = false)
    {
        var raw = element?.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadChildNames(XElement element, string childName)
    {
        var names = element.Elements()
            .Where(e => e.Name.LocalName == childName)
            .Select(e => e.Attribute("name")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(", ", names);
    }

    private static IEnumerable<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            yield break;
        }

        foreach (var item in csv.Split(new[] { ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return item;
        }
    }

    private static string ToDayzBool(bool value) => value ? "1" : "0";
}
