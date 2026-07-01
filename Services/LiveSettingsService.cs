using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BueroCockpit.Services;

public sealed class LiveSettings
{
    [JsonPropertyName("technicianNames")]
    public List<string> TechnicianNames { get; set; } = [];
}

public sealed class LiveSettingsService
{
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LiveSettings Load(string syncRootDirectory, IEnumerable<string>? localTechnicianNames)
    {
        if (string.IsNullOrWhiteSpace(syncRootDirectory))
        {
            return new LiveSettings();
        }

        var settingsPath = GetSettingsPath(syncRootDirectory);
        var settings = LoadExistingSettings(settingsPath, out var wasInvalid);
        settings.TechnicianNames = NormalizeTechnicianNames(settings.TechnicianNames);

        if (settings.TechnicianNames.Count == 0)
        {
            var localNames = NormalizeTechnicianNames(localTechnicianNames);
            if (localNames.Count > 0)
            {
                settings.TechnicianNames = localNames;
                Save(syncRootDirectory, settings);
                Debug.WriteLine($"Live settings migrated local technician names to {settingsPath}");
                return settings;
            }
        }

        if (!File.Exists(settingsPath) || wasInvalid)
        {
            Save(syncRootDirectory, settings);
        }

        return settings;
    }

    public void Save(string syncRootDirectory, LiveSettings settings)
    {
        if (string.IsNullOrWhiteSpace(syncRootDirectory))
        {
            return;
        }

        var settingsPath = GetSettingsPath(syncRootDirectory);
        try
        {
            settings.TechnicianNames = NormalizeTechnicianNames(settings.TechnicianNames);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? syncRootDirectory);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Live settings could not be saved: {settingsPath} | {ex}");
        }
    }

    public static string GetSettingsPath(string syncRootDirectory)
    {
        return Path.Combine(syncRootDirectory, "live", SettingsFileName);
    }

    private static LiveSettings LoadExistingSettings(string settingsPath, out bool wasInvalid)
    {
        wasInvalid = false;
        if (!File.Exists(settingsPath))
        {
            return new LiveSettings();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<LiveSettings>(json, Options) ?? new LiveSettings();
        }
        catch (Exception ex)
        {
            wasInvalid = true;
            BackupInvalidSettingsFile(settingsPath, ex);
            return new LiveSettings();
        }
    }

    private static void BackupInvalidSettingsFile(string settingsPath, Exception ex)
    {
        try
        {
            var backupPath = $"{settingsPath}.invalid_{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(settingsPath, backupPath, overwrite: false);
            Debug.WriteLine($"Live settings invalid JSON backed up: {settingsPath} -> {backupPath} | {ex}");
        }
        catch (Exception backupEx)
        {
            Debug.WriteLine($"Live settings invalid JSON backup failed: {settingsPath} | {ex} | backup={backupEx}");
        }
    }

    private static List<string> NormalizeTechnicianNames(IEnumerable<string>? names)
    {
        return (names ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
