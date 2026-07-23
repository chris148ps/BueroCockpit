using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services.LocalSync;

public sealed record LocalNetworkDeviceRememberRequest(
    string DeviceId,
    string DeviceName,
    string Platform,
    string? AppVersion = null,
    DateTimeOffset? LastSeenUtc = null,
    string? SharedSecret = null);

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

    public string TrustKeyHash { get; set; } = string.Empty;

    public DateTimeOffset? TrustedAtUtc { get; set; }

    public DateTimeOffset? LastSyncUtc { get; set; }

    public string? LastSyncMessage { get; set; }

    public string? ConfirmedServerRevision { get; set; }

    public long ConfirmedServerSequence { get; set; }

    public long ConfirmedClientSequence { get; set; }

    public string SyncApiVersion { get; set; } = string.Empty;
}

public enum LocalNetworkPairingState
{
    Missing,
    Invalid,
    Pending,
    Trusted,
    Revoked
}

public sealed class LocalNetworkDeviceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly string _devicesPath;

    public LocalNetworkDeviceStore(string? devicesPath = null)
    {
        _devicesPath = string.IsNullOrWhiteSpace(devicesPath)
            ? AppPaths.LocalNetworkDevicesPath
            : Path.GetFullPath(devicesPath);
    }

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

            if (!string.IsNullOrWhiteSpace(request.SharedSecret))
            {
                var trustKeyHash = HashSecret(request.SharedSecret);
                if (!string.IsNullOrWhiteSpace(existing.TrustKeyHash) &&
                    !FixedTimeEquals(existing.TrustKeyHash, trustKeyHash))
                {
                    existing.Status = "pending";
                    existing.TrustedAtUtc = null;
                }
                else if (string.Equals(existing.Status, "remembered", StringComparison.OrdinalIgnoreCase))
                {
                    existing.Status = "pending";
                }

                existing.TrustKeyHash = trustKeyHash;
            }
            else if (string.IsNullOrWhiteSpace(existing.Status))
            {
                existing.Status = "remembered";
            }

            SaveUnsafe(devices);
            return existing;
        }
    }

    public LocalNetworkPairingState GetPairingState(
        string? deviceId,
        string? sharedSecret,
        out LocalNetworkRememberedDevice? device)
    {
        lock (_gate)
        {
            device = LoadUnsafe().FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                return LocalNetworkPairingState.Missing;
            }

            if (string.IsNullOrWhiteSpace(sharedSecret) ||
                string.IsNullOrWhiteSpace(device.TrustKeyHash) ||
                !FixedTimeEquals(device.TrustKeyHash, HashSecret(sharedSecret)))
            {
                return LocalNetworkPairingState.Invalid;
            }

            return device.Status.Trim().ToLowerInvariant() switch
            {
                "trusted" => LocalNetworkPairingState.Trusted,
                "revoked" => LocalNetworkPairingState.Revoked,
                _ => LocalNetworkPairingState.Pending
            };
        }
    }

    public bool SetTrusted(string deviceId, bool trusted)
    {
        lock (_gate)
        {
            var devices = LoadUnsafe().ToList();
            var device = devices.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (device is null || (trusted && string.IsNullOrWhiteSpace(device.TrustKeyHash)))
            {
                return false;
            }

            device.Status = trusted ? "trusted" : "revoked";
            device.TrustedAtUtc = trusted ? DateTimeOffset.UtcNow : null;
            SaveUnsafe(devices);
            return true;
        }
    }

    public bool Delete(string deviceId)
    {
        lock (_gate)
        {
            var normalizedDeviceId = deviceId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDeviceId))
            {
                return false;
            }

            var devices = LoadUnsafe().ToList();
            var removedCount = devices.RemoveAll(item =>
                string.Equals(item.DeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase));
            if (removedCount == 0)
            {
                return false;
            }

            SaveUnsafe(devices);
            return true;
        }
    }

    public void RecordSync(string deviceId, MobileInboxTransferResult result)
    {
        lock (_gate)
        {
            var devices = LoadUnsafe().ToList();
            var device = devices.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                return;
            }

            device.LastSeenUtc = DateTimeOffset.UtcNow;
            device.LastSyncUtc = result.IsSuccess ? result.ReceivedAtUtc : device.LastSyncUtc;
            device.LastSyncMessage = result.Messages.FirstOrDefault();
            SaveUnsafe(devices);
        }
    }

    public void RecordSnapshotDownload(string deviceId, DateTimeOffset createdAtUtc)
    {
        lock (_gate)
        {
            var devices = LoadUnsafe().ToList();
            var device = devices.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                return;
            }

            device.LastSeenUtc = DateTimeOffset.UtcNow;
            device.LastSyncMessage =
                $"Desktopdaten wurden am {createdAtUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss} abgerufen und warten auf die Abschlussbestätigung.";
            SaveUnsafe(devices);
        }
    }

    public void RecordCheckpoint(string deviceId, LocalSyncAckResponse response)
    {
        lock (_gate)
        {
            var devices = LoadUnsafe().ToList();
            var device = devices.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                return;
            }

            device.LastSeenUtc = DateTimeOffset.UtcNow;
            device.LastSyncUtc = DateTimeOffset.UtcNow;
            device.LastSyncMessage = response.Message;
            device.ConfirmedServerRevision = response.ServerRevision;
            device.ConfirmedServerSequence = response.ServerSequence;
            device.ConfirmedClientSequence = response.LastConfirmedClientSequence;
            device.SyncApiVersion = response.ApiVersion;
            SaveUnsafe(devices);
        }
    }

    private List<LocalNetworkRememberedDevice> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_devicesPath))
            {
                return [];
            }

            var json = File.ReadAllText(_devicesPath);
            return JsonSerializer.Deserialize<List<LocalNetworkRememberedDevice>>(json, Options) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Local network devices could not be loaded: {ex}");
            return [];
        }
    }

    private void SaveUnsafe(List<LocalNetworkRememberedDevice> devices)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_devicesPath)!);
        var orderedDevices = devices
            .Where(device => !string.IsNullOrWhiteSpace(device.DeviceId))
            .OrderByDescending(device => device.LastSeenUtc)
            .ToList();
        var temporaryPath = $"{_devicesPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(orderedDevices, Options));
        File.Move(temporaryPath, _devicesPath, overwrite: true);
    }

    private static string HashSecret(string secret)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));
    }

    private static bool FixedTimeEquals(string first, string second)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(first),
                Convert.FromBase64String(second));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
