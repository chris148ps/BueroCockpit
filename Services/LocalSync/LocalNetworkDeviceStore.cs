using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services.LocalSync;

public sealed record LocalNetworkDeviceRememberRequest(
    string DeviceId,
    string DeviceName,
    string Platform,
    string? AppVersion = null,
    DateTimeOffset? LastSeenUtc = null);

public sealed class LocalNetworkRememberedDevice
{
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string? AppVersion { get; set; }

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    public string? LastRemoteAddress { get; set; }

    public string Status { get; set; } = "remembered";
}

public sealed class LocalNetworkDeviceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();

    public IReadOnlyList<LocalNetworkRememberedDevice> Load()
    {
        lock (_gate)
        {
            return LoadUnsafe();
        }
    }

    public LocalNetworkRememberedDevice Remember(LocalNetworkDeviceRememberRequest request, string? remoteAddress)
    {
        lock (_gate)
        {
            var devices = LoadUnsafe().ToList();
            var now = DateTimeOffset.UtcNow;
            var lastSeen = request.LastSeenUtc ?? now;
            var deviceId = request.DeviceId.Trim();
            var existing = devices.FirstOrDefault(device =>
                string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                existing = new LocalNetworkRememberedDevice
                {
                    DeviceId = deviceId,
                    FirstSeenUtc = lastSeen
                };
                devices.Add(existing);
            }

            existing.DeviceName = request.DeviceName.Trim();
            existing.Platform = request.Platform.Trim();
            existing.AppVersion = string.IsNullOrWhiteSpace(request.AppVersion)
                ? null
                : request.AppVersion.Trim();
            existing.LastSeenUtc = lastSeen;
            existing.LastRemoteAddress = string.IsNullOrWhiteSpace(remoteAddress) ? null : remoteAddress;
            existing.Status = "remembered";

            SaveUnsafe(devices);
            return existing;
        }
    }

    private static List<LocalNetworkRememberedDevice> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(AppPaths.LocalNetworkDevicesPath))
            {
                return [];
            }

            var json = File.ReadAllText(AppPaths.LocalNetworkDevicesPath);
            return JsonSerializer.Deserialize<List<LocalNetworkRememberedDevice>>(json, Options) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Local network devices could not be loaded: {ex}");
            return [];
        }
    }

    private static void SaveUnsafe(List<LocalNetworkRememberedDevice> devices)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LocalConfigDirectory);
            var orderedDevices = devices
                .Where(device => !string.IsNullOrWhiteSpace(device.DeviceId))
                .OrderByDescending(device => device.LastSeenUtc)
                .ToList();
            File.WriteAllText(AppPaths.LocalNetworkDevicesPath, JsonSerializer.Serialize(orderedDevices, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Local network devices could not be saved: {ex}");
        }
    }
}
