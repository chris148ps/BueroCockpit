using BueroCockpit.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncService : ILocalSyncContracts
{
    private const string ListenerHost = "*";
    private const long MaximumRequestBodyBytes = 310L * 1024 * 1024;
    private const string DeviceIdHeader = "X-BueroCockpit-Device-Id";
    private const string TrustKeyHeader = "X-BueroCockpit-Trust-Key";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly object _gate = new();
    private readonly LocalSyncOptions _options;
    private readonly LocalNetworkDeviceStore _deviceStore;
    private readonly LocalSyncInboxStore? _inboxStore;
    private readonly Func<CancellationToken, Task<LocalSyncSnapshotPackage>>? _snapshotProvider;
    private readonly Func<LocalSyncUploadCompletedEventArgs, Task<MobileInboxTransferResult>>? _uploadProcessor;
    private readonly LocalSyncDeltaStore _deltaStore;
    private LocalSyncState _state;
    private string? _lastMessage;
    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private LocalBonjourService? _bonjourService;
    private string _changeVersion = $"placeholder-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    private DateTimeOffset _lastChangedUtc = DateTimeOffset.UtcNow;

    public event EventHandler<LocalNetworkRememberedDevice>? DeviceRemembered;
    public event EventHandler<LocalSyncUploadCompletedEventArgs>? UploadCompleted;

    public LocalSyncService(
        LocalSyncOptions options,
        LocalNetworkDeviceStore? deviceStore = null,
        Func<CancellationToken, Task<LocalSyncSnapshotPackage>>? snapshotProvider = null,
        LocalSyncDeltaStore? deltaStore = null,
        Func<LocalSyncUploadCompletedEventArgs, Task<MobileInboxTransferResult>>? uploadProcessor = null)
    {
        _options = options;
        _deviceStore = deviceStore ?? new LocalNetworkDeviceStore();
        _snapshotProvider = snapshotProvider;
        _uploadProcessor = uploadProcessor;
        _deltaStore = deltaStore ?? new LocalSyncDeltaStore();
        _inboxStore = string.IsNullOrWhiteSpace(options.DataFolderPath)
            ? null
            : new LocalSyncInboxStore(options.DataFolderPath);
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
                _lastMessage = $"Sync-Dienst laeuft bereits im lokalen Netzwerk auf Port {_options.Port}.";
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
                _lastMessage = "Sync-Dienst kann nicht starten: Port nicht festgelegt oder ungueltig.";
                return Task.FromResult(BuildStatusLocked());
            }

            _state = LocalSyncState.Starting;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{ListenerHost}:{_options.Port}/");

            try
            {
                listener.Start();
            }
            catch (Exception ex) when (ex is HttpListenerException or InvalidOperationException)
            {
                listener.Close();
                _state = LocalSyncState.Error;
                _lastMessage = $"Sync-Dienst konnte Port {_options.Port} nicht oeffnen: {ex.Message}";
                return Task.FromResult(BuildStatusLocked());
            }

            _listenerCts = new CancellationTokenSource();
            _listener = listener;
            _listenerTask = Task.Run(() => RunStatusListenerAsync(listener, _listenerCts.Token), CancellationToken.None);
            var bonjourMessage = _options.AnnounceBonjour
                ? LocalBonjourService.GetAvailabilityStatus().DisplayText
                : "Bonjour-Ankuendigung fuer diesen Lauf deaktiviert.";
            var bonjourStarted = false;
            try
            {
                if (_options.AnnounceBonjour)
                {
                    _bonjourService = new LocalBonjourService();
                    bonjourStarted = _bonjourService.Start(_options);
                    if (!bonjourStarted && !string.IsNullOrWhiteSpace(_bonjourService.LastError))
                    {
                        bonjourMessage = _bonjourService.LastError;
                    }
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException
                                       or EntryPointNotFoundException
                                       or BadImageFormatException
                                       or System.Runtime.InteropServices.ExternalException
                                       or System.Runtime.InteropServices.MarshalDirectiveException
                                       or System.Runtime.InteropServices.SEHException
                                       or InvalidOperationException)
            {
                _bonjourService = null;
            }
            catch
            {
                _bonjourService = null;
            }

            _state = LocalSyncState.Running;
            _lastMessage = bonjourStarted
                ? $"Sync-Dienst laeuft im lokalen Netzwerk auf Port {_options.Port}; Bonjour-Ankuendigung aktiv."
                : $"Sync-Dienst laeuft im lokalen Netzwerk auf Port {_options.Port}; {bonjourMessage}";
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
                ? "Sync-Dienst gestoppt."
                : "Lokaler Netzwerk-Sync ist deaktiviert.";
            return Task.FromResult(BuildStatusLocked());
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

    public LocalSyncChangeStatus GetChangeStatus()
    {
        lock (_gate)
        {
            return BuildChangeStatusLocked();
        }
    }

    public void MarkLocalChange()
    {
        lock (_gate)
        {
            _lastChangedUtc = DateTimeOffset.UtcNow;
            _changeVersion = $"placeholder-{_lastChangedUtc:yyyyMMddHHmmssfff}";
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
            messages.Add("Manifest formal gültig; Datenannahme erfolgt ausschließlich über den authentisierten Mobile-Inbox-Endpunkt.");
        }

        return new MobileInboxUploadResult(
            Accepted: false,
            UploadId: manifest.UploadId,
            InboxEntryId: null,
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            Messages: messages);
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

            _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        if (string.Equals(path, "/local-sync/devices/remember", StringComparison.OrdinalIgnoreCase))
        {
            await WriteRememberDeviceResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/local-sync/pairing/status", StringComparison.OrdinalIgnoreCase))
        {
            await WritePairingStatusResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/local-sync/mobile-inbox", StringComparison.OrdinalIgnoreCase))
        {
            await WriteMobileInboxResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/local-sync/snapshot", StringComparison.OrdinalIgnoreCase))
        {
            await WriteSnapshotResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/local-sync/changes", StringComparison.OrdinalIgnoreCase))
        {
            await WriteDeltaResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(path, "/local-sync/ack", StringComparison.OrdinalIgnoreCase))
        {
            await WriteAckResponseAsync(context).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
            || (!string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "/local-sync/status", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "/local-sync/changes/status", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "/local-sync/state", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "/pairing/status", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = 404;
            await WriteJsonAsync(context.Response, new { error = "not-found" }).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = 200;
        if (string.Equals(path, "/local-sync/changes/status", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/local-sync/state", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, GetChangeStatus()).ConfigureAwait(false);
            return;
        }

        await WriteJsonAsync(
            context.Response,
            new
            {
                app = _options.AppName,
                status = "ok",
                mode = "local-network-test",
                version = string.IsNullOrWhiteSpace(_options.AppVersion) ? "unknown" : _options.AppVersion,
                serverName = GetServerName(),
                deviceId = _options.DeviceId,
                manualSyncAvailable = _inboxStore is not null,
                snapshotDownloadAvailable = _snapshotProvider is not null,
                snapshotSchemaVersion = "local-sync-snapshot-v1"
            }).ConfigureAwait(false);
    }

    private LocalSyncChangeStatus BuildChangeStatusLocked()
    {
        return new LocalSyncChangeStatus(
            App: _options.AppName,
            Status: "ok",
            Mode: "local-network-test",
            ChangeVersion: _changeVersion,
            LastChangedUtc: _lastChangedUtc,
            SyncActive: false);
    }

    private async Task WriteRememberDeviceResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        LocalNetworkDeviceRememberRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<LocalNetworkDeviceRememberRequest>(
                context.Request.InputStream,
                JsonOptions).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "invalid-json" }).ConfigureAwait(false);
            return;
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.DeviceId)
            || string.IsNullOrWhiteSpace(request.DeviceName)
            || string.IsNullOrWhiteSpace(request.Platform))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "invalid-device" }).ConfigureAwait(false);
            return;
        }

        LocalNetworkRememberedDevice rememberedDevice;
        try
        {
            rememberedDevice = _deviceStore.Remember(request, context.Request.RemoteEndPoint?.Address.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { error = "device-store-failed", message = ex.Message }).ConfigureAwait(false);
            return;
        }
        DeviceRemembered?.Invoke(this, rememberedDevice);

        context.Response.StatusCode = 200;
        await WriteJsonAsync(
            context.Response,
            new
            {
                app = _options.AppName,
                status = "ok",
                mode = "local-network-test",
                pairingStatus = rememberedDevice.Status,
                message = string.Equals(rememberedDevice.Status, "trusted", StringComparison.OrdinalIgnoreCase)
                    ? "Gerät ist freigegeben"
                    : "Gerät vorgemerkt; Freigabe am Desktop erforderlich"
            }).ConfigureAwait(false);
    }

    private async Task WritePairingStatusResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        var state = GetPairingState(context.Request, out var device);
        var status = state switch
        {
            LocalNetworkPairingState.Trusted => "trusted",
            LocalNetworkPairingState.Pending => "pending",
            LocalNetworkPairingState.Revoked => "revoked",
            LocalNetworkPairingState.Invalid => "invalid",
            _ => "missing"
        };
        context.Response.StatusCode = state == LocalNetworkPairingState.Trusted ? 200 : 403;
        await WriteJsonAsync(
            context.Response,
            new LocalSyncPairingStatus(
                _options.AppName,
                status,
                context.Request.Headers[DeviceIdHeader]?.Trim() ?? string.Empty,
                device?.DeviceName,
                state switch
                {
                    LocalNetworkPairingState.Trusted => "Kopplung gültig.",
                    LocalNetworkPairingState.Pending => "Freigabe am Desktop steht noch aus.",
                    LocalNetworkPairingState.Revoked => "Kopplung wurde am Desktop widerrufen.",
                    LocalNetworkPairingState.Invalid => "Kopplungsnachweis ist ungültig.",
                    _ => "Gerät ist am Desktop nicht vorgemerkt."
                })).ConfigureAwait(false);
    }

    private async Task WriteMobileInboxResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        var pairingState = GetPairingState(context.Request, out var device);
        if (pairingState != LocalNetworkPairingState.Trusted || device is null)
        {
            context.Response.StatusCode = 403;
            await WriteJsonAsync(context.Response, new
            {
                error = "pairing-required",
                pairingStatus = pairingState.ToString().ToLowerInvariant(),
                message = "Upload ist nur für ein am Desktop freigegebenes Gerät erlaubt."
            }).ConfigureAwait(false);
            return;
        }

        if (_inboxStore is null || string.IsNullOrWhiteSpace(_options.DataFolderPath) || !Directory.Exists(_options.DataFolderPath))
        {
            context.Response.StatusCode = 503;
            await WriteJsonAsync(context.Response, new { error = "data-folder-unavailable" }).ConfigureAwait(false);
            return;
        }

        if (context.Request.ContentLength64 == 0 || context.Request.ContentLength64 > MaximumRequestBodyBytes)
        {
            context.Response.StatusCode = 413;
            await WriteJsonAsync(context.Response, new { error = "package-too-large" }).ConfigureAwait(false);
            return;
        }

        MobileInboxUploadRequest? request;
        try
        {
            var requestBody = await ReadBoundedRequestBodyAsync(
                context.Request.InputStream,
                MaximumRequestBodyBytes).ConfigureAwait(false);
            request = JsonSerializer.Deserialize<MobileInboxUploadRequest>(requestBody, JsonOptions);
        }
        catch (InvalidDataException)
        {
            context.Response.StatusCode = 413;
            await WriteJsonAsync(context.Response, new { error = "package-too-large" }).ConfigureAwait(false);
            return;
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "invalid-json" }).ConfigureAwait(false);
            return;
        }

        if (request is null || !string.Equals(request.DeviceId?.Trim(), device.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "device-id-mismatch" }).ConfigureAwait(false);
            return;
        }

        MobileInboxTransferResult result;
        try
        {
            result = _inboxStore.Accept(request);
            if (result.IsSuccess && _uploadProcessor is not null)
            {
                result = await _uploadProcessor(
                    new LocalSyncUploadCompletedEventArgs(device.DeviceId, device.DeviceName, result)).ConfigureAwait(false);
            }

            if (result.IsSuccess)
            {
                _deltaStore.RecordClientSequence(device.DeviceId, request.ClientSequence);
                _deviceStore.RecordSync(device.DeviceId, result);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { error = "upload-failed", message = ex.Message }).ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { error = "desktop-apply-failed", message = ex.Message }).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = result.Status switch
        {
            "accepted" or "skipped" => 200,
            "conflict" => 409,
            "invalid" => 400,
            _ => 500
        };
        UploadCompleted?.Invoke(this, new LocalSyncUploadCompletedEventArgs(device.DeviceId, device.DeviceName, result));
        await WriteJsonAsync(context.Response, result).ConfigureAwait(false);
    }

    private async Task WriteSnapshotResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        var pairingState = GetPairingState(context.Request, out var device);
        if (pairingState != LocalNetworkPairingState.Trusted || device is null)
        {
            context.Response.StatusCode = 403;
            await WriteJsonAsync(context.Response, new
            {
                error = "pairing-required",
                pairingStatus = pairingState.ToString().ToLowerInvariant(),
                message = "Desktopdaten sind nur für ein am Desktop freigegebenes Gerät abrufbar."
            }).ConfigureAwait(false);
            return;
        }

        if (_snapshotProvider is null)
        {
            context.Response.StatusCode = 503;
            await WriteJsonAsync(context.Response, new { error = "snapshot-unavailable" }).ConfigureAwait(false);
            return;
        }

        LocalSyncSnapshotPackage package;
        try
        {
            package = await _snapshotProvider(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { error = "snapshot-failed", message = ex.Message }).ConfigureAwait(false);
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(package.FilePath) || !File.Exists(package.FilePath))
            {
                context.Response.StatusCode = 500;
                await WriteJsonAsync(context.Response, new { error = "snapshot-file-missing" }).ConfigureAwait(false);
                return;
            }

            var checkpoint = _deltaStore.PrepareFullSync(device.DeviceId, package);
            await using var stream = new FileStream(
                package.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/vnd.buerocockpit.snapshot+zip";
            context.Response.ContentLength64 = stream.Length;
            context.Response.Headers["X-BueroCockpit-Snapshot-Schema"] = "local-sync-snapshot-v1";
            context.Response.Headers["X-BueroCockpit-Change-Version"] = checkpoint.ServerRevision;
            context.Response.Headers["X-BueroCockpit-Server-Sequence"] = checkpoint.ServerSequence.ToString();
            context.Response.Headers["X-BueroCockpit-Ack-Token"] = checkpoint.AckToken;
            context.Response.Headers["X-BueroCockpit-Created-At"] = package.CreatedAtUtc.ToString("O");
            context.Response.AddHeader(
                "Content-Disposition",
                $"attachment; filename=\"{Path.GetFileName(package.FileName)}\"");
            await stream.CopyToAsync(context.Response.OutputStream).ConfigureAwait(false);
            try
            {
                _deviceStore.RecordSnapshotDownload(device.DeviceId, package.CreatedAtUtc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Der vollständig übertragene Snapshot bleibt gültig; nur die lokale Statusnotiz fehlt.
            }
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException)
        {
            try
            {
                context.Response.Abort();
            }
            catch
            {
                // Die Gegenstelle hat die Verbindung bereits vollständig geschlossen.
            }
        }
        finally
        {
            try
            {
                context.Response.OutputStream.Close();
            }
            catch
            {
                // Die Gegenstelle kann die Verbindung während des Downloads schließen.
            }

            if (!string.IsNullOrWhiteSpace(package.CleanupDirectoryPath))
            {
                try
                {
                    Directory.Delete(package.CleanupDirectoryPath, recursive: true);
                }
                catch
                {
                    // Temporäre Exportdaten werden beim nächsten Betriebssystem-Cleanup entfernt.
                }
            }
        }
    }

    private async Task WriteDeltaResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        var pairingState = GetPairingState(context.Request, out var device);
        if (pairingState != LocalNetworkPairingState.Trusted || device is null)
        {
            context.Response.StatusCode = 403;
            await WriteJsonAsync(context.Response, new { error = "pairing-required" }).ConfigureAwait(false);
            return;
        }

        if (_snapshotProvider is null)
        {
            context.Response.StatusCode = 503;
            await WriteJsonAsync(context.Response, new { error = "delta-unavailable" }).ConfigureAwait(false);
            return;
        }

        LocalSyncSnapshotPackage? package = null;
        try
        {
            package = await _snapshotProvider(CancellationToken.None).ConfigureAwait(false);
            var response = _deltaStore.BuildDelta(
                device.DeviceId,
                context.Request.QueryString["since"],
                package);
            context.Response.StatusCode = 200;
            await WriteJsonAsync(context.Response, response).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context.Response, new { error = "delta-failed", message = ex.Message }).ConfigureAwait(false);
        }
        finally
        {
            TryCleanupSnapshotPackage(package);
        }
    }

    private async Task WriteAckResponseAsync(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            await WriteJsonAsync(context.Response, new { error = "method-not-allowed" }).ConfigureAwait(false);
            return;
        }

        var pairingState = GetPairingState(context.Request, out var device);
        if (pairingState != LocalNetworkPairingState.Trusted || device is null)
        {
            context.Response.StatusCode = 403;
            await WriteJsonAsync(context.Response, new { error = "pairing-required" }).ConfigureAwait(false);
            return;
        }

        LocalSyncAckRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<LocalSyncAckRequest>(
                context.Request.InputStream,
                JsonOptions).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "invalid-json" }).ConfigureAwait(false);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AckToken) || string.IsNullOrWhiteSpace(request.ServerRevision))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "invalid-ack" }).ConfigureAwait(false);
            return;
        }

        var response = _deltaStore.Confirm(device.DeviceId, request);
        context.Response.StatusCode = response.Status == "confirmed" ? 200 : 409;
        if (response.Status == "confirmed")
        {
            _deviceStore.RecordCheckpoint(device.DeviceId, response);
        }
        await WriteJsonAsync(context.Response, response).ConfigureAwait(false);
    }

    private static void TryCleanupSnapshotPackage(LocalSyncSnapshotPackage? package)
    {
        if (string.IsNullOrWhiteSpace(package?.CleanupDirectoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(package.CleanupDirectoryPath, recursive: true);
        }
        catch
        {
            // Temporäre Exportdaten werden beim nächsten Betriebssystem-Cleanup entfernt.
        }
    }

    private LocalNetworkPairingState GetPairingState(HttpListenerRequest request, out LocalNetworkRememberedDevice? device)
    {
        return _deviceStore.GetPairingState(
            request.Headers[DeviceIdHeader],
            request.Headers[TrustKeyHeader],
            out device);
    }

    private static async Task<byte[]> ReadBoundedRequestBodyAsync(Stream input, long maximumBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long totalBytes = 0;
        while (true)
        {
            var bytesRead = await input.ReadAsync(chunk).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                throw new InvalidDataException("Request body exceeds the configured size limit.");
            }
            buffer.Write(chunk, 0, bytesRead);
        }

        if (totalBytes == 0)
        {
            throw new JsonException("Request body is empty.");
        }
        return buffer.ToArray();
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object value)
    {
        response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(value, JsonOptions);
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
        _bonjourService?.Stop();
        _bonjourService = null;
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
