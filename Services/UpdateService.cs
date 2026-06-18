using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace BueroCockpit.Services;

public sealed class UpdateService
{
    private const string DefaultUpdateRepositoryUrl = "https://github.com/chris148ps/BueroCockpit-Updates";

    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;
    private string _updateFeedUrl = string.Empty;
    private string _statusText = "Update-Kanal: GitHub Releases.";
    private bool _lastCheckFailed;

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

    public string EffectiveUpdateFeedUrl => UpdateFeedUrl?.Trim() ?? string.Empty;

    public string UpdateSource => string.IsNullOrWhiteSpace(UpdateFeedUrl)
        ? $"Standard: GitHub Releases ({DefaultUpdateRepositoryUrl})"
        : UpdateFeedUrl;
    public bool HasPendingUpdate => _pendingUpdate is not null;

    public bool LastCheckFailed => _lastCheckFailed;

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
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

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
        _lastCheckFailed = false;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _pendingUpdate = null;
                _statusText = "Auto-Update wird auf dieser Plattform nicht unterstützt.";
                return false;
            }

            _updateManager ??= CreateUpdateManager();
            if (_updateManager is null)
            {
                _pendingUpdate = null;
                _statusText = "Update-Kanal konnte nicht eingerichtet werden.";
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
            _lastCheckFailed = true;
            _statusText = "Updateprüfung konnte nicht durchgeführt werden.";
            Debug.WriteLine($"Update check failed: {ex}");
            return false;
        }
    }

    public async Task<bool> DownloadAndApplyUpdateAsync()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _statusText = "Update-Installation wird auf dieser Plattform nicht unterstützt.";
                return false;
            }

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
        var feedUrl = EffectiveUpdateFeedUrl;

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return new UpdateManager(new GithubSource(DefaultUpdateRepositoryUrl, null, false, null));
        }

        IUpdateSource source = Directory.Exists(feedUrl)
            ? new SimpleFileSource(new DirectoryInfo(feedUrl))
            : new SimpleWebSource(feedUrl);
        return new UpdateManager(source);
    }
}
