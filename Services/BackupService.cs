using System.Diagnostics;
using System.IO.Compression;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class BackupService
{
    public BackupResult CreateBackup()
    {
        AppPaths.EnsureBaseDirectories();

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var backupPath = Path.Combine(AppPaths.BackupDirectory, $"buerocockpit_backup_{timestamp}.zip");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"BueroCockpitBackup_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempRoot);
            var skippedFiles = 0;

            if (File.Exists(AppPaths.DatabasePath))
            {
                var targetDb = Path.Combine(tempRoot, Path.GetFileName(AppPaths.DatabasePath));
                CopyFileBestEffort(AppPaths.DatabasePath, targetDb, ref skippedFiles);
            }

            if (Directory.Exists(AppPaths.TasksDirectory))
            {
                CopyDirectoryBestEffort(AppPaths.TasksDirectory, Path.Combine(tempRoot, "Tasks"), ref skippedFiles);
            }

            ZipFile.CreateFromDirectory(tempRoot, backupPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return new BackupResult(backupPath, skippedFiles);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void CopyDirectoryBestEffort(string sourceDirectory, string targetDirectory, ref int skippedFiles)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectoryBestEffort(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)), ref skippedFiles);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            CopyFileBestEffort(file, Path.Combine(targetDirectory, Path.GetFileName(file)), ref skippedFiles);
        }
    }

    private static void CopyFileBestEffort(string sourcePath, string targetPath, ref int skippedFiles)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
        catch (Exception ex)
        {
            skippedFiles++;
            Debug.WriteLine($"Backup skipped file '{sourcePath}': {ex}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not delete temporary backup directory '{path}': {ex}");
        }
    }
}

public sealed record BackupResult(string BackupPath, int SkippedFiles);
