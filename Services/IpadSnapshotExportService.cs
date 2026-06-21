using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BueroCockpit.Data;
using BueroCockpit.Models;

namespace BueroCockpit.Services;

public sealed class IpadSnapshotExportService
{
    private const int FormatVersion = 1;
    private const string SnapshotPackageFileName = "latest.bcsnapshot";
    private const string SnapshotPackageTempFileName = "latest.bcsnapshot.tmp";
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(700);
    private static readonly object LogLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _exportGate = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();

    public void RequestExport(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion = null,
        string? deviceName = null,
        Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(sharedDirectory))
        {
            LogExportMessage("iPad snapshot export request skipped: no shared directory configured.");
            return;
        }

        LogExportMessage($"iPad snapshot export queued: {sharedDirectory}");

        CancellationTokenSource? previousCts;
        var currentCts = new CancellationTokenSource();
        lock (_debounceLock)
        {
            previousCts = _debounceCts;
            _debounceCts = currentCts;
        }

        previousCts?.Cancel();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, currentCts.Token).ConfigureAwait(false);
                LogExportMessage($"iPad snapshot export started (debounced): {sharedDirectory}");
                var result = await ExportNowAsync(repository, sharedDirectory, appVersion, deviceName, currentCts.Token).ConfigureAwait(false);
                if (!result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    LogExportMessage($"iPad snapshot export failed (debounced): {result.ErrorMessage}");
                    onError?.Invoke(result.ErrorMessage);
                }
                else if (result.Success)
                {
                    LogExportMessage($"iPad snapshot export completed (debounced): {result.OutputDirectory}");
                    onError?.Invoke(string.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                // Neue Änderungen haben den Export ersetzt.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iPad snapshot export request failed: {ex}");
                onError?.Invoke("iPad-Snapshot konnte nicht aktualisiert werden.");
            }
            finally
            {
                if (ReferenceEquals(_debounceCts, currentCts))
                {
                    lock (_debounceLock)
                    {
                        if (ReferenceEquals(_debounceCts, currentCts))
                        {
                            _debounceCts = null;
                        }
                    }
                }

                currentCts.Dispose();
            }
        });
    }

    public void LogDiagnostic(string message)
    {
        LogExportMessage(message);
    }

    public async Task<SnapshotExportResult> ExportNowAsync(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion = null,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharedDirectory))
        {
            LogExportMessage("iPad snapshot export skipped: no shared directory configured.");
            return SnapshotExportResult.CreateFailure("Kein gemeinsamer Datenordner ausgewählt.");
        }

        LogExportMessage($"iPad snapshot export started: {sharedDirectory}");

        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(() => ExportCore(repository, sharedDirectory, appVersion, deviceName), cancellationToken).ConfigureAwait(false);
            LogExportMessage($"iPad snapshot export completed: {result.OutputDirectory}");
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"iPad snapshot export failed: {ex}");
            LogExportMessage($"iPad snapshot export failed: {ex}");
            return SnapshotExportResult.CreateFailure("iPad-Snapshot konnte nicht aktualisiert werden.");
        }
        finally
        {
            _exportGate.Release();
        }
    }

    private static SnapshotExportResult ExportCore(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion,
        string? deviceName)
    {
        var syncDirectory = Path.Combine(sharedDirectory, "Sync");
        var snapshotsDirectory = Path.Combine(syncDirectory, "snapshots");
        var inboxDirectory = Path.Combine(syncDirectory, "inbox");
        var inboxChangesDirectory = Path.Combine(inboxDirectory, "changes");
        var inboxFilesDirectory = Path.Combine(inboxDirectory, "files");
        var processedDirectory = Path.Combine(syncDirectory, "processed");
        var conflictsDirectory = Path.Combine(syncDirectory, "conflicts");

        Directory.CreateDirectory(syncDirectory);
        Directory.CreateDirectory(snapshotsDirectory);
        Directory.CreateDirectory(inboxDirectory);
        Directory.CreateDirectory(inboxChangesDirectory);
        Directory.CreateDirectory(inboxFilesDirectory);
        Directory.CreateDirectory(processedDirectory);
        Directory.CreateDirectory(conflictsDirectory);

        var categories = GetCategoriesForSnapshot(repository);
        var categoryLookup = categories.ToDictionary(category => category.Id, category => category.Name, StringComparer.OrdinalIgnoreCase);

        var tasks = repository.GetTasks()
            .Where(task => !task.IsDeleted)
            .Select(task => CreateTaskSnapshot(task, categoryLookup, repository))
            .ToList();

        var attachments = CreateAttachmentIndex(repository, tasks);

        var metadata = new SnapshotMetadata(
            FormatVersion,
            DateTimeOffset.UtcNow,
            "BueroCockpit",
            string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim(),
            string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            "BueroCockpit");

        WriteJson(Path.Combine(snapshotsDirectory, "metadata.json"), metadata);
        WriteJson(Path.Combine(snapshotsDirectory, "tasks.json"), tasks);
        WriteJson(Path.Combine(snapshotsDirectory, "categories.json"), categories);
        WriteJson(Path.Combine(snapshotsDirectory, "attachments-index.json"), attachments);

        var packagePath = Path.Combine(snapshotsDirectory, SnapshotPackageFileName);
        try
        {
            LogExportMessage($"iPad snapshot package export started: {packagePath}");
            WriteSnapshotPackage(
                Path.Combine(snapshotsDirectory, SnapshotPackageTempFileName),
                packagePath,
                metadata,
                categories,
                tasks,
                attachments);
            LogExportMessage($"iPad snapshot package export completed: {packagePath}");
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad snapshot package export failed: {packagePath} | {ex}");
        }

        return SnapshotExportResult.CreateSuccess(syncDirectory);
    }

    private static SnapshotTask CreateTaskSnapshot(TaskItem task, IReadOnlyDictionary<string, string> categoryLookup, BueroRepository repository)
    {
        var categoryIds = GetTaskCategoryIds(task);
        var categoryNames = categoryIds
            .Select(id => categoryLookup.TryGetValue(id, out var name) ? name : id)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var attachments = repository.GetAttachments(task.Id);
        return new SnapshotTask(
            task.Id,
            task.Title,
            task.CustomerName,
            categoryIds.ToArray(),
            categoryNames,
            task.DueDate,
            task.FollowUpDate,
            task.CreatedAt,
            task.UpdatedAt,
            task.MaterialOrderedAt,
            string.IsNullOrWhiteSpace(task.Status) ? null : task.Status,
            string.IsNullOrWhiteSpace(task.Description) ? null : task.Description,
            CreateShortText(task.Description),
            attachments.Select(attachment => attachment.Id).ToArray());
    }

    private static List<SnapshotAttachmentIndex> CreateAttachmentIndex(BueroRepository repository, IReadOnlyCollection<SnapshotTask> tasks)
    {
        var attachmentIndex = new List<SnapshotAttachmentIndex>();
        var taskIds = tasks.Select(task => task.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var taskId in taskIds)
        {
            foreach (var attachment in repository.GetAttachments(taskId))
            {
                var relativePath = AppPaths.ToStoredPath(attachment.StoredPath);
                var resolvedPath = AppPaths.ResolveTaskAttachmentPath(attachment.TaskId, attachment.StoredPath, attachment.FileName);
                var fileExists = File.Exists(resolvedPath);
                var packagePath = fileExists
                    ? CreateAttachmentPackagePath(attachment.Id, attachment.FileName)
                    : null;
                var sizeBytes = fileExists ? TryGetFileSize(resolvedPath) : null;
                attachmentIndex.Add(new SnapshotAttachmentIndex(
                    attachment.Id,
                    attachment.TaskId,
                    attachment.FileName,
                    relativePath,
                    packagePath,
                    string.IsNullOrWhiteSpace(attachment.FileType) ? null : attachment.FileType,
                    sizeBytes,
                    false,
                    fileExists,
                    fileExists ? resolvedPath : null));
            }
        }

        return attachmentIndex;
    }

    private static string[] GetTaskCategoryIds(TaskItem task)
    {
        var categoryIds = task.CategoryIds.Count > 0
            ? task.CategoryIds.ToList()
            : string.IsNullOrWhiteSpace(task.CategoryId)
                ? new List<string>()
                : new List<string> { task.CategoryId };

        return categoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? CreateShortText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (trimmed.Length <= 160)
        {
            return trimmed;
        }

        return trimmed[..160];
    }

    private static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteSnapshotPackage<TMetadata, TCategory, TTask>(
        string tempPackagePath,
        string finalPackagePath,
        TMetadata metadata,
        IReadOnlyCollection<TCategory> categories,
        IReadOnlyCollection<TTask> tasks,
        IReadOnlyCollection<SnapshotAttachmentIndex> attachments)
    {
        if (File.Exists(tempPackagePath))
        {
            File.Delete(tempPackagePath);
        }

        try
        {
            using (var fileStream = new FileStream(tempPackagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteZipJsonEntry(archive, "metadata.json", metadata);
                WriteZipJsonEntry(archive, "categories.json", categories);
                WriteZipJsonEntry(archive, "tasks.json", tasks);
                WriteZipJsonEntry(archive, "attachments-index.json", attachments);
                foreach (var attachment in attachments)
                {
                    if (!string.IsNullOrWhiteSpace(attachment.PackagePath) &&
                        !string.IsNullOrWhiteSpace(attachment.ResolvedPath) &&
                        File.Exists(attachment.ResolvedPath))
                    {
                        TryWriteZipFileEntry(archive, attachment.PackagePath, attachment.ResolvedPath);
                    }
                }
            }

            if (File.Exists(finalPackagePath))
            {
                File.Replace(tempPackagePath, finalPackagePath, null);
            }
            else
            {
                File.Move(tempPackagePath, finalPackagePath);
            }
        }
        catch
        {
            TryDeleteFile(tempPackagePath);
            throw;
        }
    }

    private static void WriteZipJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static long? TryGetFileSize(string sourcePath)
    {
        try
        {
            return new FileInfo(sourcePath).Length;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteZipFileEntry(ZipArchive archive, string entryName, string sourcePath)
    {
        try
        {
            WriteZipFileEntry(archive, entryName, sourcePath);
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad snapshot attachment skipped: {sourcePath} | {ex.Message}");
        }
    }

    private static void WriteZipFileEntry(ZipArchive archive, string entryName, string sourcePath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        sourceStream.CopyTo(entryStream);
    }

    private static string CreateAttachmentPackagePath(string attachmentId, string fileName)
    {
        var safeAttachmentId = SanitizeZipPathSegment(attachmentId);
        var safeFileName = SanitizeZipPathSegment(Path.GetFileName(fileName));
        if (string.IsNullOrWhiteSpace(safeAttachmentId))
        {
            safeAttachmentId = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "Anhang";
        }

        return $"attachments/{safeAttachmentId}/{safeFileName}";
    }

    private static string SanitizeZipPathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            .ToHashSet();
        var builder = new StringBuilder(value.Trim().Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalidChars.Contains(character) || char.IsControl(character) ? '_' : character);
        }

        return builder.ToString().Trim('_');
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
        catch
        {
            // Der Aufräumfehler ist für den Export nicht relevant.
        }
    }

    private static void LogExportMessage(string message)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            var logPath = Path.Combine(AppPaths.AppDataDirectory, "ipad-snapshot-export.log");
            var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
            lock (LogLock)
            {
                File.AppendAllText(logPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging darf den Export nie blockieren.
        }

        Debug.WriteLine(message);
    }

    public sealed record SnapshotExportResult(bool Success, string? OutputDirectory, string? ErrorMessage)
    {
        public static SnapshotExportResult CreateSuccess(string outputDirectory) => new(true, outputDirectory, null);
        public static SnapshotExportResult CreateFailure(string errorMessage) => new(false, null, errorMessage);
    }

    private sealed record SnapshotMetadata(
        int FormatVersion,
        DateTimeOffset ExportedAt,
        string AppName,
        string? AppVersion,
        string? DeviceName,
        string Source);

    private sealed record SnapshotTask(
        string Id,
        string Title,
        string CustomerName,
        IReadOnlyCollection<string> CategoryIds,
        IReadOnlyCollection<string> CategoryNames,
        DateTime? DueDate,
        DateTime? ReminderDate,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? MaterialOrderedAt,
        string? Status,
        string? Notes,
        string? ShortText,
        IReadOnlyCollection<string> AttachmentRefs);

    private sealed record SnapshotCategory(
        string Id,
        string Name,
        int Order);

    private sealed record SnapshotAttachmentIndex(
        string Id,
        string TaskId,
        string FileName,
        string RelativePath,
        string? PackagePath,
        string? ContentType,
        long? SizeBytes,
        bool IsImportant,
        bool FileExists,
        [property: JsonIgnore] string? ResolvedPath);

    private static List<SnapshotCategory> GetCategoriesForSnapshot(BueroRepository repository)
    {
        return repository.GetAllCategories()
            .Select(category => new SnapshotCategory(category.Id, category.Name, category.SortOrder))
            .ToList();
    }
}
