using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace BueroCockpit.Services;

public sealed class UpdateService
{
    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;
    private string _updateFeedUrl = string.Empty;
    private string _statusText = "Noch kein Update-Kanal eingerichtet.";

    public string UpdateFeedUrl
    {
        get => _updateFeedUrl;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (_updateFeedUrl == normalized)
            {
                return;
            }

            _updateFeedUrl = normalized;
            _updateManager = null;
            _pendingUpdate = null;
        }
    }

    public string UpdateSource => string.IsNullOrWhiteSpace(UpdateFeedUrl) ? "Noch nicht eingerichtet" : UpdateFeedUrl;
    public bool HasPendingUpdate => _pendingUpdate is not null;

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var productVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(productVersion)
            ? assembly.GetName().Version?.ToString() ?? "unbekannt"
            : productVersion;
    }

    public bool IsVelopackAvailable()
    {
        try
        {
            _updateManager ??= CreateUpdateManager();
            if (_updateManager is null)
            {
                return false;
            }

            return _updateManager.IsInstalled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Velopack availability check failed: {ex}");
            return false;
        }
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            _updateManager ??= CreateUpdateManager();
            if (_updateManager is null)
            {
                _pendingUpdate = null;
                _statusText = "Noch kein Update-Kanal eingerichtet.";
                return false;
            }

            if (!_updateManager.IsInstalled)
            {
                _pendingUpdate = null;
                _statusText = "Auto-Update wird mit einer Velopack-Installation aktiviert.";
                return false;
            }

            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            _statusText = _pendingUpdate is null
                ? "Keine Updates gefunden."
                : $"Update {_pendingUpdate.TargetFullRelease.Version} gefunden.";
            return _pendingUpdate is not null;
        }
        catch (Exception ex)
        {
            _pendingUpdate = null;
            _statusText = "Updateprüfung konnte nicht ausgeführt werden.";
            Debug.WriteLine($"Update check failed: {ex}");
            return false;
        }
    }

    public async Task<bool> DownloadAndApplyUpdateAsync()
    {
        try
        {
            if (_updateManager is null || _pendingUpdate is null)
            {
                _statusText = "Kein Update zum Installieren vorhanden.";
                return false;
            }

            await _updateManager.DownloadUpdatesAsync(_pendingUpdate);
            _statusText = "Update wurde heruntergeladen und wird installiert.";
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
            return true;
        }
        catch (Exception ex)
        {
            _statusText = "Update konnte nicht installiert werden.";
            Debug.WriteLine($"Update install failed: {ex}");
            return false;
        }
    }

    public string GetUpdateStatusText()
    {
        return _statusText;
    }

    private UpdateManager? CreateUpdateManager()
    {
        if (string.IsNullOrWhiteSpace(UpdateFeedUrl))
        {
            return null;
        }

        IUpdateSource source = Directory.Exists(UpdateFeedUrl)
            ? new SimpleFileSource(new DirectoryInfo(UpdateFeedUrl))
            : new SimpleWebSource(UpdateFeedUrl);
        return new UpdateManager(source);
    }
}
