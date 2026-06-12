using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class StorageLocationService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

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
                    "Der gewählte Ordner enthält bereits eine BüroCockpit-Datenbank. Es wurde nichts überschrieben und kein Speicherort geändert.");
            }

            Directory.CreateDirectory(Path.Combine(targetDirectory, "Tasks"));
            Directory.CreateDirectory(Path.Combine(targetDirectory, "Backups"));

            Directory.CreateDirectory(AppPaths.DefaultAppDataDirectory);
            var settings = new StorageLocationSettings { CustomDataDirectory = targetDirectory };
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(AppPaths.BootstrapSettingsPath, json);

            return StorageLocationPreparationResult.Success(
                targetDirectory,
                "Speicherort vorbereitet. Bitte BüroCockpit neu starten. Bestehende Daten wurden nicht verschoben oder gelöscht.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Storage location could not be prepared: {ex}");
            return StorageLocationPreparationResult.Fail("Speicherort konnte nicht vorbereitet werden. Bitte Ordner und Schreibrechte prüfen.");
        }
    }

    private static void EnsureWritable(string directory)
    {
        var testPath = Path.Combine(directory, $".buerocockpit_write_test_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(testPath, "test");
        File.Delete(testPath);
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
