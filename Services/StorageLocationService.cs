using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class StorageLocationService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly BackupService _backupService = new();

    public void ApplyConfiguredDataDirectory()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DefaultAppDataDirectory);
            if (!File.Exists(AppPaths.BootstrapSettingsPath))
            {
                AppPaths.UseDefaultAppDataDirectory();
                return;
            }

            var json = File.ReadAllText(AppPaths.BootstrapSettingsPath);
            var settings = JsonSerializer.Deserialize<StorageLocationSettings>(json, Options);
            AppPaths.UseAppDataDirectory(settings?.CustomDataDirectory);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Storage location could not be loaded: {ex}");
            AppPaths.UseDefaultAppDataDirectory();
        }
    }

    public StorageLocationPreparationResult PrepareCustomDataDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return StorageLocationPreparationResult.Fail("Kein Zielordner ausgewählt.");
        }

        try
        {
            var targetDirectory = Path.GetFullPath(directory.Trim());
            Directory.CreateDirectory(targetDirectory);
            EnsureWritable(targetDirectory);

            var targetDatabasePath = Path.Combine(targetDirectory, "buerocockpit.db");
            if (File.Exists(targetDatabasePath))
            {
                return StorageLocationPreparationResult.Fail(
                    "Der gewählte Ordner enthält bereits eine BueroCockpit-Datenbank. Es wurde nichts überschrieben und kein Speicherort geändert.");
            }

            Directory.CreateDirectory(Path.Combine(targetDirectory, "Tasks"));
            Directory.CreateDirectory(Path.Combine(targetDirectory, "Backups"));

            WriteBootstrapSettings(targetDirectory);

            return StorageLocationPreparationResult.Success(
                targetDirectory,
                "Speicherort vorbereitet. Bitte BueroCockpit neu starten. Bestehende Daten wurden nicht verschoben oder gelöscht.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Storage location could not be prepared: {ex}");
            return StorageLocationPreparationResult.Fail("Speicherort konnte nicht vorbereitet werden. Bitte Ordner und Schreibrechte prüfen.");
        }
    }

    public StorageLocationMigrationResult MigrateToCustomDataDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return StorageLocationMigrationResult.Fail("Kein Zielordner ausgewählt.");
        }

        try
        {
            var sourceDirectory = Path.GetFullPath(AppPaths.AppDataDirectory);
            var targetDirectory = Path.GetFullPath(directory.Trim());

            if (AreSameDirectory(sourceDirectory, targetDirectory))
            {
                return StorageLocationMigrationResult.Fail("Der Zielordner ist bereits der aktive Datenordner. Es wurde nichts geändert.");
            }

            if (IsSubdirectoryOf(targetDirectory, sourceDirectory))
            {
                return StorageLocationMigrationResult.Fail("Der Zielordner darf nicht innerhalb des aktuell aktiven Datenordners liegen.");
            }

            Directory.CreateDirectory(targetDirectory);
            EnsureWritable(targetDirectory);

            var preflightError = ValidateMigrationTarget(targetDirectory);
            if (preflightError is not null)
            {
                return StorageLocationMigrationResult.Fail(preflightError);
            }

            BackupResult backupResult;
            try
            {
                backupResult = _backupService.CreateBackup();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Storage migration backup failed: {ex}");
                return StorageLocationMigrationResult.Fail("Backup vor der Migration konnte nicht erstellt werden. Speicherort wurde nicht geändert.");
            }

            if (backupResult.SkippedFiles > 0)
            {
                return StorageLocationMigrationResult.Fail(
                    "Backup vor der Migration war unvollständig. Speicherort wurde nicht geändert.");
            }

            CopyKnownAppData(sourceDirectory, targetDirectory);
            WriteBootstrapSettings(targetDirectory);

            return StorageLocationMigrationResult.Success(
                targetDirectory,
                backupResult.BackupPath,
                "Daten wurden kopiert. Bitte BueroCockpit neu starten, damit der neue Speicherort aktiv wird.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Storage migration failed: {ex}");
            return StorageLocationMigrationResult.Fail("Datenordner konnte nicht kopiert werden. Speicherort wurde nicht geändert.");
        }
    }

    private static void EnsureWritable(string directory)
    {
        var testPath = Path.Combine(directory, $".buerocockpit_write_test_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(testPath, "test");
        File.Delete(testPath);
    }

    private static string? ValidateMigrationTarget(string targetDirectory)
    {
        if (File.Exists(Path.Combine(targetDirectory, "buerocockpit.db")))
        {
            return "Der Zielordner enthält bereits eine BueroCockpit-Datenbank. Es wurde nichts überschrieben und kein Speicherort geändert.";
        }

        if (File.Exists(Path.Combine(targetDirectory, "settings.json")))
        {
            return "Der Zielordner enthält bereits BueroCockpit-Einstellungen. Bitte einen leeren Zielordner wählen.";
        }

        foreach (var directoryName in new[] { "Tasks", "Backups" })
        {
            var existingDirectory = Path.Combine(targetDirectory, directoryName);
            if (Directory.Exists(existingDirectory) && Directory.EnumerateFileSystemEntries(existingDirectory).Any())
            {
                return $"Der Zielordner enthält bereits Daten im Ordner {directoryName}. Bitte einen leeren Zielordner wählen.";
            }
        }

        var unexpectedEntry = Directory.EnumerateFileSystemEntries(targetDirectory)
            .FirstOrDefault(entry => !IsAllowedEmptyMigrationDirectory(entry));
        if (unexpectedEntry is not null)
        {
            return "Der Zielordner ist nicht leer. Bitte einen leeren Zielordner wählen.";
        }

        return null;
    }

    private static bool IsAllowedEmptyMigrationDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        var name = Path.GetFileName(path);
        return (string.Equals(name, "Tasks", StringComparison.Ordinal) ||
                string.Equals(name, "Backups", StringComparison.Ordinal)) &&
               !Directory.EnumerateFileSystemEntries(path).Any();
    }

    private static void CopyKnownAppData(string sourceDirectory, string targetDirectory)
    {
        CopyFileIfExists(Path.Combine(sourceDirectory, "buerocockpit.db"), Path.Combine(targetDirectory, "buerocockpit.db"));
        CopyFileIfExists(Path.Combine(sourceDirectory, "settings.json"), Path.Combine(targetDirectory, "settings.json"));
        CopyDirectoryIfExists(Path.Combine(sourceDirectory, "Tasks"), Path.Combine(targetDirectory, "Tasks"));
        CopyDirectoryIfExists(Path.Combine(sourceDirectory, "Backups"), Path.Combine(targetDirectory, "Backups"));
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }

    private static void CopyDirectoryIfExists(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectoryIfExists(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            CopyFileIfExists(file, Path.Combine(targetDirectory, Path.GetFileName(file)));
        }
    }

    private static bool AreSameDirectory(string firstDirectory, string secondDirectory)
    {
        var first = TrimDirectorySeparators(firstDirectory);
        var second = TrimDirectorySeparators(secondDirectory);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(first, second, comparison);
    }

    private static string TrimDirectorySeparators(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSubdirectoryOf(string candidateDirectory, string parentDirectory)
    {
        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidateDirectory));
        var parent = EnsureTrailingSeparator(Path.GetFullPath(parentDirectory));
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return candidate.StartsWith(parent, comparison) && !AreSameDirectory(candidate, parent);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return TrimDirectorySeparators(path) + Path.DirectorySeparatorChar;
    }

    private static void WriteBootstrapSettings(string targetDirectory)
    {
        Directory.CreateDirectory(AppPaths.DefaultAppDataDirectory);
        var settings = new StorageLocationSettings { CustomDataDirectory = targetDirectory };
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(AppPaths.BootstrapSettingsPath, json);
    }
}

public sealed class StorageLocationSettings
{
    public string CustomDataDirectory { get; set; } = string.Empty;
}

public sealed record StorageLocationPreparationResult(bool IsSuccess, string? Directory, string Message)
{
    public static StorageLocationPreparationResult Success(string directory, string message)
    {
        return new StorageLocationPreparationResult(true, directory, message);
    }

    public static StorageLocationPreparationResult Fail(string message)
    {
        return new StorageLocationPreparationResult(false, null, message);
    }
}

public sealed record StorageLocationMigrationResult(bool IsSuccess, string? Directory, string? BackupPath, string Message)
{
    public static StorageLocationMigrationResult Success(string directory, string backupPath, string message)
    {
        return new StorageLocationMigrationResult(true, directory, backupPath, message);
    }

    public static StorageLocationMigrationResult Fail(string message)
    {
        return new StorageLocationMigrationResult(false, null, null, message);
    }
}
