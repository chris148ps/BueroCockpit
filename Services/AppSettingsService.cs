using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class AppSettings
{
    // Lokal/geraetespezifisch: zeigt auf den gemeinsamen BueroCockpit_Daten-Ordner auf diesem Rechner.
    public string OneDriveEditDirectory { get; set; } = string.Empty;

    // Legacy/Toleranz: alter frei waehlbarer iPad-Dateizielpfad. Im aktuellen Bedienweg ignoriert.
    public string IpadLiveFileTargetPath { get; set; } = string.Empty;

    // Lokal/geraetespezifisch: reine UI-Darstellung.
    public string AppearanceMode { get; set; } = "Dark Mode";

    // Lokal/geraetespezifisch: zeigt den optionalen Schreibtisch in der Navigation an.
    // Der Initialwert hält den Schreibtisch für bestehende Installationen sichtbar.
    public bool ShowDesktop { get; set; } = true;

    // Leer bedeutet: Standard-Updatekanal aus UpdateService verwenden.
    // Nur fuer lokale Tests oder Sonderkanaele setzen.
    public string UpdateFeedUrl { get; set; } = string.Empty;

    // Vorbereitung fuer spaeteren manuellen lokalen Netzwerk-Sync. Bleibt standardmaessig aus.
    public bool LocalNetworkSyncEnabled { get; set; }

    // Lokal/geraetespezifisch. 0 bedeutet: noch nicht gesetzt; die UI speichert dann den sicheren Default.
    public int LocalNetworkSyncPort { get; set; }

    // Lokal/geraetespezifischer Anzeigename fuer spaetere Kopplung.
    public string LocalNetworkSyncDeviceName { get; set; } = string.Empty;

    // Lokal/geraetespezifische Kennung fuer den lokalen Netzwerk-Testdienst. Darf nicht zentral synchronisiert werden.
    public string LocalNetworkSyncDeviceId { get; set; } = string.Empty;

    // Legacy/Toleranz: alter Einmal-Code aus frueherer Pairing-Code-Vorbereitung. Wird im aktuellen Bedienweg ignoriert.
    public string LocalNetworkSyncPairingCode { get; set; } = string.Empty;

    // Legacy/Toleranz: alte Liste aus frueherer Pairing-Code-Vorbereitung. Wird im aktuellen Bedienweg ignoriert.
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
    public const int DefaultLocalNetworkSyncPort = 53941;

    public static string CreateLocalNetworkSyncDeviceId()
    {
        return $"desktop-{Guid.NewGuid():N}";
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
            try
            {
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            catch (JsonException ex) when (TryDeserializeWithLenientLocalNetworkSyncPort(json, out var settings))
            {
                Debug.WriteLine($"Settings were loaded with normalized LocalNetworkSyncPort: {ex}");
                return settings;
            }
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

    private static bool TryDeserializeWithLenientLocalNetworkSyncPort(string json, out AppSettings settings)
    {
        settings = new AppSettings();

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
            {
                return false;
            }

            if (root.TryGetPropertyValue(nameof(AppSettings.LocalNetworkSyncPort), out var portNode)
                && TryNormalizeLocalNetworkSyncPort(portNode, out var port))
            {
                root[nameof(AppSettings.LocalNetworkSyncPort)] = port;
            }

            settings = root.Deserialize<AppSettings>(Options) ?? new AppSettings();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            Debug.WriteLine($"Settings could not be loaded with normalized LocalNetworkSyncPort: {ex}");
            return false;
        }
    }

    private static bool TryNormalizeLocalNetworkSyncPort(JsonNode? portNode, out int port)
    {
        port = 0;

        if (portNode is null)
        {
            return true;
        }

        if (portNode is not JsonValue portValue)
        {
            return true;
        }

        if (portValue.TryGetValue<int>(out var parsedNumber))
        {
            port = parsedNumber;
        }
        else if (portValue.TryGetValue<string>(out var parsedText))
        {
            var portText = parsedText?.Trim();
            if (string.IsNullOrWhiteSpace(portText))
            {
                return true;
            }

            if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port))
            {
                port = 0;
                return true;
            }
        }
        else
        {
            port = 0;
            return true;
        }

        if (port <= 0 || port < 1024 || port > 65535)
        {
            port = 0;
        }

        return true;
    }
}
