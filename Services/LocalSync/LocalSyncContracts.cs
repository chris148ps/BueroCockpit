using System.Text.Json;
using System.Text.Json.Serialization;

namespace BueroCockpit.Services.LocalSync;

public sealed record LocalSyncStatus(
    string ServerName,
    string AppName,
    string? AppVersion,
    bool DataFolderAvailable,
    string? DataFolderDisplayName,
    DateTimeOffset ServerTimeUtc,
    string? Message = null);

public sealed record MobileInboxUploadManifest(
    string UploadId,
    string DeviceId,
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<MobileInboxFileEntry> Files,
    string? EntryTitle = null,
    string? CustomerName = null,
    string? Notes = null);

public sealed record MobileInboxUploadRequest(
    string UploadId,
    string DeviceId,
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<MobileInboxUploadFile> Files,
    long? ClientSequence = null);

public sealed record MobileInboxUploadFile(
    string RelativePath,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Purpose,
    byte[] Data);

public sealed record MobileInboxTransferResult(
    string Status,
    string UploadId,
    string? InboxEntryId,
    DateTimeOffset ReceivedAtUtc,
    int TransferredObjects,
    int TransferredPhotos,
    int TransferredFiles,
    int SkippedObjects,
    int SkippedFiles,
    int FailedObjects,
    IReadOnlyList<string> Messages)
{
    public bool IsSuccess => Status is "accepted" or "skipped";
}

public sealed record LocalSyncPairingStatus(
    string App,
    string Status,
    string DeviceId,
    string? DeviceName,
    string Message);

public sealed record LocalSyncUploadCompletedEventArgs(
    string DeviceId,
    string DeviceName,
    MobileInboxTransferResult Result);

public sealed record MobileInboxFileEntry(
    string RelativePath,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Purpose);

public sealed record MobileInboxUploadResult(
    bool Accepted,
    string UploadId,
    string? InboxEntryId,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<string> Messages);

public sealed record LocalSyncSnapshotManifest(
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ServerName,
    string State,
    bool ContainsProductiveData,
    IReadOnlyList<LocalSyncSnapshotManifestEntry> Entries,
    string? Message = null);

public sealed record LocalSyncSnapshotManifestEntry(
    string RelativePath,
    string ContentType,
    long SizeBytes,
    string Purpose);

public sealed record LocalSyncChangeStatus(
    [property: JsonPropertyName("app")]
    string App,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("mode")]
    string Mode,
    [property: JsonPropertyName("changeVersion")]
    string ChangeVersion,
    [property: JsonPropertyName("lastChangedUtc")]
    DateTimeOffset LastChangedUtc,
    [property: JsonPropertyName("syncActive")]
    bool SyncActive);

public sealed record LocalSyncSnapshotPackage(
    string FilePath,
    string FileName,
    string ChangeVersion,
    DateTimeOffset CreatedAtUtc,
    string? CleanupDirectoryPath = null);

public sealed record LocalSyncDeltaFile(
    string RelativePath,
    long SizeBytes,
    string Sha256,
    string DataBase64);

public sealed record LocalSyncDeltaTombstones(
    IReadOnlyList<string> TaskIds,
    IReadOnlyList<string> CategoryIds,
    IReadOnlyList<string> TechnicianIds,
    IReadOnlyList<string> AttachmentIds)
{
    public int TotalCount => TaskIds.Count + CategoryIds.Count + TechnicianIds.Count + AttachmentIds.Count;
}

public sealed record LocalSyncDeltaCounts(
    int Tasks,
    int Categories,
    int Technicians,
    int Attachments,
    int Files,
    int Tombstones,
    int UnchangedObjects,
    int UnchangedFiles);

public sealed record LocalSyncDeltaResponse(
    string ApiVersion,
    bool RequiresFullSync,
    string? FromRevision,
    string ToRevision,
    long ServerSequence,
    long LastConfirmedClientSequence,
    string? AckToken,
    IReadOnlyList<JsonElement> Tasks,
    IReadOnlyList<JsonElement> Categories,
    IReadOnlyList<JsonElement> Technicians,
    IReadOnlyList<JsonElement> Attachments,
    LocalSyncDeltaTombstones Tombstones,
    IReadOnlyList<LocalSyncDeltaFile> Files,
    LocalSyncDeltaCounts Counts)
{
    public static LocalSyncDeltaResponse RequiresFirstSync(string revision, long sequence) =>
        new(
            LocalSyncDeltaStore.ApiVersion,
            true,
            null,
            revision,
            sequence,
            0,
            null,
            [],
            [],
            [],
            [],
            new LocalSyncDeltaTombstones([], [], [], []),
            [],
            new LocalSyncDeltaCounts(0, 0, 0, 0, 0, 0, 0, 0));

    public static LocalSyncDeltaResponse NoChanges(
        string revision,
        long sequence,
        long clientSequence,
        int unchangedObjects,
        int unchangedFiles) =>
        new(
            LocalSyncDeltaStore.ApiVersion,
            false,
            revision,
            revision,
            sequence,
            clientSequence,
            null,
            [],
            [],
            [],
            [],
            new LocalSyncDeltaTombstones([], [], [], []),
            [],
            new LocalSyncDeltaCounts(0, 0, 0, 0, 0, 0, unchangedObjects, unchangedFiles));
}

public sealed record LocalSyncPreparedCheckpoint(
    string AckToken,
    string ServerRevision,
    long ServerSequence);

public sealed record LocalSyncAckRequest(
    string AckToken,
    string ServerRevision,
    long? LastConfirmedClientSequence = null);

public sealed record LocalSyncAckResponse(
    string ApiVersion,
    string Status,
    string ServerRevision,
    long ServerSequence,
    long LastConfirmedClientSequence,
    string Message);

public interface ILocalSyncContracts
{
    LocalSyncStatus GetStatus();

    LocalSyncChangeStatus GetChangeStatus();

    MobileInboxUploadResult ValidateMobileInboxUpload(MobileInboxUploadManifest manifest);
}
