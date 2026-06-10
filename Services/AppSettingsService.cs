using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class AppSettings
{
    public string OneDriveEditDirectory { get; set; } = string.Empty;
    public string UpdateFeedUrl { get; set; } = string.Empty;
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsPath);
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
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(AppPaths.SettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings could not be saved: {ex}");
        }
    }
}
