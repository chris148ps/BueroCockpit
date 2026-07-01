using BueroCockpit.Services;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncOptions
{
    public bool Enabled { get; init; }

    public int Port { get; init; }

    public string DeviceName { get; init; } = string.Empty;

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
            DataFolderPath = dataFolderPath
        };
    }
}
