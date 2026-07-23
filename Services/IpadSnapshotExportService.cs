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
    private const string NetworkSnapshotPackageFileName = "current.bcsnapshot";
    private const int LivePreviewMaxLongSide = 1600;
    private const int LivePreviewJpegQuality = 74;
    private const string LiveOriginalDownloadMode = "onDemandPlanned";
    private const string LiveOriginalReason = "Original wird zur Speicherersparnis nicht automatisch synchronisiert.";
    private static readonly object LogLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _exportGate = new(1, 1);

    public async Task<SnapshotExportResult> ExportNetworkSnapshotToFileAsync(
        BueroRepository repository,
        string targetFilePath,
        string? appVersion = null,
        string? deviceName = null,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<TechnicianProfile>? technicians = null)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            LogExportMessage("Local network snapshot export skipped: no target file configured.");
            return SnapshotExportResult.CreateFailure("Kein Zielpfad eingerichtet.");
        }

        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "BueroCockpit", "local-network-snapshot", Guid.NewGuid().ToString("N"));
        try
        {
            return await Task.Run(
                () => ExportNetworkSnapshotToFileCore(repository, stagingDirectory, targetFilePath, appVersion, deviceName, technicians),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogExportMessage($"Local network snapshot export failed: {targetFilePath} | {ex}");
            return SnapshotExportResult.CreateFailure("Netzwerk-Snapshot konnte nicht erstellt werden. Details siehe Log.");
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
            _exportGate.Release();
        }
    }

    private static SnapshotExportResult ExportNetworkSnapshotToFileCore(
        BueroRepository repository,
        string stagingDirectory,
        string targetFilePath,
        string? appVersion,
        string? deviceName,
        IReadOnlyCollection<TechnicianProfile>? technicians)
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

        var exportResult = ExportNetworkSnapshotCore(repository, stagingDirectory, appVersion, deviceName);
        if (!exportResult.Success)
        {
            return exportResult;
        }

        var sourcePackagePath = Path.Combine(stagingDirectory, NetworkSnapshotPackageFileName);
        ValidateSnapshotPackage(sourcePackagePath);

        var tempTargetPath = Path.Combine(
            targetDirectory,
            $"{Path.GetFileName(targetFilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePackagePath, tempTargetPath, overwrite: true);
            AddTechniciansToPackage(tempTargetPath, technicians);
            ValidateSnapshotPackage(tempTargetPath);
            PromoteSnapshotPackage(tempTargetPath, targetFilePath);
            LogExportMessage($"Local network snapshot export completed: {targetFilePath}");
            return SnapshotExportResult.CreateSuccess(targetFilePath);
        }
        catch
        {
            TryDeleteFile(tempTargetPath);
            throw;
        }
    }

    private static void AddTechniciansToPackage(
        string packagePath,
        IReadOnlyCollection<TechnicianProfile>? technicians)
    {
        var normalized = (technicians ?? Array.Empty<TechnicianProfile>())
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => new TechnicianProfile
            {
                Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id.Trim(),
                Name = profile.Name.Trim(),
                Abbreviation = profile.Abbreviation?.Trim() ?? string.Empty,
                Email = profile.Email?.Trim() ?? string.Empty,
                Phone = profile.Phone?.Trim() ?? string.Empty
            })
            .GroupBy(profile => profile.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: false);
        archive.GetEntry("technicians.json")?.Delete();
        WriteZipJsonEntry(archive, "technicians.json", normalized);
    }

    private static SnapshotExportResult ExportNetworkSnapshotCore(
        BueroRepository repository,
        string stagingDirectory,
        string? appVersion,
        string? deviceName)
    {
        var stopwatch = Stopwatch.StartNew();
        var liveDirectory = Path.Combine(stagingDirectory, "live");
        var previewsDirectory = Path.Combine(liveDirectory, "previews");
        var attachmentsDirectory = Path.Combine(liveDirectory, "attachments");

        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(liveDirectory);
        Directory.CreateDirectory(previewsDirectory);

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

        var packagePath = Path.Combine(stagingDirectory, NetworkSnapshotPackageFileName);
        var tempPackagePath = Path.Combine(
            stagingDirectory,
            $"{NetworkSnapshotPackageFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            CleanupNetworkSnapshotTempFiles(stagingDirectory);
            LogExportMessage($"Local network snapshot package export started: {packagePath}");
            WriteNetworkSnapshotPackage(tempPackagePath, packagePath, liveDirectory, previewsDirectory);
            var packageSizeBytes = TryGetFileSize(packagePath) ?? 0L;
            LogExportMessage(
                $"Local network snapshot package export completed: {packagePath} | size={packageSizeBytes / 1024d / 1024d:F1}MB");
            CleanupNetworkSnapshotTempFiles(stagingDirectory);
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPackagePath);
            LogExportMessage($"Local network snapshot package export failed: {packagePath} | {ex}");
            return SnapshotExportResult.CreateFailure("Netzwerk-Snapshot konnte nicht erstellt werden. Details siehe Log.");
        }

        stopwatch.Stop();

        LogExportMessage(
            $"iPad live sync completed: new previews={liveExport.NewPreviews}, skipped previews={liveExport.SkippedPreviews}, " +
            $"removed originals={removedOriginals}, originals copied=0, duration={stopwatch.Elapsed.TotalSeconds:F1}s, " +
            $"live size={liveSizeBytes / 1024d / 1024d:F1}MB");

        return SnapshotExportResult.CreateSuccess(packagePath);
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
            string.IsNullOrWhiteSpace(task.CustomerAddress) ? null : task.CustomerAddress,
            string.IsNullOrWhiteSpace(task.CustomerEmail) ? null : task.CustomerEmail,
            string.IsNullOrWhiteSpace(task.CustomerPhone) ? null : task.CustomerPhone,
            string.IsNullOrWhiteSpace(task.CategoryId) ? null : task.CategoryId,
            categoryIds.ToArray(),
            categoryNames,
            task.DueDate,
            task.FollowUpDate,
            string.IsNullOrWhiteSpace(task.FollowUpReason) ? null : task.FollowUpReason,
            task.CreatedAt,
            task.UpdatedAt,
            task.MaterialOrderedAt,
            string.IsNullOrWhiteSpace(task.Status) ? null : task.Status,
            string.IsNullOrWhiteSpace(task.Technician) ? null : task.Technician,
            string.IsNullOrWhiteSpace(task.WorkflowType) ? null : task.WorkflowType,
            string.IsNullOrWhiteSpace(task.WorkflowStep) ? null : task.WorkflowStep,
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

    private static void CleanupNetworkSnapshotTempFiles(string stagingDirectory)
    {
        if (!Directory.Exists(stagingDirectory))
        {
            return;
        }

        foreach (var tempFile in Directory.EnumerateFiles(stagingDirectory, $"{NetworkSnapshotPackageFileName}.*.tmp"))
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

    private static void WriteNetworkSnapshotPackage(
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

    private static void WriteZipFileEntry(ZipArchive archive, string entryName, string sourcePath)
    {
        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        sourceStream.CopyTo(entryStream);
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

    public sealed record SnapshotExportResult(bool Success, string? OutputPath, string? ErrorMessage)
    {
        public static SnapshotExportResult CreateSuccess(string outputPath) => new(true, outputPath, null);
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
        string? CustomerAddress,
        string? CustomerEmail,
        string? CustomerPhone,
        string? CurrentCategoryId,
        IReadOnlyCollection<string> CategoryIds,
        IReadOnlyCollection<string> CategoryNames,
        DateTime? DueDate,
        DateTime? ReminderDate,
        string? FollowUpReason,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? MaterialOrderedAt,
        string? Status,
        string? Technician,
        string? WorkflowType,
        string? WorkflowStep,
        string? Notes,
        string? ShortText,
        IReadOnlyCollection<string> AttachmentRefs);

    private sealed record SnapshotCategory(
        string Id,
        string Name,
        int Order,
        string? ParentId);

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
            .Select(category => new SnapshotCategory(category.Id, category.Name, category.SortOrder, category.ParentId))
            .ToList();
    }

    private static bool IsSystemSettingsCategory(CategoryItem category)
    {
        return string.Equals(category.Id, "__settings", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(category.Name?.Trim(), "Einstellungen", StringComparison.OrdinalIgnoreCase);
    }
}
