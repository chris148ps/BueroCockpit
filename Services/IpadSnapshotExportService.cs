using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BueroCockpit.Data;
using BueroCockpit.Models;

namespace BueroCockpit.Services;

public sealed class IpadSnapshotExportService
{
    private const int FormatVersion = 1;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(700);
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
            return;
        }

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
                var result = await ExportNowAsync(repository, sharedDirectory, appVersion, deviceName, currentCts.Token).ConfigureAwait(false);
                if (!result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    onError?.Invoke(result.ErrorMessage);
                }
                else if (result.Success)
                {
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

    public async Task<SnapshotExportResult> ExportNowAsync(
        BueroRepository repository,
        string sharedDirectory,
        string? appVersion = null,
        string? deviceName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sharedDirectory))
        {
            return SnapshotExportResult.CreateFailure("Kein gemeinsamer Datenordner ausgewählt.");
        }

        await _exportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ExportCore(repository, sharedDirectory, appVersion, deviceName), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"iPad snapshot export failed: {ex}");
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
                attachmentIndex.Add(new SnapshotAttachmentIndex(
                    attachment.Id,
                    attachment.TaskId,
                    attachment.FileName,
                    relativePath,
                    false,
                    File.Exists(resolvedPath)));
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
        bool IsImportant,
        bool FileExists);

    private static List<SnapshotCategory> GetCategoriesForSnapshot(BueroRepository repository)
    {
        return repository.GetAllCategories()
            .Select(category => new SnapshotCategory(category.Id, category.Name, category.SortOrder))
            .ToList();
    }
}
