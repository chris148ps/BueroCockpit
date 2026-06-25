using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BueroCockpit.Data;
using BueroCockpit.Models;
using SkiaSharp;

namespace BueroCockpit.Services;

public sealed class IpadSnapshotExportService
{
    private const int FormatVersion = 1;
    private const string SnapshotPackageFileName = "latest.bcsnapshot";
    private const string LegacySnapshotPackageTempFileName = "latest.bcsnapshot.tmp";
    private const string LivePackageFileName = "live.bclive";
    private const int LivePreviewMaxLongSide = 1600;
    private const int LivePreviewJpegQuality = 74;
    private const string LiveOriginalDownloadMode = "onDemandPlanned";
    private const string LiveOriginalReason = "Original wird zur Speicherersparnis nicht automatisch synchronisiert.";
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
                var result = await ExportLiveNowAsync(repository, sharedDirectory, appVersion, deviceName, currentCts.Token).ConfigureAwait(false);
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
            var result = await Task.Run(
                () => ExportCore(repository, sharedDirectory, appVersion, deviceName, includeFullSnapshot: true),
                cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                LogExportMessage($"iPad snapshot export completed: {result.OutputDirectory}");
            }
            else
            {
                LogExportMessage($"iPad snapshot export failed: {result.ErrorMessage}");
            }

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

    public async Task<SnapshotExportResult> ExportLiveNowAsync(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion = null,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharedDirectory))
        {
            LogExportMessage("iPad live sync skipped: no shared directory configured.");
            return SnapshotExportResult.CreateFailure("Kein gemeinsamer Datenordner ausgewählt.");
        }

        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ExportCore(repository, sharedDirectory, appVersion, deviceName, includeFullSnapshot: false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync failed: {ex}");
            return SnapshotExportResult.CreateFailure("iPad-Sync konnte nicht aktualisiert werden. Details siehe Log.");
        }
        finally
        {
            _exportGate.Release();
        }
    }

    public async Task<SnapshotExportResult> ExportLivePackageToFileAsync(
        BueroRepository repository,
        string targetFilePath,
        string? appVersion = null,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            LogExportMessage("iPad live file export skipped: no target file configured.");
            return SnapshotExportResult.CreateFailure("Kein Zielpfad eingerichtet.");
        }

        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "BueroCockpit", "ipad-live-file", Guid.NewGuid().ToString("N"));
        try
        {
            return await Task.Run(
                () => ExportLivePackageToFileCore(repository, stagingDirectory, targetFilePath, appVersion, deviceName),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live file export failed: {targetFilePath} | {ex}");
            return SnapshotExportResult.CreateFailure("iPad-Live-Datei konnte nicht geschrieben werden. Details siehe Log.");
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
            _exportGate.Release();
        }
    }

    private static SnapshotExportResult ExportLivePackageToFileCore(
        BueroRepository repository,
        string stagingDirectory,
        string targetFilePath,
        string? appVersion,
        string? deviceName)
    {
        var targetDirectory = Path.GetDirectoryName(targetFilePath);
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return SnapshotExportResult.CreateFailure("Zielpfad ist ungueltig.");
        }

        if (!Directory.Exists(targetDirectory))
        {
            return SnapshotExportResult.CreateFailure("Zielordner wurde nicht gefunden. Bitte den Ordner erneut auswählen.");
        }

        Directory.CreateDirectory(stagingDirectory);

        var exportResult = ExportCore(repository, stagingDirectory, appVersion, deviceName, includeFullSnapshot: false);
        if (!exportResult.Success)
        {
            return exportResult;
        }

        var sourcePackagePath = Path.Combine(stagingDirectory, "Sync", LivePackageFileName);
        ValidateSnapshotPackage(sourcePackagePath);

        var tempTargetPath = Path.Combine(
            targetDirectory,
            $"{Path.GetFileName(targetFilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePackagePath, tempTargetPath, overwrite: true);
            ValidateSnapshotPackage(tempTargetPath);
            PromoteSnapshotPackage(tempTargetPath, targetFilePath);
            LogExportMessage($"iPad live file export completed: {targetFilePath}");
            return SnapshotExportResult.CreateSuccess(targetFilePath);
        }
        catch
        {
            TryDeleteFile(tempTargetPath);
            throw;
        }
    }

    private static SnapshotExportResult ExportCore(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion,
        string? deviceName,
        bool includeFullSnapshot)
    {
        var stopwatch = Stopwatch.StartNew();
        var syncDirectory = Path.Combine(sharedDirectory, "Sync");
        var liveDirectory = Path.Combine(syncDirectory, "live");
        var previewsDirectory = Path.Combine(liveDirectory, "previews");
        var attachmentsDirectory = Path.Combine(liveDirectory, "attachments");
        var snapshotsDirectory = Path.Combine(syncDirectory, "snapshots");
        var inboxDirectory = Path.Combine(syncDirectory, "inbox");
        var inboxChangesDirectory = Path.Combine(inboxDirectory, "changes");
        var inboxFilesDirectory = Path.Combine(inboxDirectory, "files");
        var processedDirectory = Path.Combine(syncDirectory, "processed");
        var conflictsDirectory = Path.Combine(syncDirectory, "conflicts");

        Directory.CreateDirectory(syncDirectory);
        Directory.CreateDirectory(liveDirectory);
        Directory.CreateDirectory(previewsDirectory);
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

        var liveExport = ExportLiveAttachments(attachments, liveDirectory, previewsDirectory);

        WriteJson(Path.Combine(liveDirectory, "metadata.json"), metadata);
        WriteJson(Path.Combine(liveDirectory, "tasks.json"), tasks);
        WriteJson(Path.Combine(liveDirectory, "categories.json"), categories);
        WriteJson(Path.Combine(liveDirectory, "attachments-index.json"), liveExport.Attachments);
        CleanupLivePreviews(previewsDirectory, liveExport.Attachments);
        var removedOriginals = CleanupLiveOriginals(attachmentsDirectory);
        var liveSizeBytes = GetLiveDirectorySize(liveDirectory, previewsDirectory);

        var livePackagePath = Path.Combine(syncDirectory, LivePackageFileName);
        var tempLivePackagePath = Path.Combine(
            syncDirectory,
            $"{LivePackageFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            CleanupLivePackageTempFiles(syncDirectory);
            LogExportMessage($"live package export started: {livePackagePath}");
            WriteLivePackage(tempLivePackagePath, livePackagePath, liveDirectory, previewsDirectory);
            var livePackageSizeBytes = TryGetFileSize(livePackagePath) ?? 0L;
            LogExportMessage(
                $"live package export completed: {livePackagePath} | size={livePackageSizeBytes / 1024d / 1024d:F1}MB");
            CleanupLivePackageTempFiles(syncDirectory);
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempLivePackagePath);
            LogExportMessage($"live package export failed: {livePackagePath} | {ex}");
            return SnapshotExportResult.CreateFailure("iPad-Live-Datei konnte nicht exportiert werden. Details siehe Log.");
        }

        stopwatch.Stop();

        LogExportMessage(
            $"iPad live sync completed: new previews={liveExport.NewPreviews}, skipped previews={liveExport.SkippedPreviews}, " +
            $"removed originals={removedOriginals}, originals copied=0, duration={stopwatch.Elapsed.TotalSeconds:F1}s, " +
            $"live size={liveSizeBytes / 1024d / 1024d:F1}MB");

        if (!includeFullSnapshot)
        {
            CleanupSnapshotTempFiles(snapshotsDirectory);
            return SnapshotExportResult.CreateSuccess(liveDirectory);
        }

        WriteJson(Path.Combine(snapshotsDirectory, "metadata.json"), metadata);
        WriteJson(Path.Combine(snapshotsDirectory, "tasks.json"), tasks);
        WriteJson(Path.Combine(snapshotsDirectory, "categories.json"), categories);
        var fullSnapshotAttachments = PrepareFullSnapshotAttachments(attachments);
        WriteJson(Path.Combine(snapshotsDirectory, "attachments-index.json"), fullSnapshotAttachments);

        var packagePath = Path.Combine(snapshotsDirectory, SnapshotPackageFileName);
        var legacyTempPackagePath = Path.Combine(snapshotsDirectory, LegacySnapshotPackageTempFileName);
        var tempPackagePath = Path.Combine(
            snapshotsDirectory,
            $"{SnapshotPackageFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            if (File.Exists(legacyTempPackagePath))
            {
                File.Delete(legacyTempPackagePath);
                LogExportMessage($"iPad snapshot legacy temp file deleted: {legacyTempPackagePath}");
            }

            LogExportMessage($"iPad snapshot package export started: {packagePath}");
            WriteSnapshotPackage(
                tempPackagePath,
                packagePath,
                metadata,
                categories,
                tasks,
                fullSnapshotAttachments);
            LogExportMessage($"iPad snapshot package export completed: {packagePath}");
            CleanupSnapshotTempFiles(snapshotsDirectory);
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad snapshot package export failed: {packagePath} | {ex}");
            return SnapshotExportResult.CreateFailure("iPad-Snapshot konnte nicht exportiert werden. Details siehe Log.");
        }

        return SnapshotExportResult.CreateSuccess(liveDirectory);
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
            string.IsNullOrWhiteSpace(task.CustomerEmail) ? null : task.CustomerEmail,
            string.IsNullOrWhiteSpace(task.CustomerPhone) ? null : task.CustomerPhone,
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
                var sizeBytes = fileExists ? TryGetFileSize(resolvedPath) : null;
                var hash = fileExists ? ResolveContentHash(attachment.ContentHash, resolvedPath) : null;
                var exportHint = fileExists
                    ? null
                    : "Datei nicht im Snapshot enthalten: Quelldatei wurde nicht gefunden.";
                attachmentIndex.Add(new SnapshotAttachmentIndex(
                    attachment.Id,
                    attachment.TaskId,
                    attachment.FileName,
                    attachment.FileName,
                    attachment.FileName,
                    relativePath,
                    null,
                    null,
                    hash,
                    string.IsNullOrWhiteSpace(attachment.FileType) ? null : attachment.FileType,
                    sizeBytes,
                    false,
                    fileExists,
                    false,
                    false,
                    false,
                    LiveOriginalDownloadMode,
                    LiveOriginalReason,
                    exportHint,
                    fileExists ? resolvedPath : null));
            }
        }

        return attachmentIndex;
    }

    private static LiveAttachmentExportResult ExportLiveAttachments(
        IReadOnlyCollection<SnapshotAttachmentIndex> attachments,
        string liveDirectory,
        string previewsDirectory)
    {
        var thumbnailService = new ThumbnailService();
        var result = new List<SnapshotAttachmentIndex>(attachments.Count);
        var previewPathsByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newPreviews = 0;
        var skippedPreviews = 0;

        foreach (var attachment in attachments)
        {
            if (!attachment.FileExists || string.IsNullOrWhiteSpace(attachment.ResolvedPath) ||
                string.IsNullOrWhiteSpace(attachment.ContentHash))
            {
                result.Add(attachment with
                {
                    PackagePath = null,
                    PreviewPath = null,
                    PreviewAvailable = false,
                    OriginalAvailableInLiveSync = false,
                    OriginalDownloadMode = LiveOriginalDownloadMode,
                    Reason = LiveOriginalReason,
                    ExistsInSnapshot = false
                });
                continue;
            }

            var hash = attachment.ContentHash;
            string? previewPath = null;
            if (IsPreviewSupported(attachment.FileName))
            {
                if (previewPathsByHash.TryGetValue(hash, out var knownPreviewPath))
                {
                    previewPath = knownPreviewPath;
                    skippedPreviews++;
                }
                else
                {
                    var targetPreviewPath = Path.Combine(previewsDirectory, $"{hash}.jpg");
                    var relativePreviewPath = ToSyncRelativePath(liveDirectory, targetPreviewPath);
                    if (File.Exists(targetPreviewPath))
                    {
                        previewPath = relativePreviewPath;
                        skippedPreviews++;
                    }
                    else
                    {
                        if (TryCreateLivePreview(attachment, targetPreviewPath, thumbnailService))
                        {
                            previewPath = relativePreviewPath;
                            newPreviews++;
                        }
                        else
                        {
                            skippedPreviews++;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(previewPath))
                    {
                        previewPathsByHash[hash] = previewPath;
                    }
                }
            }
            else
            {
                skippedPreviews++;
            }

            result.Add(attachment with
            {
                PackagePath = null,
                PreviewPath = previewPath,
                PreviewAvailable = previewPath is not null,
                OriginalAvailableInLiveSync = false,
                OriginalDownloadMode = LiveOriginalDownloadMode,
                Reason = LiveOriginalReason,
                ExistsInSnapshot = false,
                ExportHint = previewPath is null ? "Keine Vorschau verfügbar." : null
            });
        }

        return new LiveAttachmentExportResult(
            result,
            newPreviews,
            skippedPreviews);
    }

    private static List<SnapshotAttachmentIndex> PrepareFullSnapshotAttachments(
        IReadOnlyCollection<SnapshotAttachmentIndex> attachments)
    {
        var packagePathByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedPackagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SnapshotAttachmentIndex>(attachments.Count);

        foreach (var attachment in attachments)
        {
            string? packagePath = null;
            if (attachment.FileExists && !string.IsNullOrWhiteSpace(attachment.ResolvedPath))
            {
                if (!string.IsNullOrWhiteSpace(attachment.ContentHash) &&
                    packagePathByHash.TryGetValue(attachment.ContentHash, out var existingPath))
                {
                    packagePath = existingPath;
                }
                else
                {
                    packagePath = CreateAttachmentPackagePath(attachment.TaskId, attachment.FileName, usedPackagePaths);
                    if (!string.IsNullOrWhiteSpace(attachment.ContentHash))
                    {
                        packagePathByHash[attachment.ContentHash] = packagePath;
                    }
                }
            }

            result.Add(attachment with { PackagePath = packagePath, PreviewPath = null });
        }

        return result;
    }

    private static void CleanupLivePreviews(
        string previewsDirectory,
        IReadOnlyCollection<SnapshotAttachmentIndex> attachments)
    {
        var referencedPreviews = attachments
            .Select(attachment => attachment.PreviewPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileName(path!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(previewsDirectory))
        {
            if (!referencedPreviews.Contains(Path.GetFileName(file)))
            {
                TryDeleteFile(file);
            }
        }

    }

    private static int CleanupLiveOriginals(string attachmentsDirectory)
    {
        if (!Directory.Exists(attachmentsDirectory))
        {
            return 0;
        }

        var rootInfo = new DirectoryInfo(attachmentsDirectory);
        if (rootInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            LogExportMessage($"iPad live sync original cleanup skipped for linked directory: {attachmentsDirectory}");
            return 0;
        }

        var removedOriginals = 0;
        foreach (var file in rootInfo.EnumerateFiles())
        {
            if (TryDeleteFile(file.FullName))
            {
                removedOriginals++;
            }
        }

        foreach (var directory in rootInfo.EnumerateDirectories())
        {
            CleanupLiveOriginalDirectory(directory, ref removedOriginals);
        }

        try
        {
            if (!rootInfo.EnumerateFileSystemInfos().Any())
            {
                rootInfo.Delete();
            }
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync original directory cleanup failed: {attachmentsDirectory} | {ex.Message}");
        }

        return removedOriginals;
    }

    private static void CleanupLiveOriginalDirectory(DirectoryInfo directory, ref int removedOriginals)
    {
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            LogExportMessage($"iPad live sync original cleanup skipped for linked directory: {directory.FullName}");
            return;
        }

        try
        {
            foreach (var file in directory.EnumerateFiles())
            {
                if (TryDeleteFile(file.FullName))
                {
                    removedOriginals++;
                }
            }

            foreach (var childDirectory in directory.EnumerateDirectories())
            {
                CleanupLiveOriginalDirectory(childDirectory, ref removedOriginals);
            }

            if (!directory.EnumerateFileSystemInfos().Any())
            {
                directory.Delete();
            }
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync original cleanup failed: {directory.FullName} | {ex.Message}");
        }
    }

    private static void CleanupSnapshotTempFiles(string snapshotsDirectory)
    {
        if (!Directory.Exists(snapshotsDirectory))
        {
            return;
        }

        foreach (var tempFile in Directory.EnumerateFiles(snapshotsDirectory, "latest.bcsnapshot*.tmp"))
        {
            TryDeleteFile(tempFile);
        }
    }

    private static void CleanupLivePackageTempFiles(string syncDirectory)
    {
        if (!Directory.Exists(syncDirectory))
        {
            return;
        }

        foreach (var tempFile in Directory.EnumerateFiles(syncDirectory, $"{LivePackageFileName}.*.tmp"))
        {
            TryDeleteFile(tempFile);
        }
    }

    private static string? ResolveContentHash(string? storedHash, string sourcePath)
    {
        var normalizedStoredHash = storedHash?.Trim().ToLowerInvariant();
        if (normalizedStoredHash is { Length: 64 } && normalizedStoredHash.All(Uri.IsHexDigit))
        {
            return normalizedStoredHash;
        }

        try
        {
            using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync hash failed: {sourcePath} | {ex.Message}");
            return null;
        }
    }

    private static bool IsPreviewSupported(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() is
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".pdf";
    }

    private static bool TryCreateLivePreview(
        SnapshotAttachmentIndex attachment,
        string targetPreviewPath,
        ThumbnailService thumbnailService)
    {
        try
        {
            var sourcePath = attachment.ResolvedPath!;
            var previewSourcePath = sourcePath;
            if (Path.GetExtension(attachment.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                previewSourcePath = thumbnailService.EnsureThumbnail(new AttachmentItem
                {
                    Id = attachment.Id,
                    TaskId = attachment.TaskId,
                    FileName = attachment.FileName,
                    StoredPath = sourcePath,
                    FileType = attachment.ContentType ?? ".pdf"
                }) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(previewSourcePath) || !File.Exists(previewSourcePath))
            {
                return false;
            }

            using var source = SKBitmap.Decode(previewSourcePath);
            if (source is null || source.Width <= 0 || source.Height <= 0)
            {
                return false;
            }

            var scale = Math.Min(1d, LivePreviewMaxLongSide / (double)Math.Max(source.Width, source.Height));
            var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var resized = source.Resize(
                new SKImageInfo(targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized is null)
            {
                return false;
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, LivePreviewJpegQuality);
            if (encoded is null)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPreviewPath)!);
            var tempPath = $"{targetPreviewPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    encoded.SaveTo(stream);
                }

                File.Move(tempPath, targetPreviewPath, overwrite: false);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync preview skipped: {attachment.FileName} | {ex.Message}");
            return false;
        }
    }

    private static long GetLiveDirectorySize(string liveDirectory, string previewsDirectory)
    {
        try
        {
            var jsonSize = Directory.EnumerateFiles(liveDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Sum(path => TryGetFileSize(path) ?? 0L);
            var previewSize = Directory.EnumerateFiles(previewsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Sum(path => TryGetFileSize(path) ?? 0L);
            return jsonSize + previewSize;
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad live sync size calculation failed: {liveDirectory} | {ex.Message}");
            return 0;
        }
    }

    private static string ToSyncRelativePath(string liveDirectory, string path)
    {
        return Path.GetRelativePath(liveDirectory, path).Replace('\\', '/');
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
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Move(tempPath, path, overwrite: true);
            }
            catch (IOException)
            {
                File.Move(tempPath, path, overwrite: true);
            }
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
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
                var attachmentsForIndex = new List<SnapshotAttachmentIndex>(attachments.Count);
                var writtenAttachmentEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var attachment in attachments)
                {
                    if (!string.IsNullOrWhiteSpace(attachment.PackagePath) &&
                        !string.IsNullOrWhiteSpace(attachment.ResolvedPath) &&
                        File.Exists(attachment.ResolvedPath))
                    {
                        string? exportHint = null;
                        var copied = writtenAttachmentEntries.Contains(attachment.PackagePath) ||
                            TryWriteZipFileEntry(archive, attachment.PackagePath, attachment.ResolvedPath, out exportHint);
                        if (copied)
                        {
                            writtenAttachmentEntries.Add(attachment.PackagePath);
                        }
                        attachmentsForIndex.Add(attachment with
                        {
                            ExistsInSnapshot = copied,
                            PackagePath = copied ? attachment.PackagePath : null,
                            ExportHint = copied ? null : exportHint ?? "Datei konnte nicht ins Snapshot-Paket kopiert werden."
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(attachment.ResolvedPath))
                    {
                        attachmentsForIndex.Add(attachment with
                        {
                            PackagePath = null,
                            ExistsInSnapshot = false,
                            ExportHint = "Datei nicht im Snapshot enthalten: Quelldatei wurde nicht gefunden."
                        });
                    }
                    else
                    {
                        attachmentsForIndex.Add(attachment);
                    }
                }
                WriteZipJsonEntry(archive, "attachments-index.json", attachmentsForIndex);
            }

            ValidateSnapshotPackage(tempPackagePath);

            PromoteSnapshotPackage(tempPackagePath, finalPackagePath);
        }
        catch
        {
            TryDeleteFile(tempPackagePath);
            throw;
        }
    }

    private static void WriteLivePackage(
        string tempPackagePath,
        string finalPackagePath,
        string liveDirectory,
        string previewsDirectory)
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
                foreach (var fileName in new[]
                         {
                             "metadata.json",
                             "categories.json",
                             "tasks.json",
                             "attachments-index.json"
                         })
                {
                    WriteZipFileEntry(archive, fileName, Path.Combine(liveDirectory, fileName));
                }

                archive.CreateEntry("previews/");
                foreach (var previewPath in Directory.EnumerateFiles(previewsDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    WriteZipFileEntry(archive, $"previews/{Path.GetFileName(previewPath)}", previewPath);
                }
            }

            ValidateSnapshotPackage(tempPackagePath);
            PromoteSnapshotPackage(tempPackagePath, finalPackagePath);
        }
        catch
        {
            TryDeleteFile(tempPackagePath);
            throw;
        }
    }

    private static void PromoteSnapshotPackage(string tempPackagePath, string finalPackagePath)
    {
        if (!File.Exists(finalPackagePath))
        {
            File.Move(tempPackagePath, finalPackagePath);
            return;
        }

        try
        {
            File.Replace(tempPackagePath, finalPackagePath, null);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(tempPackagePath, finalPackagePath, overwrite: true);
        }
        catch (IOException ex)
        {
            LogExportMessage($"iPad snapshot package replace failed, trying overwrite move: {ex.Message}");
            File.Move(tempPackagePath, finalPackagePath, overwrite: true);
        }
    }

    private static void ValidateSnapshotPackage(string packagePath)
    {
        var requiredEntries = new HashSet<string>(StringComparer.Ordinal)
        {
            "metadata.json",
            "categories.json",
            "tasks.json",
            "attachments-index.json"
        };

        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries)
        {
            requiredEntries.Remove(entry.FullName);
            using var entryStream = entry.Open();
            entryStream.CopyTo(Stream.Null);
        }

        if (requiredEntries.Count > 0)
        {
            throw new InvalidDataException(
                $"Snapshot-Paket enthält nicht alle Pflichtdateien: {string.Join(", ", requiredEntries)}");
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

    private static bool TryWriteZipFileEntry(ZipArchive archive, string entryName, string sourcePath, out string? exportHint)
    {
        try
        {
            WriteZipFileEntry(archive, entryName, sourcePath);
            exportHint = null;
            return true;
        }
        catch (Exception ex)
        {
            LogExportMessage($"iPad snapshot attachment skipped: {sourcePath} | {ex.Message}");
            exportHint = $"Datei konnte nicht ins Snapshot-Paket kopiert werden: {ex.Message}";
            return false;
        }
    }

    private static void WriteZipFileEntry(ZipArchive archive, string entryName, string sourcePath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        sourceStream.CopyTo(entryStream);
    }

    private static string CreateAttachmentPackagePath(string taskId, string fileName, ISet<string> usedPackagePaths)
    {
        var safeTaskId = SanitizeZipPathSegment(taskId);
        var safeFileName = SanitizeZipPathSegment(Path.GetFileName(fileName));
        if (string.IsNullOrWhiteSpace(safeTaskId))
        {
            safeTaskId = "Aufgabe";
        }

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "Anhang";
        }

        var extension = Path.GetExtension(safeFileName);
        var stem = string.IsNullOrWhiteSpace(extension)
            ? safeFileName
            : safeFileName[..^extension.Length];
        var candidate = $"attachments/{safeTaskId}/{safeFileName}";
        var counter = 2;
        while (!usedPackagePaths.Add(candidate))
        {
            candidate = $"attachments/{safeTaskId}/{stem}-{counter}{extension}";
            counter++;
        }

        return candidate;
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

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Der Aufräumfehler ist für den Export nicht relevant.
        }

        return false;
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
        catch
        {
            // Temporäre Exportdaten dürfen den App-Lauf nicht blockieren.
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
        string? CustomerEmail,
        string? CustomerPhone,
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
        string OriginalFileName,
        string DisplayName,
        string RelativePath,
        string? PackagePath,
        string? PreviewPath,
        string? ContentHash,
        string? ContentType,
        long? SizeBytes,
        bool IsImportant,
        bool FileExists,
        bool ExistsInSnapshot,
        bool PreviewAvailable,
        bool OriginalAvailableInLiveSync,
        string OriginalDownloadMode,
        string Reason,
        string? ExportHint,
        [property: JsonIgnore] string? ResolvedPath);

    private sealed record LiveAttachmentExportResult(
        IReadOnlyCollection<SnapshotAttachmentIndex> Attachments,
        int NewPreviews,
        int SkippedPreviews);

    private static List<SnapshotCategory> GetCategoriesForSnapshot(BueroRepository repository)
    {
        return repository.GetCategories()
            .Where(category => !IsSystemSettingsCategory(category))
            .Select(category => new SnapshotCategory(category.Id, category.Name, category.SortOrder))
            .ToList();
    }

    private static bool IsSystemSettingsCategory(CategoryItem category)
    {
        return string.Equals(category.Id, "__settings", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(category.Name?.Trim(), "Einstellungen", StringComparison.OrdinalIgnoreCase);
    }
}
