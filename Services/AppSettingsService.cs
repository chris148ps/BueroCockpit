using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class AppSettings
{
    // Lokal/geraetespezifisch: zeigt auf den gemeinsamen BueroCockpit_Daten-Ordner auf diesem Rechner.
    public string OneDriveEditDirectory { get; set; } = string.Empty;

    // Legacy/Uebergang: frei waehlbare live.bclive-Zieldatei. Aktive Hauptquelle ist BueroCockpit_Daten/Sync.
    public string IpadLiveFileTargetPath { get; set; } = string.Empty;

    // Lokal/geraetespezifisch: reine UI-Darstellung.
    public string AppearanceMode { get; set; } = "Dark Mode";

    // Leer bedeutet: Standard-Updatekanal aus UpdateService verwenden.
    // Nur fuer lokale Tests oder Sonderkanaele setzen.
    public string UpdateFeedUrl { get; set; } = string.Empty;

    // Vorbereitung fuer spaeteren manuellen lokalen Netzwerk-Sync. Bleibt standardmaessig aus.
    public bool LocalNetworkSyncEnabled { get; set; }

    // Lokal/geraetespezifisch. 0 bedeutet: noch nicht gesetzt, kein Port reserviert.
    public int LocalNetworkSyncPort { get; set; }

    // Lokal/geraetespezifischer Anzeigename fuer spaetere Kopplung.
    public string LocalNetworkSyncDeviceName { get; set; } = string.Empty;

    // Lokal/geraetespezifische Kennung fuer spaeteres Pairing. Darf nicht zentral synchronisiert werden.
    public string LocalNetworkSyncDeviceId { get; set; } = string.Empty;

    // Lokal/geraetespezifischer Einmal-Code fuer die spaetere Erstkopplung.
    public string LocalNetworkSyncPairingCode { get; set; } = string.Empty;

    // Lokal vorbereitete Liste fuer spaeter gekoppelte Geraete.
    public List<LocalNetworkSyncPairedDevice> LocalNetworkSyncPairedDevices { get; set; } = [];

    // Legacy/Fallback: Techniker/Monteure werden zentral in Sync/live/settings.json gespeichert.
    // Dieser lokale Wert wird nur noch zum einmaligen Befuellen leerer Live-Settings gelesen.
    public List<string> TechnicianNames { get; set; } = [];
}

public sealed class LocalNetworkSyncPairedDevice
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DevicePlatform { get; set; } = string.Empty;

    public DateTimeOffset PairedAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public string TrustKey { get; set; } = string.Empty;

    public string SharedSecret { get; set; } = string.Empty;
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string CreateLocalNetworkSyncDeviceId()
    {
        return $"desktop-{Guid.NewGuid():N}";
    }

    public static string CreateLocalNetworkSyncPairingCode()
    {
        Span<char> letters = stackalloc char[4];
        for (var index = 0; index < letters.Length; index++)
        {
            letters[index] = (char)('A' + RandomNumberGenerator.GetInt32(0, 26));
        }

        var digits = RandomNumberGenerator.GetInt32(1000, 10000).ToString("D4", CultureInfo.InvariantCulture);
        return $"{letters[0]}{letters[1]}{letters[2]}{letters[3]}-{digits}";
    }

    public static bool IsLocalNetworkSyncPairingCodeFormat(string? pairingCode)
    {
        if (string.IsNullOrWhiteSpace(pairingCode))
        {
            return false;
        }

        var value = pairingCode.Trim();
        if (value.Length != 9 || value[4] != '-')
        {
            return false;
        }

        for (var index = 0; index < 4; index++)
        {
            if (value[index] < 'A' || value[index] > 'Z')
            {
                return false;
            }
        }

        for (var index = 5; index < value.Length; index++)
        {
            if (value[index] < '0' || value[index] > '9')
            {
                return false;
            }
        }

        return true;
    }

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
