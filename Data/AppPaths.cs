namespace BueroCockpit.Data;

public static class AppPaths
{
    private const string AppFolderName = "BueroCockpit";
    private const string LocalConfigFolderName = "BueroCockpitLocal";
    private const string DeskItemsRelativeDirectory = "DeskItems";
    private const string DeskFilesRelativeDirectory = "DeskItems/Files";

    public static string DefaultAppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    public static string LocalConfigDirectory { get; } = GetLocalConfigDirectory();

    public static string AppDataDirectory { get; private set; } = DefaultAppDataDirectory;
    public static string BootstrapSettingsPath => Path.Combine(LocalConfigDirectory, "storage-location.local.json");
    public static string LegacyBootstrapSettingsPath => Path.Combine(DefaultAppDataDirectory, "storage-location.json");
    public static string DatabasePath => Path.Combine(AppDataDirectory, "buerocockpit.db");
    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");
    public static string LockPath => Path.Combine(AppDataDirectory, "buerocockpit.lock");
    public static string TasksDirectory => Path.Combine(AppDataDirectory, "Tasks");
    public static string BackupDirectory => Path.Combine(AppDataDirectory, "Backups");
    public static string DeskItemsDirectory => Path.Combine(AppDataDirectory, "DeskItems");
    public static string DeskFilesDirectory => Path.Combine(DeskItemsDirectory, "Files");

    public static bool IsUsingCustomDataDirectory =>
        !string.Equals(AppDataDirectory, DefaultAppDataDirectory, StringComparison.Ordinal);

    public static void UseDefaultAppDataDirectory()
    {
        AppDataDirectory = DefaultAppDataDirectory;
    }

    public static void UseAppDataDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            UseDefaultAppDataDirectory();
            return;
        }

        AppDataDirectory = Path.GetFullPath(ExpandHomeDirectory(Environment.ExpandEnvironmentVariables(directory.Trim())));
    }

    public static string GetAttachmentDirectory(string taskId)
    {
        return Path.Combine(AppDataDirectory, "Tasks", taskId, "Attachments");
    }

    public static string GetAttachmentBackupDirectory(string taskId)
    {
        return Path.Combine(AppDataDirectory, "Tasks", taskId, "AttachmentBackups");
    }

    public static string ResolveDataPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim();

        // Wichtig für macOS/Linux:
        // Windows-Pfade wie "C:\Users\Installation\AppData\Roaming\BueroCockpit\Tasks\..."
        // gelten dort nicht als rooted path. Deshalb zuerst auf Tasks/...,
        // DeskItems/... usw. kürzen und dann relativ zum aktuellen Datenordner auflösen.
        if (TryGetRelativeLegacyBueroCockpitPath(trimmedPath, out var legacyRelativePath))
        {
            return GetAbsolutePathFromRelative(legacyRelativePath);
        }

        if (!Path.IsPathRooted(trimmedPath))
        {
            return GetAbsolutePathFromRelative(trimmedPath);
        }

        if (TryGetRelativeToCurrentAppData(trimmedPath, out var relativePath))
        {
            var currentPath = GetAbsolutePathFromRelative(relativePath);
            if (File.Exists(currentPath) || Directory.Exists(currentPath))
            {
                return currentPath;
            }

            if (File.Exists(trimmedPath) || Directory.Exists(trimmedPath))
            {
                return NormalizeFullPath(trimmedPath);
            }

            return currentPath;
        }

        return NormalizeFullPath(trimmedPath);
    }

    public static string ResolveStoredPath(string? path)
    {
        return ResolveDataPath(path);
    }

    public static string ResolveTaskAttachmentPath(string taskId, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim();

        if (TryGetRelativeLegacyBueroCockpitPath(trimmedPath, out var legacyRelativePath))
        {
            return GetAbsolutePathFromRelative(legacyRelativePath);
        }

        if (Path.IsPathRooted(trimmedPath))
        {
            return ResolveDataPath(trimmedPath);
        }

        var relativePath = NormalizeRelativePath(trimmedPath);
        if (relativePath.StartsWith("Tasks/", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDataPath(relativePath);
        }

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            return GetAbsolutePathFromRelative(CombineRelativePath("Tasks", taskId, "Attachments", relativePath));
        }

        return ResolveDataPath(relativePath);
    }

    public static string GetDeskPdfDirectory(string deskItemId)
    {
        return GetDeskFileDirectory(deskItemId);
    }

    public static string GetDeskFileDirectory(string deskItemId)
    {
        return Path.Combine(DeskFilesDirectory, deskItemId);
    }

    public static string GetDeskFilePath(string deskItemId, string fileName)
    {
        return Path.Combine(GetDeskFileDirectory(deskItemId), fileName);
    }

    public static string GetDeskThumbnailPath(string deskItemId)
    {
        return Path.Combine(GetDeskFileDirectory(deskItemId), "Thumbnails", $"{deskItemId}.png");
    }

    public static string GetDeskFileRelativePath(string deskItemId, string fileName)
    {
        return CombineRelativePath(DeskItemsRelativeDirectory, "Files", deskItemId, fileName);
    }

    public static string GetDeskThumbnailRelativePath(string deskItemId)
    {
        return CombineRelativePath(DeskItemsRelativeDirectory, "Files", deskItemId, "Thumbnails", $"{deskItemId}.png");
    }

    public static string ResolveDeskItemPath(string? path)
    {
        return ResolveDataPath(path);
    }

    public static string MakeRelativeToAppDataDirectory(string? path)
    {
        return ToStoredPath(path);
    }

    public static string ToStoredPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.Trim();

        if (TryGetRelativeLegacyBueroCockpitPath(trimmedPath, out var legacyRelativePath))
        {
            return NormalizeRelativePath(legacyRelativePath).Replace('\\', '/');
        }

        if (!Path.IsPathRooted(trimmedPath))
        {
            return NormalizeRelativePath(trimmedPath).Replace('\\', '/');
        }

        if (TryGetRelativeToCurrentAppData(trimmedPath, out var relativePath))
        {
            return NormalizeRelativePath(relativePath).Replace('\\', '/');
        }

        return NormalizeFullPath(trimmedPath);
    }

    public static string MakeRelativeToDataFolder(string? path)
    {
        return ToStoredPath(path);
    }

    public static bool PathsEqual(string? firstPath, string? secondPath)
    {
        var normalizedFirstPath = NormalizePathForComparison(firstPath);
        var normalizedSecondPath = NormalizePathForComparison(secondPath);
        if (string.IsNullOrWhiteSpace(normalizedFirstPath) || string.IsNullOrWhiteSpace(normalizedSecondPath))
        {
            return false;
        }

        return string.Equals(normalizedFirstPath, normalizedSecondPath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDeskFileStoragePath(string? path)
    {
        var relativePath = ToStoredPath(path);
        return !string.IsNullOrWhiteSpace(relativePath) &&
               !Path.IsPathRooted(relativePath) &&
               relativePath.StartsWith($"{DeskFilesRelativeDirectory}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathInsideAppDataDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var resolvedPath = ResolveStoredPath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        return IsSameOrChildPath(NormalizeFullPath(resolvedPath), NormalizeFullPath(AppDataDirectory));
    }

    public static void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(DefaultAppDataDirectory);
        Directory.CreateDirectory(LocalConfigDirectory);
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(TasksDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(DeskItemsDirectory);
        Directory.CreateDirectory(DeskFilesDirectory);
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            path.StartsWith($"~{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }

    private static string GetLocalConfigDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folderName = OperatingSystem.IsMacOS()
            ? LocalConfigFolderName
            : AppFolderName;

        return Path.Combine(localApplicationData, folderName);
    }

    private static string GetAbsolutePathFromRelative(string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(Path.Combine(AppDataDirectory, normalizedRelativePath));
        }
        catch
        {
            return Path.Combine(AppDataDirectory, normalizedRelativePath);
        }
    }

    private static string CombineRelativePath(params string[] segments)
    {
        return string.Join(
            '/',
            segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => NormalizeRelativePath(segment))
                .Select(segment => segment.Trim('/')));
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    private static string NormalizeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static string NormalizePathForComparison(string? path)
    {
        var resolvedPath = ResolveDataPath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(resolvedPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static bool TryGetRelativeToCurrentAppData(string absolutePath, out string relativePath)
    {
        var normalizedAbsolutePath = NormalizeFullPath(absolutePath);
        var normalizedCurrentRoot = NormalizeFullPath(AppDataDirectory);

        if (IsSameOrChildPath(normalizedAbsolutePath, normalizedCurrentRoot))
        {
            relativePath = NormalizeRelativePath(Path.GetRelativePath(normalizedCurrentRoot, normalizedAbsolutePath));
            return true;
        }

        return TryGetRelativeLegacyBueroCockpitPath(normalizedAbsolutePath, out relativePath);
    }

    private static bool TryGetRelativeLegacyBueroCockpitPath(string path, out string relativePath)
    {
        relativePath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim().Replace('\\', '/');

        // Plattformwechsel-Fix:
        // Alte absolute Windows-/Mac-Pfade sollen direkt ab den bekannten
        // Daten-Unterordnern gekürzt werden. Damit ist es egal, ob davor
        // "C:/Users/Installation/AppData/Roaming/BueroCockpit" oder ein
        // OneDrive-/macOS-Stammordner steht.
        var knownRelativeRoots = new[]
        {
            "Tasks/",
            "DeskItems/",
            "Backups/"
        };

        foreach (var knownRoot in knownRelativeRoots)
        {
            var marker = "/" + knownRoot;
            var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (markerIndex >= 0)
            {
                var suffix = normalized[(markerIndex + 1)..];
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    relativePath = NormalizeRelativePath(suffix);
                    return true;
                }
            }

            if (normalized.StartsWith(knownRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = NormalizeRelativePath(normalized);
                return true;
            }
        }

        var markerByAppFolder = $"/{AppFolderName}/";
        var markerByAppFolderIndex = normalized.IndexOf(markerByAppFolder, StringComparison.OrdinalIgnoreCase);

        if (markerByAppFolderIndex >= 0)
        {
            var suffix = normalized[(markerByAppFolderIndex + markerByAppFolder.Length)..];
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                relativePath = NormalizeRelativePath(suffix);
                return true;
            }
        }

        var oldAppFolderPrefix = "BueroCockpit/";
        var oldAppFolderIndex = normalized.IndexOf(oldAppFolderPrefix, StringComparison.OrdinalIgnoreCase);

        if (oldAppFolderIndex >= 0)
        {
            var suffix = normalized[(oldAppFolderIndex + oldAppFolderPrefix.Length)..];
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                relativePath = NormalizeRelativePath(suffix);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRelativeDeskItemPath(string absolutePath, out string relativePath)
    {
        var parts = absolutePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        var appIndex = Array.FindIndex(parts, segment => string.Equals(segment, AppFolderName, StringComparison.OrdinalIgnoreCase));
        if (appIndex < 0 || appIndex >= parts.Length - 1)
        {
            relativePath = string.Empty;
            return false;
        }

        var suffix = NormalizeRelativePath(string.Join('/', parts[(appIndex + 1)..]));
        if (string.IsNullOrWhiteSpace(suffix))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = suffix;
        return true;
    }

    private static bool IsSameOrChildPath(string path, string rootPath)
    {
        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.StartsWith($"{normalizedRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith($"{normalizedRoot}{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
}
