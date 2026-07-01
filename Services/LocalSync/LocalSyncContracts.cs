namespace BueroCockpit.Services.LocalSync;

public sealed record LocalSyncStatus(
    string ServerName,
    string AppName,
    string? AppVersion,
    bool DataFolderAvailable,
    string? DataFolderDisplayName,
    bool PairingRequired,
    int PairedDeviceCount,
    DateTimeOffset ServerTimeUtc,
    string? Message = null);

public sealed record PairingRequest(
    string DeviceName,
    string DevicePlatform,
    string AppVersion,
    DateTimeOffset RequestedAtUtc);

public sealed record PairingConfirmation(
    string PairingCode,
    string DeviceId,
    string DeviceName,
    DateTimeOffset ConfirmedAtUtc);

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

public interface ILocalSyncContracts
{
    LocalSyncStatus GetStatus();

    PairingConfirmation ConfirmPairing(PairingRequest request, string pairingCode);

    MobileInboxUploadResult ValidateMobileInboxUpload(MobileInboxUploadManifest manifest);
}
