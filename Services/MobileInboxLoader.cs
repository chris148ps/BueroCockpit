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
        var baseDirectory = IsBcliveFilePath(fullPath) || File.Exists(fullPath)
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

    private static bool IsBcliveFilePath(string path)
    {
        return string.Equals(Path.GetFileName(path), "live.bclive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(path), ".bclive", StringComparison.OrdinalIgnoreCase);
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
            PhotoPreviews = ReadPhotoPreviewItems(entryDirectory, root),
            SketchPreviews = ReadPreviewItems(entryDirectory, root, "sketches", "sketches"),
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

    private static DateTime? ReadDateTime(JsonElement root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static List<MobileInboxPreviewItem> ReadPreviewItems(string entryDirectory, JsonElement root, string arrayName, string fallbackDirectoryName)
    {
        var items = new List<MobileInboxPreviewItem>();
        if (TryGetProperty(root, arrayName, out var arrayElement) && arrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemElement in arrayElement.EnumerateArray())
            {
                var path = ReadPreviewPath(itemElement);
                AddPreviewItem(entryDirectory, items, path, fallbackDirectoryName);
            }
        }

        var fallbackDirectory = Path.Combine(entryDirectory, fallbackDirectoryName);
        if (Directory.Exists(fallbackDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(fallbackDirectory).OrderBy(Path.GetFileName))
            {
                AddPreviewItem(entryDirectory, items, path, fallbackDirectoryName);
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
                AddPreviewItem(entryDirectory, items, path, "previews");

                if (itemElement.ValueKind == JsonValueKind.Object)
                {
                    var annotatedPreviewPath = ReadFirstString(
                        itemElement,
                        "annotatedPreviewPath",
                        "annotatedPreview",
                        "markedPreviewPath");
                    AddPreviewItem(entryDirectory, items, annotatedPreviewPath, "annotated");
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

        return ReadFirstString(element, "previewPath", "preview", "thumbnailPath", "path");
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
                AddResolvedPhotoPath(entryDirectory, paths, originalPath);

                if (photoElement.ValueKind == JsonValueKind.Object)
                {
                    var annotatedPath = ReadFirstString(
                        photoElement,
                        "annotatedPath",
                        "annotated",
                        "markedPath");
                    AddResolvedPhotoPath(entryDirectory, paths, annotatedPath);
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

    private static void AddResolvedPhotoPath(string entryDirectory, List<string> paths, string? path)
    {
        var resolvedPath = ResolveEntryPath(entryDirectory, path);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && !paths.Contains(resolvedPath, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(resolvedPath);
        }
    }

    private static void AddPreviewItem(string entryDirectory, List<MobileInboxPreviewItem> items, string? path, string kind)
    {
        var resolvedPath = ResolveEntryPath(entryDirectory, path);
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
            Kind = kind
        });
    }

    private static string ResolveEntryPath(string entryDirectory, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim();
        return Path.IsPathRooted(trimmedPath)
            ? Path.GetFullPath(trimmedPath)
            : Path.GetFullPath(Path.Combine(entryDirectory, trimmedPath));
    }

    private static bool IsOriginalsPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/originals/", StringComparison.OrdinalIgnoreCase);
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
