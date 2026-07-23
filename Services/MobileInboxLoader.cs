using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BueroCockpit.Data;
using BueroCockpit.Models;

namespace BueroCockpit.Services;

public sealed class MobileInboxLoader
{
    public List<MobileInboxEntry> Load(params string[] basePaths)
    {
        var inboxDirectory = ResolveMobileInboxDirectory(basePaths);
        if (inboxDirectory is null || !Directory.Exists(inboxDirectory))
        {
            return new List<MobileInboxEntry>();
        }

        var entries = new List<MobileInboxEntry>();
        foreach (var entryDirectory in Directory.EnumerateDirectories(inboxDirectory, "mobile-*").OrderByDescending(Path.GetFileName))
        {
            var jsonPath = Path.Combine(entryDirectory, "aufgabe.json");
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            try
            {
                var entry = ReadEntry(entryDirectory, jsonPath);
                if (string.Equals(entry.Status, "new", StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mobile inbox entry skipped: {jsonPath}: {ex}");
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenBy(entry => entry.DisplayCustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string? ResolveMobileInboxDirectory(IEnumerable<string> basePaths)
    {
        var candidates = new List<string>();
        foreach (var basePath in basePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            AddCandidates(basePath, candidates);
        }

        if (candidates.Count == 0)
        {
            AddCandidates(AppPaths.AppDataDirectory, candidates);
        }

        foreach (var candidate in candidates)
        {
            if (IsUsableInboxDirectory(candidate))
            {
                return candidate;
            }
        }

        return candidates.FirstOrDefault();
    }

    private static void AddCandidates(string path, List<string> candidates)
    {
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        var baseDirectory = File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "mobile-inbox", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(baseDirectory, candidates);
            return;
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "inbox", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Directory.GetParent(baseDirectory)?.Name, "Sync", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(baseDirectory, candidates);
            return;
        }

        AddCandidate(Path.Combine(baseDirectory, "Sync", "inbox"), candidates);
        if (string.Equals(Path.GetFileName(baseDirectory), "Sync", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(Path.Combine(baseDirectory, "inbox"), candidates);
        }
        AddCandidate(Path.Combine(baseDirectory, "mobile-inbox"), candidates);
        AddCandidate(baseDirectory, candidates);

        var parent = Directory.GetParent(baseDirectory);
        if (parent is not null)
        {
            AddCandidate(Path.Combine(parent.FullName, "mobile-inbox"), candidates);
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "live", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(baseDirectory), "Sync", StringComparison.OrdinalIgnoreCase))
        {
            var syncParent = Directory.GetParent(baseDirectory);
            if (syncParent is not null)
            {
                AddCandidate(Path.Combine(syncParent.FullName, "mobile-inbox"), candidates);
            }
        }
    }

    private static bool IsUsableInboxDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        if (string.Equals(Path.GetFileName(path), "mobile-inbox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Directory.EnumerateDirectories(path, "mobile-*").Any();
    }

    private static void AddCandidate(string candidate, List<string> candidates)
    {
        if (candidates.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidates.Add(candidate);
    }

    private static MobileInboxEntry ReadEntry(string entryDirectory, string jsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = document.RootElement;
        var createdAt = ReadDateTime(root, "createdAt") ?? TryParseDateFromDirectoryName(entryDirectory) ?? File.GetCreationTime(jsonPath);
        var id = ReadString(root, "id");

        return new MobileInboxEntry
        {
            Id = string.IsNullOrWhiteSpace(id) ? Path.GetFileName(entryDirectory) : id,
            DirectoryPath = entryDirectory,
            JsonPath = jsonPath,
            CreatedAt = createdAt,
            Status = ReadString(root, "status", "new"),
            CustomerName = ReadFirstString(root, "customerName", "kunde"),
            Address = ReadFirstString(root, "address", "adresse"),
            Phone = ReadFirstString(root, "phone", "telefon"),
            Email = ReadString(root, "email"),
            Title = ReadFirstString(root, "title", "titel", "Mobile Besichtigung"),
            Category = ReadFirstString(root, "category", "kategorie", "categoryName", "Kategorie"),
            Notes = ReadFirstString(root, "notes", "notiz"),
            SchemaVersion = ReadInt(root, "schemaVersion", 1),
            Operation = ReadString(root, "operation", "create"),
            DesktopTaskId = ReadString(root, "desktopTaskId"),
            BaseRevision = ReadString(root, "baseRevision"),
            ConfirmedRevision = ReadString(root, "confirmedRevision"),
            CategoryId = ReadString(root, "categoryId"),
            WorkflowType = ReadString(root, "workflowType"),
            WorkflowStep = ReadFirstString(root, "workflowStep", "status"),
            DueDate = ReadDateTime(root, "dueDate"),
            FollowUpDate = ReadDateTime(root, "followUpDate"),
            FollowUpReason = ReadString(root, "followUpReason"),
            Technician = ReadString(root, "technician"),
            BaseValues = ReadRevisionValues(root),
            PhotoPreviews = ReadPhotoPreviewItems(entryDirectory, root),
            SketchPreviews = ReadSketchPreviewItems(entryDirectory, root),
            FilePreviews = ReadFilePreviewItems(entryDirectory, root),
            OriginalPhotoPaths = ReadOriginalPhotoPaths(entryDirectory, root)
        };
    }

    private static string ReadString(JsonElement root, string propertyName, string defaultValue = "")
    {
        return TryGetProperty(root, propertyName, out var element) ? ConvertToString(element) : defaultValue;
    }

    private static string ReadString(JsonElement root, string propertyName, string fallbackPropertyName, string defaultValue = "")
    {
        var value = ReadString(root, propertyName);
        return string.IsNullOrWhiteSpace(value) ? ReadString(root, fallbackPropertyName, defaultValue) : value;
    }

    private static string ReadFirstString(JsonElement root, string propertyName, string fallbackPropertyName, string defaultValue = "")
    {
        var value = ReadString(root, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = ReadString(root, fallbackPropertyName);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string ReadFirstExistingString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(root, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static DateTime? ReadDateTime(JsonElement root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static int ReadInt(JsonElement root, string propertyName, int defaultValue)
    {
        if (!TryGetProperty(root, propertyName, out var element))
        {
            return defaultValue;
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value)
            ? value
            : int.TryParse(ConvertToString(element), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
    }

    private static MobileTaskRevisionValues? ReadRevisionValues(JsonElement root)
    {
        if (!TryGetProperty(root, "baseValues", out var values) || values.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new MobileTaskRevisionValues
        {
            Notes = ReadString(values, "notes"),
            CategoryId = ReadString(values, "categoryId"),
            WorkflowType = ReadString(values, "workflowType"),
            WorkflowStep = ReadString(values, "workflowStep"),
            DueDate = ReadDateTime(values, "dueDate"),
            FollowUpDate = ReadDateTime(values, "followUpDate"),
            FollowUpReason = ReadString(values, "followUpReason"),
            Technician = ReadString(values, "technician")
        };
    }

    private static List<MobileInboxPreviewItem> ReadSketchPreviewItems(string entryDirectory, JsonElement root)
    {
        var items = new List<MobileInboxPreviewItem>();
        if (TryGetProperty(root, "sketches", out var arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElement in arrayElement.EnumerateArray())
            {
                var path = ReadPreviewPath(itemElement);
                AddPreviewItem(entryDirectory, items, path, "sketches");
            }
        }

        var fallbackDirectory = Path.Combine(entryDirectory, "sketches");
        if (Directory.Exists(fallbackDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(fallbackDirectory).OrderBy(Path.GetFileName))
            {
                if (!IsPreviewImagePath(path))
                {
                    continue;
                }

                AddPreviewItem(entryDirectory, items, path, "sketches");
            }
        }

        return items;
    }

    private static List<MobileInboxPreviewItem> ReadPhotoPreviewItems(string entryDirectory, JsonElement root)
    {
        var items = new List<MobileInboxPreviewItem>();
        if (TryGetProperty(root, "photos", out var arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElement in arrayElement.EnumerateArray())
            {
                var path = ReadPreviewPath(itemElement);
                var originalPath = itemElement.ValueKind == JsonValueKind.Object
                    ? ReadString(itemElement, "originalPath", "original")
                    : ConvertToString(itemElement);
                AddPreviewItem(entryDirectory, items, path, "previews", originalPath);

                if (itemElement.ValueKind == JsonValueKind.Object)
                {
                    var annotatedPath = ReadFirstExistingString(
                        itemElement,
                        "annotatedPath",
                        "annotated",
                        "markedPath");
                    var annotatedPreviewPath = ReadFirstExistingString(
                        itemElement,
                        "annotatedPreviewPath",
                        "annotatedPreview",
                        "markedPreviewPath");
                    if (string.IsNullOrWhiteSpace(annotatedPreviewPath))
                    {
                        annotatedPreviewPath = annotatedPath;
                    }

                    AddPreviewItem(entryDirectory, items, annotatedPreviewPath, "annotated", annotatedPath);
                }
            }
        }

        var previewsDirectory = Path.Combine(entryDirectory, "previews");
        if (Directory.Exists(previewsDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(previewsDirectory).OrderBy(Path.GetFileName))
            {
                AddPreviewItem(entryDirectory, items, path, "previews");
            }
        }

        var annotatedDirectory = Path.Combine(entryDirectory, "annotated");
        if (Directory.Exists(annotatedDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(annotatedDirectory, "*thumb*", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName))
            {
                AddPreviewItem(entryDirectory, items, path, "annotated");
            }
        }

        return items;
    }

    private static List<MobileInboxPreviewItem> ReadFilePreviewItems(string entryDirectory, JsonElement root)
    {
        var items = new List<MobileInboxPreviewItem>();
        if (TryGetProperty(root, "files", out var arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElement in arrayElement.EnumerateArray())
            {
                var path = itemElement.ValueKind == JsonValueKind.Object
                    ? ReadFirstExistingString(itemElement, "path", "filePath")
                    : ConvertToString(itemElement);
                AddPreviewItem(entryDirectory, items, path, "file", path);
            }
        }

        var filesDirectory = Path.Combine(entryDirectory, "files");
        if (Directory.Exists(filesDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(filesDirectory).OrderBy(Path.GetFileName))
            {
                AddPreviewItem(entryDirectory, items, path, "file", path);
            }
        }

        return items;
    }

    private static string ReadPreviewPath(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return ConvertToString(element);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return ReadFirstExistingString(element, "previewPath", "preview", "thumbnailPath", "path");
    }

    private static IReadOnlyList<string> ReadOriginalPhotoPaths(string entryDirectory, JsonElement root)
    {
        var paths = new List<string>();
        if (TryGetProperty(root, "photos", out var photosElement) && photosElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var photoElement in photosElement.EnumerateArray())
            {
                var originalPath = photoElement.ValueKind == JsonValueKind.Object
                    ? ReadString(photoElement, "originalPath", "original")
                    : ConvertToString(photoElement);
                AddResolvedPhotoPath(entryDirectory, paths, originalPath, "originals");

                if (photoElement.ValueKind == JsonValueKind.Object)
                {
                    var annotatedPath = ReadFirstExistingString(
                        photoElement,
                        "annotatedPath",
                        "annotated",
                        "markedPath");
                    AddResolvedPhotoPath(entryDirectory, paths, annotatedPath, "annotated");
                }
            }
        }

        var originalsDirectory = Path.Combine(entryDirectory, "originals");
        if (Directory.Exists(originalsDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(originalsDirectory).OrderBy(Path.GetFileName))
            {
                if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
    }

    private static void AddResolvedPhotoPath(string entryDirectory, List<string> paths, string? path, string fallbackDirectoryName)
    {
        var resolvedPath = ResolveEntryPath(entryDirectory, path, fallbackDirectoryName);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && !paths.Contains(resolvedPath, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(resolvedPath);
        }
    }

    private static void AddPreviewItem(
        string entryDirectory,
        List<MobileInboxPreviewItem> items,
        string? path,
        string kind,
        string? detailPath = null)
    {
        var resolvedPath = ResolveEntryPath(entryDirectory, path, kind);
        if (string.IsNullOrWhiteSpace(resolvedPath) ||
            (string.Equals(kind, "previews", StringComparison.OrdinalIgnoreCase) && IsOriginalsPath(resolvedPath)) ||
            items.Any(item => string.Equals(item.Path, resolvedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        items.Add(new MobileInboxPreviewItem
        {
            FileName = Path.GetFileName(resolvedPath),
            Path = resolvedPath,
            DetailPath = ResolveEntryPath(entryDirectory, detailPath, GetDetailFallbackDirectoryName(kind)),
            Kind = kind
        });
    }

    private static string ResolveEntryPath(string entryDirectory, string? path, string fallbackDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim();
        if (!Path.IsPathRooted(trimmedPath))
        {
            return Path.GetFullPath(Path.Combine(entryDirectory, trimmedPath));
        }

        var fullPath = Path.GetFullPath(trimmedPath);
        if (IsPathInsideDirectory(fullPath, entryDirectory))
        {
            return fullPath;
        }

        return TryResolveLocalCopy(entryDirectory, fullPath, fallbackDirectoryName) ?? fullPath;
    }

    private static string GetDetailFallbackDirectoryName(string kind)
    {
        return string.Equals(kind, "annotated", StringComparison.OrdinalIgnoreCase)
            ? "annotated"
            : kind;
    }

    private static string? TryResolveLocalCopy(string entryDirectory, string absolutePath, string fallbackDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(fallbackDirectoryName))
        {
            return null;
        }

        var fallbackDirectory = Path.Combine(entryDirectory, fallbackDirectoryName);
        if (!Directory.Exists(fallbackDirectory))
        {
            return null;
        }

        var fileName = Path.GetFileName(absolutePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var exactMatch = Directory
                .EnumerateFiles(fallbackDirectory, fileName, SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return Path.GetFullPath(exactMatch);
            }
        }

        var imageMatches = Directory
            .EnumerateFiles(fallbackDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsPreviewImagePath)
            .ToList();
        return imageMatches.Count == 1 ? Path.GetFullPath(imageMatches[0]) : null;
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(
            normalizedDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOriginalsPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/originals/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreviewImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ConvertToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static string ReadFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static DateTime? TryParseDateFromDirectoryName(string entryDirectory)
    {
        var directoryName = Path.GetFileName(entryDirectory);
        var match = Regex.Match(directoryName, @"^mobile-(\d{8})-(\d{6})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return DateTime.TryParseExact(
            $"{match.Groups[1].Value}-{match.Groups[2].Value}",
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : null;
    }
}
