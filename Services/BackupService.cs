using System.Diagnostics;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class BackupService
{
    private const int RetainedBackupCount = 20;
    private const string BackupFilePrefix = "buerocockpit-backup-";
    private const string BackupFileExtension = ".db";

    public BackupResult CreateBackup()
    {
        AppPaths.EnsureBaseDirectories();
        Directory.CreateDirectory(AppPaths.BackupDirectory);

        var backupPath = CreateUniqueBackupPath();
        var tempPath = Path.Combine(AppPaths.BackupDirectory, $".{Path.GetFileNameWithoutExtension(backupPath)}-{Guid.NewGuid():N}.tmp");

        try
        {
            CopyDatabaseFile(tempPath);
            File.Move(tempPath, backupPath);
            TrimOldBackups();
            return new BackupResult(backupPath, 0);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static string CreateUniqueBackupPath()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var attempt = 0;
        string backupPath;

        do
        {
            attempt++;
            var suffix = attempt == 1 ? string.Empty : $"_{attempt}";
            backupPath = Path.Combine(AppPaths.BackupDirectory, $"{BackupFilePrefix}{timestamp}{suffix}{BackupFileExtension}");
        }
        while (File.Exists(backupPath));

        return backupPath;
    }

    private static void CopyDatabaseFile(string targetPath)
    {
        if (!File.Exists(AppPaths.DatabasePath))
        {
            throw new FileNotFoundException("Die Datenbank konnte nicht gesichert werden, weil sie nicht gefunden wurde.", AppPaths.DatabasePath);
        }

        using var source = new FileStream(AppPaths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
        target.Flush(flushToDisk: true);
    }

    private static void TrimOldBackups()
    {
        try
        {
            var backupsToDelete = Directory.EnumerateFiles(AppPaths.BackupDirectory, $"{BackupFilePrefix}*{BackupFileExtension}")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(RetainedBackupCount)
                .ToList();

            foreach (var backupFile in backupsToDelete)
            {
                TryDeleteFile(backupFile.FullName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not trim old backups: {ex}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not delete temporary backup file '{path}': {ex}");
        }
    }
}

public sealed record BackupResult(string BackupPath, int SkippedFiles);
