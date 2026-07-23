using BueroCockpit.Data;

namespace BueroCockpit.Services;

/// <summary>
/// Bindet den produktiven Datenbestand an den lokalen Betriebssystem-Standardordner.
/// Frühere storage-location*.json-Dateien werden absichtlich weder gelesen noch verändert.
/// </summary>
public sealed class StorageLocationService
{
    public void ApplyConfiguredDataDirectory()
    {
        AppPaths.UseDefaultAppDataDirectory();
        EnsureProductiveDirectoryIsLocal(AppPaths.DefaultAppDataDirectory);
        Directory.CreateDirectory(AppPaths.DefaultAppDataDirectory);
        Directory.CreateDirectory(AppPaths.LocalConfigDirectory);
    }

    public static void EnsureProductiveDirectoryIsLocal(string directory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(directory));
        while (current is not null)
        {
            if (current.Exists && !string.IsNullOrWhiteSpace(current.LinkTarget))
            {
                var target = current.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? current.LinkTarget;
                throw new LocalDataDirectoryRedirectException(
                    $"Der lokale BüroCockpit-Datenordner ist als Verknüpfung umgeleitet und wird aus Sicherheitsgründen nicht geöffnet.\n\n" +
                    $"Lokaler Sollpfad: {Path.GetFullPath(directory)}\n" +
                    $"Erkanntes Ziel: {target}\n\n" +
                    "Bitte die Verknüpfung manuell entfernen und am Sollpfad einen echten lokalen Ordner anlegen. " +
                    "Der bisherige OneDrive-Ordner wird von BüroCockpit weder verändert noch automatisch übernommen.");
            }

            current = current.Parent;
        }
    }
}

public sealed class LocalDataDirectoryRedirectException : InvalidOperationException
{
    public LocalDataDirectoryRedirectException(string message)
        : base(message)
    {
    }
}
