using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class AppSettings
{
    public string OneDriveEditDirectory { get; set; } = string.Empty;
    public string IpadLiveFileTargetPath { get; set; } = string.Empty;
    public string AppearanceMode { get; set; } = "Dark Mode";
    // Leer bedeutet: Standard-Updatekanal aus UpdateService verwenden.
    // Nur fuer lokale Tests oder Sonderkanaele setzen.
    public string UpdateFeedUrl { get; set; } = string.Empty;
    public List<string> TechnicianNames { get; set; } = [];
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            var settingsPath = File.Exists(AppPaths.LocalSettingsPath)
                ? AppPaths.LocalSettingsPath
                : AppPaths.SettingsPath;
            if (!File.Exists(settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings could not be loaded: {ex}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LocalConfigDirectory);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(AppPaths.LocalSettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings could not be saved: {ex}");
        }
    }
}
