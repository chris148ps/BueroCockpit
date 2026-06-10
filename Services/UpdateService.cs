using System.Diagnostics;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace BueroCockpit.Services;

public sealed class UpdateService
{
    private const string UpdateSourceText = "GitHub Releases";
    private const string GitHubRepositoryUrl = "https://github.com/chris148ps/BueroCockpit";
    private UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;
    private string _statusText = "Auto-Update ist vorbereitet, aber noch nicht mit einem Release-Kanal verbunden.";

    public string UpdateSource => UpdateSourceText;
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
            if (!_updateManager.IsInstalled)
            {
                _pendingUpdate = null;
                _statusText = "Auto-Update wird mit einem Velopack-Release aktiviert.";
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

    private static UpdateManager CreateUpdateManager()
    {
        var source = new GithubSource(GitHubRepositoryUrl, accessToken: null, prerelease: false);
        return new UpdateManager(source);
    }
}
