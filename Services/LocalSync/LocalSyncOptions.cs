using BueroCockpit.Services;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncOptions
{
    public bool Enabled { get; init; }

    public int Port { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    public string DeviceId { get; init; } = string.Empty;

    public string PairingCode { get; init; } = string.Empty;

    public IReadOnlyList<LocalNetworkSyncPairedDevice> PairedDevices { get; init; } = Array.Empty<LocalNetworkSyncPairedDevice>();

    public string AppName { get; init; } = "BueroCockpit";

    public string? AppVersion { get; init; }

    public string? DataFolderPath { get; init; }

    public static LocalSyncOptions FromSettings(AppSettings settings, string? dataFolderPath = null)
    {
        return new LocalSyncOptions
        {
            Enabled = settings.LocalNetworkSyncEnabled,
            Port = settings.LocalNetworkSyncPort,
            DeviceName = settings.LocalNetworkSyncDeviceName,
            DeviceId = settings.LocalNetworkSyncDeviceId,
            PairingCode = settings.LocalNetworkSyncPairingCode,
            PairedDevices = settings.LocalNetworkSyncPairedDevices,
            DataFolderPath = dataFolderPath
        };
    }
}
