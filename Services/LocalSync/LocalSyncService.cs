using System.Security.Cryptography;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncService : ILocalSyncContracts
{
    private readonly object _gate = new();
    private readonly LocalSyncOptions _options;
    private LocalSyncState _state;
    private string? _lastMessage;
    private string? _pairingCode;
    private DateTimeOffset? _pairingCodeCreatedAtUtc;

    public LocalSyncService(LocalSyncOptions options)
    {
        _options = options;
        _state = options.Enabled ? LocalSyncState.Stopped : LocalSyncState.Disabled;
        _lastMessage = options.Enabled
            ? "Lokaler Netzwerk-Sync ist vorbereitet, aber nicht gestartet."
            : "Lokaler Netzwerk-Sync ist deaktiviert.";
    }

    public LocalSyncStatus GetStatus()
    {
        lock (_gate)
        {
            return BuildStatusLocked();
        }
    }

    public Task<LocalSyncStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_options.Enabled)
            {
                _state = LocalSyncState.Disabled;
                _lastMessage = "Lokaler Netzwerk-Sync ist deaktiviert. Es wird kein Server gestartet.";
                return Task.FromResult(BuildStatusLocked());
            }

            _state = LocalSyncState.Starting;
            _state = LocalSyncState.Error;
            _lastMessage = "Dienst-Geruest vorbereitet; Serverstart ist noch nicht implementiert.";
            return Task.FromResult(BuildStatusLocked());
        }
    }

    public Task<LocalSyncStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _state = _options.Enabled ? LocalSyncState.Stopped : LocalSyncState.Disabled;
            _lastMessage = _options.Enabled
                ? "Lokaler Netzwerk-Sync ist gestoppt. Es laeuft kein Server."
                : "Lokaler Netzwerk-Sync ist deaktiviert.";
            return Task.FromResult(BuildStatusLocked());
        }
    }

    public string CreatePairingCode()
    {
        lock (_gate)
        {
            var value = RandomNumberGenerator.GetInt32(100000, 1000000);
            _pairingCode = value.ToString("D6");
            _pairingCodeCreatedAtUtc = DateTimeOffset.UtcNow;
            _lastMessage = "Pairing-Code wurde nur lokal im Speicher vorbereitet.";
            return _pairingCode;
        }
    }

    public void ClearPairingCode()
    {
        lock (_gate)
        {
            _pairingCode = null;
            _pairingCodeCreatedAtUtc = null;
            _lastMessage = "Pairing-Code geloescht.";
        }
    }

    public bool ValidatePairingCode(string? pairingCode)
    {
        lock (_gate)
        {
            return !string.IsNullOrWhiteSpace(pairingCode)
                   && !string.IsNullOrWhiteSpace(_pairingCode)
                   && string.Equals(pairingCode.Trim(), _pairingCode, StringComparison.Ordinal);
        }
    }

    public LocalSyncSnapshotManifest BuildSnapshotManifest()
    {
        lock (_gate)
        {
            return new LocalSyncSnapshotManifest(
                SchemaVersion: "local-sync-phase2-placeholder",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                ServerName: GetServerName(),
                State: _state.ToString(),
                ContainsProductiveData: false,
                Entries: Array.Empty<LocalSyncSnapshotManifestEntry>(),
                Message: "Platzhaltermanifest: Es werden keine Produktivdaten gelesen oder ausgegeben.");
        }
    }

    public MobileInboxUploadResult ValidateMobileInboxManifest(MobileInboxUploadManifest manifest)
    {
        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(manifest.UploadId))
        {
            messages.Add("UploadId fehlt.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DeviceId))
        {
            messages.Add("DeviceId fehlt.");
        }

        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            messages.Add("SchemaVersion fehlt.");
        }

        foreach (var file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.RelativePath))
            {
                messages.Add("Dateieintrag ohne relativen Pfad.");
                continue;
            }

            if (Path.IsPathRooted(file.RelativePath) || file.RelativePath.Contains("..", StringComparison.Ordinal))
            {
                messages.Add($"Unsicherer relativer Pfad: {file.RelativePath}");
            }

            if (file.SizeBytes < 0)
            {
                messages.Add($"Ungueltige Dateigroesse: {file.RelativePath}");
            }
        }

        if (messages.Count == 0)
        {
            messages.Add("Manifest formal vorbereitet, aber Netzwerk-Upload ist noch nicht implementiert.");
        }

        return new MobileInboxUploadResult(
            Accepted: false,
            UploadId: manifest.UploadId,
            InboxEntryId: null,
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            Messages: messages);
    }

    public PairingConfirmation ConfirmPairing(PairingRequest request, string pairingCode)
    {
        if (!ValidatePairingCode(pairingCode))
        {
            throw new InvalidOperationException("Pairing-Code ist ungueltig oder nicht vorbereitet.");
        }

        return new PairingConfirmation(
            PairingCode: pairingCode,
            DeviceId: string.Empty,
            DeviceName: request.DeviceName,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);
    }

    public MobileInboxUploadResult ValidateMobileInboxUpload(MobileInboxUploadManifest manifest)
    {
        return ValidateMobileInboxManifest(manifest);
    }

    private LocalSyncStatus BuildStatusLocked()
    {
        return new LocalSyncStatus(
            ServerName: GetServerName(),
            AppName: _options.AppName,
            AppVersion: _options.AppVersion,
            DataFolderAvailable: !string.IsNullOrWhiteSpace(_options.DataFolderPath) && Directory.Exists(_options.DataFolderPath),
            DataFolderDisplayName: GetDataFolderDisplayName(),
            PairingRequired: true,
            PairedDeviceCount: _options.PairedDevices.Count,
            ServerTimeUtc: DateTimeOffset.UtcNow,
            Message: $"{_state}: {_lastMessage}");
    }

    private string GetServerName()
    {
        return string.IsNullOrWhiteSpace(_options.DeviceName)
            ? Environment.MachineName
            : _options.DeviceName.Trim();
    }

    private string? GetDataFolderDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_options.DataFolderPath))
        {
            return null;
        }

        return Path.GetFileName(_options.DataFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
