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
    IReadOnlyList<MobileInboxUploadFile> Files);

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

public interface ILocalSyncContracts
{
    LocalSyncStatus GetStatus();

    LocalSyncChangeStatus GetChangeStatus();

    MobileInboxUploadResult ValidateMobileInboxUpload(MobileInboxUploadManifest manifest);
}
