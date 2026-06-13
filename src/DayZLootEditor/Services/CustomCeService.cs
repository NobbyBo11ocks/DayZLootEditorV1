using System.Xml;
using System.Xml.Linq;
using DayZLootEditor.Models;

namespace DayZLootEditor.Services;

public sealed class CustomCeService : ICustomCeService
{
    private static readonly Dictionary<string, string> RootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["types"] = "types",
        ["spawnabletypes"] = "spawnabletypes",
        ["events"] = "events",
        ["eventspawns"] = "eventposdef",
        ["globals"] = "variables"
    };

    public IReadOnlyList<string> SupportedTypes { get; } = RootNames.Keys.OrderBy(key => key).ToArray();

    public string GetEconomyCorePath(string missionFolder)
    {
        return Path.Combine(missionFolder ?? string.Empty, "cfgeconomycore.xml");
    }

    public CustomCePreview BuildPreview(string missionFolder, string folder, string fileName, string type)
    {
        folder = NormalizeFolder(folder);
        fileName = NormalizeFileName(fileName);
        type = NormalizeType(type);

        var missionFolderPath = missionFolder ?? string.Empty;
        var fullPath = Path.Combine(missionFolderPath, folder, fileName);
        var relativePath = CombineRelative(folder, fileName);
        var economyCorePath = GetEconomyCorePath(missionFolderPath);
        var isRegistered = false;

        if (File.Exists(economyCorePath))
        {
            try
            {
                var document = XDocument.Load(economyCorePath, LoadOptions.None);
                var registration = FindRegistration(document.Root, folder, fileName, type);
                isRegistered = registration.File is not null;
            }
            catch
            {
                isRegistered = false;
            }
        }

        var fileExists = File.Exists(fullPath);
        var summary = $"{relativePath} | type='{type}' | root <{RootNames[type]}> | " +
            (isRegistered ? "already registered" : "will be registered") + " | " +
            (fileExists ? "file already exists" : "new file will be created");

        return new CustomCePreview(
            folder,
            fileName,
            type,
            relativePath,
            fullPath,
            RootNames[type],
            fileExists,
            isRegistered,
            summary);
    }

    public async Task<CustomCeLoadResult> LoadAsync(string missionFolder, CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();
        var entries = new List<CustomCeFileEntry>();

        if (string.IsNullOrWhiteSpace(missionFolder) || !Directory.Exists(missionFolder))
        {
            return new CustomCeLoadResult(string.Empty, entries, issues);
        }

        var economyCorePath = GetEconomyCorePath(missionFolder);
        if (!File.Exists(economyCorePath))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "cfgeconomycore.xml", "Missing cfgeconomycore.xml in the selected mission folder."));
            return new CustomCeLoadResult(economyCorePath, entries, issues);
        }

        XDocument document;
        try
        {
            await using var stream = File.Open(economyCorePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or IOException)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "cfgeconomycore.xml", $"Cannot read cfgeconomycore.xml: {ex.Message}"));
            return new CustomCeLoadResult(economyCorePath, entries, issues);
        }

        var root = document.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "economycore", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "cfgeconomycore.xml", "Root element must be <economycore>."));
            return new CustomCeLoadResult(economyCorePath, entries, issues);
        }

        foreach (var ce in root.Elements().Where(element => element.Name.LocalName == "ce"))
        {
            var folder = ce.Attribute("folder")?.Value?.Trim() ?? string.Empty;
            foreach (var file in ce.Elements().Where(element => element.Name.LocalName == "file"))
            {
                var fileName = file.Attribute("name")?.Value?.Trim() ?? string.Empty;
                var type = file.Attribute("type")?.Value?.Trim() ?? string.Empty;
                var relativePath = CombineRelative(folder, fileName);
                entries.Add(new CustomCeFileEntry
                {
                    Folder = folder,
                    FileName = fileName,
                    Type = type,
                    RelativePath = relativePath,
                    FullPath = Path.Combine(missionFolder, folder, fileName)
                });
            }
        }

        ValidateEntries(missionFolder, entries, issues);
        return new CustomCeLoadResult(economyCorePath, entries, issues);
    }

    public async Task<CustomCeFileEntry> AddOrRegisterFileAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(missionFolder) || !Directory.Exists(missionFolder))
        {
            throw new InvalidOperationException("Open a DayZ mission folder first.");
        }

        folder = NormalizeFolder(folder);
        fileName = NormalizeFileName(fileName);
        type = NormalizeType(type);

        var folderPath = Path.Combine(missionFolder, folder);
        var filePath = Path.Combine(folderPath, fileName);
        var fileAlreadyExisted = File.Exists(filePath);

        var economyCorePath = GetEconomyCorePath(missionFolder);
        var document = await LoadOrCreateEconomyCoreAsync(economyCorePath, cancellationToken).ConfigureAwait(false);
        var root = document.Root ?? throw new InvalidDataException("cfgeconomycore.xml is missing its root element.");

        var ce = root.Elements().FirstOrDefault(element =>
            element.Name.LocalName == "ce" &&
            string.Equals(element.Attribute("folder")?.Value?.Trim(), folder, StringComparison.OrdinalIgnoreCase));

        if (ce is null)
        {
            ce = new XElement("ce", new XAttribute("folder", folder));
            root.Add(new XText(Environment.NewLine + "    "));
            root.Add(ce);
            root.Add(new XText(Environment.NewLine));
        }

        var existingRegistrations = root
            .Elements()
            .Where(element => element.Name.LocalName == "ce")
            .SelectMany(element => element.Elements()
                .Where(child => child.Name.LocalName == "file")
                .Select(child => new
                {
                    Folder = element.Attribute("folder")?.Value?.Trim() ?? string.Empty,
                    Name = child.Attribute("name")?.Value?.Trim() ?? string.Empty,
                    Type = child.Attribute("type")?.Value?.Trim() ?? string.Empty
                }))
            .ToList();

        var conflictingRegistration = existingRegistrations.FirstOrDefault(registration =>
            string.Equals(registration.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(registration.Name, fileName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(registration.Type, type, StringComparison.OrdinalIgnoreCase));

        if (conflictingRegistration is not null)
        {
            throw new InvalidOperationException(
                $"The file '{CombineRelative(folder, fileName)}' is already registered as type '{conflictingRegistration.Type}'. Remove the conflicting registration before registering it as '{type}'.");
        }

        var alreadyRegistered = existingRegistrations.Any(registration =>
            string.Equals(registration.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(registration.Name, fileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(registration.Type, type, StringComparison.OrdinalIgnoreCase));

        if (!alreadyRegistered)
        {
            ce.Add(new XText(Environment.NewLine + "        "));
            ce.Add(new XElement("file", new XAttribute("name", fileName), new XAttribute("type", type)));
            ce.Add(new XText(Environment.NewLine + "    "));
        }

        try
        {
            if (!fileAlreadyExisted)
            {
                Directory.CreateDirectory(folderPath);
                await CreateEmptyCustomFileAsync(filePath, type, cancellationToken).ConfigureAwait(false);
            }

            await SaveDocumentAsync(document, economyCorePath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!fileAlreadyExisted && File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }

        return await ValidateSingleEntryAsync(missionFolder, folder, fileName, type).ConfigureAwait(false);
    }

    public async Task<bool> UnregisterFileAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        bool deleteFile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(missionFolder) || !Directory.Exists(missionFolder))
        {
            throw new InvalidOperationException("Open a DayZ mission folder first.");
        }

        folder = NormalizeFolder(folder);
        fileName = NormalizeFileName(fileName);
        type = NormalizeType(type);

        var economyCorePath = GetEconomyCorePath(missionFolder);
        var document = await LoadOrCreateEconomyCoreAsync(economyCorePath, cancellationToken).ConfigureAwait(false);
        var root = document.Root ?? throw new InvalidDataException("cfgeconomycore.xml is missing its root element.");

        var registration = FindRegistration(root, folder, fileName, type);
        if (registration.File is null || registration.Ce is null)
        {
            return false;
        }

        var filePath = Path.Combine(missionFolder, folder, fileName);
        var stagedDeletePath = string.Empty;

        try
        {
            if (deleteFile && File.Exists(filePath))
            {
                stagedDeletePath = filePath + ".delete-" + Guid.NewGuid().ToString("N");
                File.Move(filePath, stagedDeletePath);
            }

            registration.File.Remove();

            if (!registration.Ce.Elements().Any(element => element.Name.LocalName == "file"))
            {
                registration.Ce.Remove();
            }

            await SaveDocumentAsync(document, economyCorePath, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stagedDeletePath) && File.Exists(stagedDeletePath))
            {
                File.Delete(stagedDeletePath);
            }

            return true;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(stagedDeletePath) && File.Exists(stagedDeletePath) && !File.Exists(filePath))
            {
                File.Move(stagedDeletePath, filePath);
            }

            throw;
        }
    }

    public async Task<CustomCeFileEntry> RepairFileRootAsync(
        string missionFolder,
        string folder,
        string fileName,
        string type,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(missionFolder) || !Directory.Exists(missionFolder))
        {
            throw new InvalidOperationException("Open a DayZ mission folder first.");
        }

        folder = NormalizeFolder(folder);
        fileName = NormalizeFileName(fileName);
        type = NormalizeType(type);

        var filePath = Path.Combine(missionFolder, folder, fileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Custom CE file does not exist.", filePath);
        }

        var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        document.Root?.ReplaceWith(new XElement(RootNames[type], document.Root?.Attributes() ?? [], document.Root?.Nodes() ?? []));
        await SaveDocumentAsync(document, filePath, cancellationToken).ConfigureAwait(false);

        return await ValidateSingleEntryAsync(missionFolder, folder, fileName, type).ConfigureAwait(false);
    }

    private static (XElement? Ce, XElement? File) FindRegistration(XElement? root, string folder, string fileName, string type)
    {
        if (root is null)
        {
            return (null, null);
        }

        var ce = root.Elements().FirstOrDefault(element =>
            element.Name.LocalName == "ce" &&
            string.Equals(element.Attribute("folder")?.Value?.Trim(), folder, StringComparison.OrdinalIgnoreCase));

        var file = ce?.Elements().FirstOrDefault(element =>
            element.Name.LocalName == "file" &&
            string.Equals(element.Attribute("name")?.Value?.Trim(), fileName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(element.Attribute("type")?.Value?.Trim(), type, StringComparison.OrdinalIgnoreCase));

        return (ce, file);
    }

    private static void ValidateEntries(string missionFolder, IReadOnlyList<CustomCeFileEntry> entries, List<ValidationIssue> issues)
    {
        var duplicates = entries
            .GroupBy(entry => $"{entry.Folder}/{entry.FileName}/{entry.Type}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var entryIssues = new List<string>();

            if (string.IsNullOrWhiteSpace(entry.Folder))
            {
                entryIssues.Add("Folder is missing.");
            }

            if (string.IsNullOrWhiteSpace(entry.FileName))
            {
                entryIssues.Add("File name is missing.");
            }

            if (!RootNames.ContainsKey(entry.Type))
            {
                entryIssues.Add($"Unsupported CE file type '{entry.Type}'.");
            }

            if (duplicates.Contains($"{entry.Folder}/{entry.FileName}/{entry.Type}"))
            {
                entryIssues.Add("Duplicate cfgeconomycore.xml registration.");
            }

            var folderPath = Path.Combine(missionFolder, entry.Folder ?? string.Empty);
            var filePath = Path.Combine(folderPath, entry.FileName ?? string.Empty);
            entry.FullPath = filePath;
            entry.RelativePath = CombineRelative(entry.Folder, entry.FileName);
            entry.ItemCount = 0;

            if (!Directory.Exists(folderPath))
            {
                entryIssues.Add("Folder does not exist yet.");
            }

            if (!File.Exists(filePath))
            {
                entryIssues.Add("File does not exist yet.");
            }
            else if (RootNames.TryGetValue(entry.Type, out var expectedRoot))
            {
                try
                {
                    var document = XDocument.Load(filePath, LoadOptions.None);
                    var actualRoot = document.Root?.Name.LocalName ?? string.Empty;
                    if (!string.Equals(actualRoot, expectedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        entryIssues.Add($"Wrong XML root. Type '{entry.Type}' expects <{expectedRoot}> but file has <{actualRoot}>.");
                    }
                    else
                    {
                        entry.ItemCount = CountItems(document, entry.Type);
                        if (entry.ItemCount == 0)
                        {
                            entryIssues.Add("File is valid but empty.");
                        }
                    }
                }
                catch (Exception ex) when (ex is XmlException or InvalidDataException or IOException)
                {
                    entryIssues.Add($"Broken XML: {ex.Message}");
                }
            }

            var hasBlockingIssues = entryIssues.Any(issue => !string.Equals(issue, "File is valid but empty.", StringComparison.OrdinalIgnoreCase));
            entry.Status = hasBlockingIssues ? "FIX" : "OK";
            entry.IssueSummary = entryIssues.Count == 0 ? "Ready" : string.Join(" | ", entryIssues);

            foreach (var issue in entryIssues)
            {
                var severity = string.Equals(issue, "File is valid but empty.", StringComparison.OrdinalIgnoreCase)
                    ? ValidationSeverity.Info
                    : ValidationSeverity.Error;
                issues.Add(new ValidationIssue(severity, entry.RelativePath, issue));
            }
        }
    }

    private static int CountItems(XDocument document, string type)
    {
        var root = document.Root;
        if (root is null)
        {
            return 0;
        }

        return type.ToLowerInvariant() switch
        {
            "types" => root.Elements().Count(element => element.Name.LocalName == "type"),
            "spawnabletypes" => root.Elements().Count(element => element.Name.LocalName == "type"),
            "events" => root.Elements().Count(element => element.Name.LocalName == "event"),
            "eventspawns" => root.Elements().Count(element => element.Name.LocalName == "event"),
            "globals" => root.Elements().Count(element => element.Name.LocalName is "var" or "variable"),
            _ => 0
        };
    }

    private static async Task<XDocument> LoadOrCreateEconomyCoreAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("economycore"));
        }

        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var document = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(document.Root?.Name.LocalName, "economycore", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("cfgeconomycore.xml root element must be <economycore>.");
        }

        return document;
    }

    private static async Task CreateEmptyCustomFileAsync(string path, string type, CancellationToken cancellationToken)
    {
        var rootName = RootNames[type];
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(rootName));
        await SaveDocumentAsync(document, path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveDocumentAsync(XDocument document, string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        var temporaryPath = path + ".tmp";

        try
        {
            await using (var stream = File.Open(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var writer = XmlWriter.Create(stream, settings))
            {
                await document.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static async Task<CustomCeFileEntry> ValidateSingleEntryAsync(string missionFolder, string folder, string fileName, string type)
    {
        var entry = new CustomCeFileEntry
        {
            Folder = folder,
            FileName = fileName,
            Type = type
        };

        var issues = new List<ValidationIssue>();
        ValidateEntries(missionFolder, new[] { entry }, issues);
        await Task.CompletedTask.ConfigureAwait(false);
        return entry;
    }

    private static string NormalizeFolder(string folder)
    {
        folder = (folder ?? string.Empty).Trim().Trim('/', '\\');
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("Folder name is required.");
        }

        if (Path.IsPathRooted(folder) || folder.Split('/', '\\').Any(part => part == ".."))
        {
            throw new ArgumentException("Folder must be a safe relative folder inside the mission folder.");
        }

        return folder.Replace('\\', '/');
    }

    private static string NormalizeFileName(string fileName)
    {
        fileName = (fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.");
        }

        if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".xml";
        }

        if (Path.IsPathRooted(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("File name must be a simple .xml file name, not a path.");
        }

        return fileName;
    }

    private static string NormalizeType(string type)
    {
        type = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (!RootNames.ContainsKey(type))
        {
            throw new ArgumentException("Choose a supported CE file type.");
        }

        return type;
    }

    private static string CombineRelative(string? folder, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return fileName ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return folder;
        }

        return $"{folder.Trim().Trim('/', '\\')}/{fileName.Trim()}";
    }
}
