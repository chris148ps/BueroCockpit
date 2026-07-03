using BueroCockpit.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncService : ILocalSyncContracts
{
    private readonly object _gate = new();
    private readonly LocalSyncOptions _options;
    private LocalSyncState _state;
    private string? _lastMessage;
    private string? _pairingCode;
    private DateTimeOffset? _pairingCodeCreatedAtUtc;
    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;

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
            if (_state == LocalSyncState.Running && _listener is not null)
            {
                _lastMessage = $"Testdienst laeuft bereits auf Port {_options.Port}.";
                return Task.FromResult(BuildStatusLocked());
            }

            if (!_options.Enabled)
            {
                _state = LocalSyncState.Disabled;
                _lastMessage = "Lokaler Netzwerk-Sync ist deaktiviert. Es wird kein Server gestartet.";
                return Task.FromResult(BuildStatusLocked());
            }

            if (_options.Port < 1024 || _options.Port > 65535)
            {
                _state = LocalSyncState.Error;
                _lastMessage = "Testdienst kann nicht starten: Port nicht festgelegt oder ungueltig.";
                return Task.FromResult(BuildStatusLocked());
            }

            _state = LocalSyncState.Starting;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException)
            {
                listener.Close();
                _state = LocalSyncState.Error;
                _lastMessage = $"Testdienst konnte Port {_options.Port} nicht oeffnen: {ex.Message}";
                return Task.FromResult(BuildStatusLocked());
            }

            _listenerCts = new CancellationTokenSource();
            _listener = listener;
            _listenerTask = Task.Run(() => RunStatusListenerAsync(listener, _listenerCts.Token), CancellationToken.None);
            _state = LocalSyncState.Running;
            _lastMessage = $"Testdienst laeuft auf Port {_options.Port}.";
            return Task.FromResult(BuildStatusLocked());
        }
    }

    public Task<LocalSyncStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            StopListenerLocked();
            _state = _options.Enabled ? LocalSyncState.Stopped : LocalSyncState.Disabled;
            _lastMessage = _options.Enabled
                ? "Testdienst gestoppt."
                : "Lokaler Netzwerk-Sync ist deaktiviert.";
            return Task.FromResult(BuildStatusLocked());
        }
    }

    public string CreatePairingCode()
    {
        lock (_gate)
        {
            _pairingCode = AppSettingsService.CreateLocalNetworkSyncPairingCode();
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
            DesktopDeviceId: _options.DeviceId,
            DesktopName: GetServerName(),
            PairedDeviceId: request.DeviceId,
            PairedDeviceName: request.DeviceName,
            PairedAtUtc: DateTimeOffset.UtcNow,
            TrustKey: string.Empty);
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

    private async Task RunStatusListenerAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            _ = Task.Run(() => WriteStatusResponseAsync(context), CancellationToken.None);
        }
    }

    private async Task WriteStatusResponseAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        if (!string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(path, "/pairing/status", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 404;
            await WriteJsonAsync(context.Response, new { error = "not-found" }).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = 200;
        await WriteJsonAsync(
            context.Response,
            new
            {
                app = _options.AppName,
                status = "ok",
                mode = "pairing-test",
                version = string.IsNullOrWhiteSpace(_options.AppVersion) ? "unknown" : _options.AppVersion
            }).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object value)
    {
        response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(value);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;

        try
        {
            await response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
        }
        finally
        {
            response.OutputStream.Close();
        }
    }

    private void StopListenerLocked()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;
        _listener?.Close();
        _listener = null;
        _listenerTask = null;
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
