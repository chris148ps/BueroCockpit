using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncDeltaStore
{
    public const string ApiVersion = "local-sync-delta-v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _statePath;

    public LocalSyncDeltaStore(string? statePath = null)
    {
        _statePath = string.IsNullOrWhiteSpace(statePath)
            ? AppPaths.LocalNetworkSyncStatePath
            : Path.GetFullPath(statePath);
    }

    public LocalSyncDeltaResponse BuildDelta(
        string deviceId,
        string? confirmedRevision,
        LocalSyncSnapshotPackage package)
    {
        var snapshot = ReadSnapshot(package.FilePath);
        lock (_gate)
        {
            var state = LoadUnsafe();
            var revision = UpdateServerRevision(state, snapshot.AggregateHash);
            var device = GetOrCreateDevice(state, deviceId);
            if (string.IsNullOrWhiteSpace(device.ConfirmedServerRevision) ||
                !string.Equals(device.ConfirmedServerRevision, confirmedRevision?.Trim(), StringComparison.Ordinal))
            {
                SaveUnsafe(state);
                return LocalSyncDeltaResponse.RequiresFirstSync(revision, state.ServerSequence);
            }

            var changedTasks = ChangedItems(snapshot.Tasks, device.ItemHashes, "task");
            var changedCategories = ChangedItems(snapshot.Categories, device.ItemHashes, "category");
            var changedTechnicians = ChangedItems(snapshot.Technicians, device.ItemHashes, "technician");
            var changedAttachments = ChangedItems(snapshot.Attachments, device.ItemHashes, "attachment");
            var tombstones = BuildTombstones(device.ItemHashes.Keys, snapshot.ItemHashes.Keys);
            var changedFiles = BuildChangedFiles(snapshot, changedAttachments, device.FileHashes);

            var hasChanges =
                changedTasks.Count > 0 ||
                changedCategories.Count > 0 ||
                changedTechnicians.Count > 0 ||
                changedAttachments.Count > 0 ||
                changedFiles.Count > 0 ||
                tombstones.TaskIds.Count > 0 ||
                tombstones.CategoryIds.Count > 0 ||
                tombstones.TechnicianIds.Count > 0 ||
                tombstones.AttachmentIds.Count > 0;

            if (!hasChanges)
            {
                device.LastStatus = "no-changes";
                device.LastAttemptUtc = DateTimeOffset.UtcNow;
                SaveUnsafe(state);
                return LocalSyncDeltaResponse.NoChanges(
                    device.ConfirmedServerRevision,
                    state.ServerSequence,
                    device.LastConfirmedClientSequence,
                    snapshot.ItemHashes.Count,
                    snapshot.FileHashes.Count);
            }

            var token = Guid.NewGuid().ToString("N");
            device.Pending = new LocalSyncPendingCheckpoint
            {
                AckToken = token,
                ServerRevision = revision,
                ServerSequence = state.ServerSequence,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ItemHashes = snapshot.ItemHashes,
                FileHashes = snapshot.FileHashes
            };
            device.LastStatus = "awaiting-ack";
            device.LastAttemptUtc = DateTimeOffset.UtcNow;
            SaveUnsafe(state);

            return new LocalSyncDeltaResponse(
                ApiVersion,
                false,
                device.ConfirmedServerRevision,
                revision,
                state.ServerSequence,
                device.LastConfirmedClientSequence,
                token,
                changedTasks,
                changedCategories,
                changedTechnicians,
                changedAttachments,
                tombstones,
                changedFiles,
                new LocalSyncDeltaCounts(
                    changedTasks.Count,
                    changedCategories.Count,
                    changedTechnicians.Count,
                    changedAttachments.Count,
                    changedFiles.Count,
                    tombstones.TotalCount,
                    Math.Max(
                        0,
                        snapshot.ItemHashes.Count -
                        changedTasks.Count -
                        changedCategories.Count -
                        changedTechnicians.Count -
                        changedAttachments.Count),
                    Math.Max(0, snapshot.FileHashes.Count - changedFiles.Count)));
        }
    }

    public LocalSyncPreparedCheckpoint PrepareFullSync(string deviceId, LocalSyncSnapshotPackage package)
    {
        var snapshot = ReadSnapshot(package.FilePath);
        lock (_gate)
        {
            var state = LoadUnsafe();
            var revision = UpdateServerRevision(state, snapshot.AggregateHash);
            var device = GetOrCreateDevice(state, deviceId);
            var token = Guid.NewGuid().ToString("N");
            device.Pending = new LocalSyncPendingCheckpoint
            {
                AckToken = token,
                ServerRevision = revision,
                ServerSequence = state.ServerSequence,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ItemHashes = snapshot.ItemHashes,
                FileHashes = snapshot.FileHashes
            };
            device.LastStatus = "full-sync-awaiting-ack";
            device.LastAttemptUtc = DateTimeOffset.UtcNow;
            SaveUnsafe(state);
            return new LocalSyncPreparedCheckpoint(token, revision, state.ServerSequence);
        }
    }

    public LocalSyncAckResponse Confirm(
        string deviceId,
        LocalSyncAckRequest request)
    {
        lock (_gate)
        {
            var state = LoadUnsafe();
            var device = GetOrCreateDevice(state, deviceId);
            var pending = device.Pending;
            if (pending is null ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(pending.AckToken),
                    Encoding.UTF8.GetBytes(request.AckToken?.Trim() ?? string.Empty)) ||
                !string.Equals(pending.ServerRevision, request.ServerRevision?.Trim(), StringComparison.Ordinal))
            {
                device.LastStatus = "ack-rejected";
                device.LastAttemptUtc = DateTimeOffset.UtcNow;
                SaveUnsafe(state);
                return new LocalSyncAckResponse(
                    ApiVersion,
                    "rejected",
                    device.ConfirmedServerRevision,
                    device.ConfirmedServerSequence,
                    device.LastConfirmedClientSequence,
                    "Bestätigung passt nicht zum ausstehenden Sync-Stand.");
            }

            device.ConfirmedServerRevision = pending.ServerRevision;
            device.ConfirmedServerSequence = pending.ServerSequence;
            device.ItemHashes = pending.ItemHashes;
            device.FileHashes = pending.FileHashes;
            device.Pending = null;
            device.LastSuccessfulSyncUtc = DateTimeOffset.UtcNow;
            device.LastStatus = "confirmed";
            device.LastAttemptUtc = device.LastSuccessfulSyncUtc;
            if (request.LastConfirmedClientSequence is > 0)
            {
                device.LastConfirmedClientSequence = Math.Max(
                    device.LastConfirmedClientSequence,
                    request.LastConfirmedClientSequence.Value);
            }
            SaveUnsafe(state);
            return new LocalSyncAckResponse(
                ApiVersion,
                "confirmed",
                device.ConfirmedServerRevision,
                device.ConfirmedServerSequence,
                device.LastConfirmedClientSequence,
                "Synchronisationsstand bestätigt.");
        }
    }

    public void RecordClientSequence(string deviceId, long? clientSequence)
    {
        if (clientSequence is not > 0)
        {
            return;
        }

        lock (_gate)
        {
            var state = LoadUnsafe();
            var device = GetOrCreateDevice(state, deviceId);
            device.LastConfirmedClientSequence = Math.Max(device.LastConfirmedClientSequence, clientSequence.Value);
            SaveUnsafe(state);
        }
    }

    public bool DeleteDeviceCheckpoint(string deviceId)
    {
        lock (_gate)
        {
            var normalizedDeviceId = deviceId.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDeviceId))
            {
                return false;
            }

            var state = LoadUnsafe();
            if (!state.Devices.Remove(normalizedDeviceId))
            {
                return false;
            }

            SaveUnsafe(state);
            return true;
        }
    }

    private static List<JsonElement> ChangedItems(
        IReadOnlyDictionary<string, JsonElement> current,
        IReadOnlyDictionary<string, string> confirmedHashes,
        string kind)
    {
        return current
            .Where(item =>
            {
                var key = $"{kind}:{item.Key}";
                return !confirmedHashes.TryGetValue(key, out var confirmed) ||
                       !string.Equals(confirmed, Hash(item.Value.GetRawText()), StringComparison.Ordinal);
            })
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Value.Clone())
            .ToList();
    }

    private static LocalSyncDeltaTombstones BuildTombstones(
        IEnumerable<string> confirmedKeys,
        IEnumerable<string> currentKeys)
    {
        var current = currentKeys.ToHashSet(StringComparer.Ordinal);
        var missing = confirmedKeys
            .Where(key => !current.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        return new LocalSyncDeltaTombstones(
            Ids(missing, "task:"),
            Ids(missing, "category:"),
            Ids(missing, "technician:"),
            Ids(missing, "attachment:"));
    }

    private static IReadOnlyList<string> Ids(IEnumerable<string> keys, string prefix) =>
        keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..])
            .ToList();

    private static List<LocalSyncDeltaFile> BuildChangedFiles(
        SnapshotState snapshot,
        IReadOnlyCollection<JsonElement> changedAttachments,
        IReadOnlyDictionary<string, string> confirmedFileHashes)
    {
        var result = new List<LocalSyncDeltaFile>();
        var requestedPaths = changedAttachments
            .SelectMany(AttachmentPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var path in requestedPaths)
        {
            if (!snapshot.Files.TryGetValue(path, out var data))
            {
                continue;
            }

            var hash = Hash(data);
            if (confirmedFileHashes.TryGetValue(path, out var confirmedHash) &&
                string.Equals(hash, confirmedHash, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new LocalSyncDeltaFile(path, data.LongLength, hash, Convert.ToBase64String(data)));
        }

        return result;
    }

    private static IEnumerable<string> AttachmentPaths(JsonElement attachment)
    {
        foreach (var name in new[] { "packagePath", "previewPath" })
        {
            if (attachment.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var path = value.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return path.Replace('\\', '/').TrimStart('/');
                }
            }
        }
    }

    private static SnapshotState ReadSnapshot(string packagePath)
    {
        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var tasks = ReadItems(archive, "tasks.json");
        var categories = ReadItems(archive, "categories.json");
        var technicians = ReadItems(archive, "technicians.json", required: false);
        var attachments = ReadItems(archive, "attachments-index.json", required: false);
        var itemHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        AddHashes(itemHashes, "task", tasks);
        AddHashes(itemHashes, "category", categories);
        AddHashes(itemHashes, "technician", technicians);
        AddHashes(itemHashes, "attachment", attachments);

        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attachment in attachments.Values)
        {
            foreach (var path in AttachmentPaths(attachment))
            {
                if (files.ContainsKey(path))
                {
                    continue;
                }

                var entry = archive.Entries.FirstOrDefault(candidate =>
                    string.Equals(candidate.FullName.Replace('\\', '/').TrimStart('/'), path, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var memory = new MemoryStream();
                entryStream.CopyTo(memory);
                var data = memory.ToArray();
                files[path] = data;
                fileHashes[path] = Hash(data);
            }
        }

        var aggregate = new StringBuilder();
        foreach (var item in itemHashes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            aggregate.Append(item.Key).Append('=').Append(item.Value).Append('\n');
        }
        foreach (var item in fileHashes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            aggregate.Append("file:").Append(item.Key).Append('=').Append(item.Value).Append('\n');
        }

        return new SnapshotState(
            tasks,
            categories,
            technicians,
            attachments,
            itemHashes,
            files,
            fileHashes,
            Hash(aggregate.ToString()));
    }

    private static Dictionary<string, JsonElement> ReadItems(
        ZipArchive archive,
        string fileName,
        bool required = true)
    {
        var entry = archive.GetEntry(fileName);
        if (entry is null)
        {
            if (!required)
            {
                return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            }
            throw new InvalidDataException($"{fileName} fehlt im Sync-Paket.");
        }

        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{fileName} enthält keine Liste.");
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idElement.GetString()))
            {
                continue;
            }
            result[idElement.GetString()!.Trim()] = item.Clone();
        }
        return result;
    }

    private static void AddHashes(
        IDictionary<string, string> destination,
        string kind,
        IReadOnlyDictionary<string, JsonElement> items)
    {
        foreach (var item in items)
        {
            destination[$"{kind}:{item.Key}"] = Hash(item.Value.GetRawText());
        }
    }

    private static string UpdateServerRevision(LocalSyncStateFile state, string aggregateHash)
    {
        if (!string.Equals(state.LastAggregateHash, aggregateHash, StringComparison.Ordinal))
        {
            state.ServerSequence++;
            state.LastAggregateHash = aggregateHash;
        }
        return $"server-{state.ServerSequence}";
    }

    private static LocalSyncDeviceCheckpoint GetOrCreateDevice(LocalSyncStateFile state, string deviceId)
    {
        var key = deviceId.Trim();
        if (!state.Devices.TryGetValue(key, out var device))
        {
            device = new LocalSyncDeviceCheckpoint { DeviceId = key };
            state.Devices[key] = device;
        }
        return device;
    }

    private LocalSyncStateFile LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new LocalSyncStateFile();
            }
            return JsonSerializer.Deserialize<LocalSyncStateFile>(File.ReadAllText(_statePath), JsonOptions)
                   ?? new LocalSyncStateFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LocalSyncStateFile();
        }
    }

    private void SaveUnsafe(LocalSyncStateFile state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var temporaryPath = $"{_statePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, _statePath, overwrite: true);
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed record SnapshotState(
        IReadOnlyDictionary<string, JsonElement> Tasks,
        IReadOnlyDictionary<string, JsonElement> Categories,
        IReadOnlyDictionary<string, JsonElement> Technicians,
        IReadOnlyDictionary<string, JsonElement> Attachments,
        Dictionary<string, string> ItemHashes,
        IReadOnlyDictionary<string, byte[]> Files,
        Dictionary<string, string> FileHashes,
        string AggregateHash);

    private sealed class LocalSyncStateFile
    {
        public int SchemaVersion { get; set; } = 1;
        public long ServerSequence { get; set; }
        public string LastAggregateHash { get; set; } = string.Empty;
        public Dictionary<string, LocalSyncDeviceCheckpoint> Devices { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LocalSyncDeviceCheckpoint
    {
        public string DeviceId { get; set; } = string.Empty;
        public string ConfirmedServerRevision { get; set; } = string.Empty;
        public long ConfirmedServerSequence { get; set; }
        public long LastConfirmedClientSequence { get; set; }
        public DateTimeOffset? LastSuccessfulSyncUtc { get; set; }
        public DateTimeOffset? LastAttemptUtc { get; set; }
        public string LastStatus { get; set; } = "never";
        public Dictionary<string, string> ItemHashes { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> FileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public LocalSyncPendingCheckpoint? Pending { get; set; }
    }

    private sealed class LocalSyncPendingCheckpoint
    {
        public string AckToken { get; set; } = string.Empty;
        public string ServerRevision { get; set; } = string.Empty;
        public long ServerSequence { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public Dictionary<string, string> ItemHashes { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> FileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
