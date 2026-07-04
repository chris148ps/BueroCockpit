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

public interface ILocalSyncContracts
{
    LocalSyncStatus GetStatus();

    MobileInboxUploadResult ValidateMobileInboxUpload(MobileInboxUploadManifest manifest);
}
