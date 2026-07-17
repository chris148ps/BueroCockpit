using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BueroCockpit.Services.LocalSync;

public sealed class LocalSyncInboxStore
{
    public const int MaximumFileCount = 250;
    public const long MaximumFileSizeBytes = 100L * 1024 * 1024;
    public const long MaximumPackageSizeBytes = 220L * 1024 * 1024;

    private static readonly Regex StableIdPattern = new("^[A-Za-z0-9._-]{1,128}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    private readonly object _gate = new();
    private readonly string _syncDirectory;

    public LocalSyncInboxStore(string dataFolderPath)
    {
        _syncDirectory = Path.Combine(dataFolderPath, "Sync");
    }

    public MobileInboxTransferResult Accept(MobileInboxUploadRequest request)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var validationMessages = Validate(request);
            if (validationMessages.Count > 0)
            {
                return Failed(request.UploadId, now, "invalid", validationMessages);
            }

            var safeUploadId = request.UploadId.Trim();
            var fingerprint = BuildFingerprint(request.Files);
            var receipt = LoadReceipt(safeUploadId);
            if (receipt is not null)
            {
                if (string.Equals(receipt.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    var skipped = CreateResult(
                        "skipped",
                        safeUploadId,
                        receipt.InboxEntryId,
                        now,
                        request.Files,
                        skippedObjects: 1,
                        skippedFiles: request.Files.Count,
                        messages: ["Mobiler Eingang war bereits vollständig vorhanden und wurde nicht dupliziert."]);
                    AppendLog(request.DeviceId, skipped);
                    return skipped;
                }

                return StoreConflict(request, fingerprint, now, "Dieselbe stabile Objekt-ID wurde mit geändertem Inhalt erneut übertragen.");
            }

            var inboxDirectory = Path.Combine(_syncDirectory, "inbox");
            Directory.CreateDirectory(inboxDirectory);
            var stagingDirectory = Path.Combine(inboxDirectory, $".staging-{safeUploadId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                WritePackage(stagingDirectory, request, fingerprint);
                var inboxEntryId = safeUploadId.StartsWith("mobile-", StringComparison.OrdinalIgnoreCase)
                    ? safeUploadId
                    : $"mobile-{safeUploadId}";
                var destinationDirectory = Path.Combine(inboxDirectory, inboxEntryId);
                if (Directory.Exists(destinationDirectory))
                {
                    var existingFingerprint = LoadPackageFingerprint(destinationDirectory);
                    if (string.Equals(existingFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteDirectory(stagingDirectory);
                        SaveReceipt(new LocalSyncReceipt(safeUploadId, fingerprint, inboxEntryId, now));
                        var recovered = CreateResult(
                            "skipped",
                            safeUploadId,
                            inboxEntryId,
                            now,
                            request.Files,
                            skippedObjects: 1,
                            skippedFiles: request.Files.Count,
                            messages: ["Vollständig abgelegtes Paket wurde nach fehlender Bestätigung wiedererkannt und nicht dupliziert."]);
                        AppendLog(request.DeviceId, recovered);
                        return recovered;
                    }
                    return StoreConflictFromStaging(
                        request,
                        fingerprint,
                        now,
                        stagingDirectory,
                        "Der Zielordner existiert bereits ohne passenden Übertragungsbeleg.");
                }

                Directory.Move(stagingDirectory, destinationDirectory);
                SaveReceipt(new LocalSyncReceipt(safeUploadId, fingerprint, inboxEntryId, now));
                var result = CreateResult(
                    "accepted",
                    safeUploadId,
                    inboxEntryId,
                    now,
                    request.Files,
                    skippedObjects: 0,
                    skippedFiles: 0,
                    messages: ["Mobiler Eingang wurde vollständig geprüft und in der Desktop-Inbox bereitgestellt."]);
                AppendLog(request.DeviceId, result);
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
            {
                TryDeleteDirectory(stagingDirectory);
                var result = Failed(safeUploadId, now, "failed", ["Upload konnte nicht vollständig gespeichert werden.", ex.Message]);
                AppendLog(request.DeviceId, result);
                return result;
            }
        }
    }

    private static List<string> Validate(MobileInboxUploadRequest request)
    {
        var messages = new List<string>();
        if (!StableIdPattern.IsMatch(request.UploadId?.Trim() ?? string.Empty))
        {
            messages.Add("Upload-ID fehlt oder enthält unzulässige Zeichen.");
        }
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            messages.Add("Geräte-ID fehlt.");
        }
        if (!string.Equals(request.SchemaVersion, "local-sync-inbox-v1", StringComparison.Ordinal))
        {
            messages.Add("Nicht unterstützte Upload-Schemaversion.");
        }
        if (request.Files is null || request.Files.Count == 0 || request.Files.Count > MaximumFileCount)
        {
            messages.Add($"Dateianzahl ist ungültig; höchstens {MaximumFileCount} Dateien sind erlaubt.");
            return messages;
        }

        var totalSize = 0L;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in request.Files)
        {
            var relativePath = NormalizeRelativePath(file.RelativePath);
            if (relativePath is null || !IsAllowedRelativePath(relativePath))
            {
                messages.Add($"Unzulässiger relativer Pfad: {file.RelativePath}");
                continue;
            }
            if (!paths.Add(relativePath))
            {
                messages.Add($"Doppelter Dateipfad: {relativePath}");
            }
            if (file.Data is null || file.SizeBytes <= 0 || file.SizeBytes != file.Data.LongLength)
            {
                messages.Add($"Dateigröße stimmt nicht: {relativePath}");
                continue;
            }
            if (file.SizeBytes > MaximumFileSizeBytes || totalSize > MaximumPackageSizeBytes - file.SizeBytes)
            {
                messages.Add($"Datei oder Gesamtpaket ist zu groß: {relativePath}");
                continue;
            }
            totalSize += file.SizeBytes;
            var actualHash = Convert.ToHexString(SHA256.HashData(file.Data)).ToLowerInvariant();
            if (!string.Equals(actualHash, file.Sha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                messages.Add($"Prüfsumme stimmt nicht: {relativePath}");
            }
            if (!HasExpectedFileSignature(relativePath, file.Data))
            {
                messages.Add($"Dateityp und Dateiinhalt passen nicht zusammen: {relativePath}");
            }
        }

        var taskFile = request.Files.FirstOrDefault(file =>
            string.Equals(NormalizeRelativePath(file.RelativePath), "aufgabe.json", StringComparison.OrdinalIgnoreCase));
        if (taskFile is null)
        {
            messages.Add("aufgabe.json fehlt.");
        }
        else
        {
            try
            {
                using var document = JsonDocument.Parse(taskFile.Data);
                var root = document.RootElement;
                var taskId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (!string.Equals(taskId?.Trim(), (request.UploadId ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add("Stabile Objekt-ID in aufgabe.json stimmt nicht mit der Upload-ID überein.");
                }
            }
            catch (JsonException)
            {
                messages.Add("aufgabe.json ist ungültig.");
            }
        }

        return messages;
    }

    private void WritePackage(string stagingDirectory, MobileInboxUploadRequest request, string fingerprint)
    {
        foreach (var file in request.Files)
        {
            var relativePath = NormalizeRelativePath(file.RelativePath)
                ?? throw new IOException("Ungültiger relativer Pfad.");
            var destinationPath = Path.GetFullPath(Path.Combine(stagingDirectory, relativePath));
            var stagingRoot = Path.GetFullPath(stagingDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(stagingRoot, StringComparison.Ordinal))
            {
                throw new IOException("Dateipfad verlässt den Staging-Ordner.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var stream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(file.Data);
            stream.Flush(flushToDisk: true);
        }

        var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
        var manifest = new
        {
            schemaVersion = request.SchemaVersion,
            request.UploadId,
            request.DeviceId,
            request.CreatedAtUtc,
            fingerprint,
            files = request.Files.Select(file => new
            {
                relativePath = NormalizeRelativePath(file.RelativePath),
                file.ContentType,
                file.SizeBytes,
                file.Sha256,
                file.Purpose
            })
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private MobileInboxTransferResult StoreConflict(
        MobileInboxUploadRequest request,
        string fingerprint,
        DateTimeOffset now,
        string message)
    {
        var inboxDirectory = Path.Combine(_syncDirectory, "inbox");
        Directory.CreateDirectory(inboxDirectory);
        var stagingDirectory = Path.Combine(inboxDirectory, $".staging-{request.UploadId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            WritePackage(stagingDirectory, request, fingerprint);
            return StoreConflictFromStaging(request, fingerprint, now, stagingDirectory, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDeleteDirectory(stagingDirectory);
            var result = Failed(request.UploadId, now, "failed", [message, ex.Message]);
            AppendLog(request.DeviceId, result);
            return result;
        }
    }

    private MobileInboxTransferResult StoreConflictFromStaging(
        MobileInboxUploadRequest request,
        string fingerprint,
        DateTimeOffset now,
        string stagingDirectory,
        string message)
    {
        var conflictsDirectory = Path.Combine(_syncDirectory, "conflicts");
        Directory.CreateDirectory(conflictsDirectory);
        var conflictDirectory = Path.Combine(
            conflictsDirectory,
            $"mobile-{request.UploadId}-conflict-{now:yyyyMMddHHmmss}-{fingerprint[..8]}");
        Directory.Move(stagingDirectory, conflictDirectory);
        var result = Failed(request.UploadId, now, "conflict", [message, "Mobile Daten wurden im Konfliktordner erhalten und nicht überschrieben."]);
        AppendLog(request.DeviceId, result);
        return result;
    }

    private static MobileInboxTransferResult CreateResult(
        string status,
        string uploadId,
        string? inboxEntryId,
        DateTimeOffset now,
        IReadOnlyList<MobileInboxUploadFile> files,
        int skippedObjects,
        int skippedFiles,
        IReadOnlyList<string> messages)
    {
        var photoCount = files.Count(file =>
            string.Equals(file.Purpose, "original-photo", StringComparison.OrdinalIgnoreCase));
        var transferredFiles = status == "accepted" ? files.Count : 0;
        return new MobileInboxTransferResult(
            status,
            uploadId,
            inboxEntryId,
            now,
            status == "accepted" ? 1 : 0,
            status == "accepted" ? photoCount : 0,
            transferredFiles,
            skippedObjects,
            skippedFiles,
            0,
            messages);
    }

    private static MobileInboxTransferResult Failed(
        string? uploadId,
        DateTimeOffset now,
        string status,
        IReadOnlyList<string> messages) =>
        new(status, uploadId ?? string.Empty, null, now, 0, 0, 0, 0, 0, 1, messages);

    private string? GetReceiptPath(string uploadId)
    {
        if (!StableIdPattern.IsMatch(uploadId))
        {
            return null;
        }
        return Path.Combine(_syncDirectory, "receipts", $"{uploadId}.json");
    }

    private LocalSyncReceipt? LoadReceipt(string uploadId)
    {
        var receiptPath = GetReceiptPath(uploadId);
        if (receiptPath is null || !File.Exists(receiptPath))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<LocalSyncReceipt>(File.ReadAllText(receiptPath), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void SaveReceipt(LocalSyncReceipt receipt)
    {
        var receiptPath = GetReceiptPath(receipt.UploadId)
            ?? throw new IOException("Ungültige Beleg-ID.");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        var temporaryPath = $"{receiptPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(receipt, JsonOptions));
        File.Move(temporaryPath, receiptPath, overwrite: true);
    }

    private static string? LoadPackageFingerprint(string packageDirectory)
    {
        try
        {
            var manifestPath = Path.Combine(packageDirectory, "manifest.json");
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("fingerprint", out var fingerprintElement)
                ? fingerprintElement.GetString()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void AppendLog(string deviceId, MobileInboxTransferResult result)
    {
        try
        {
            Directory.CreateDirectory(_syncDirectory);
            var logPath = Path.Combine(_syncDirectory, "sync-log.jsonl");
            var line = JsonSerializer.Serialize(new
            {
                timestampUtc = result.ReceivedAtUtc,
                deviceId,
                result.UploadId,
                result.Status,
                result.TransferredObjects,
                result.TransferredPhotos,
                result.SkippedObjects,
                result.FailedObjects,
                message = result.Messages.FirstOrDefault()
            }, JsonOptions);
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein fehlendes Protokoll darf nicht als bestaetigte erfolgreiche Uebertragung erscheinen.
            throw new IOException("Sync-Protokoll konnte nicht geschrieben werden.", ex);
        }
    }

    private static string BuildFingerprint(IEnumerable<MobileInboxUploadFile> files)
    {
        var canonical = string.Join("\n", files
            .Select(file => $"{NormalizeRelativePath(file.RelativePath)}|{file.SizeBytes}|{file.Sha256.Trim().ToLowerInvariant()}")
            .OrderBy(value => value, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string? NormalizeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return null;
        }
        var normalized = path.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Any(char.IsControl))
            ? null
            : string.Join('/', segments);
    }

    private static bool IsAllowedRelativePath(string path)
    {
        if (string.Equals(path, "aufgabe.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var firstSegment = path.Split('/')[0];
        return firstSegment is "originals" or "previews" or "annotated" or "sketches" or "files";
    }

    private static bool HasExpectedFileSignature(string path, byte[] data)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
            ".png" => data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => data.Length >= 12 && Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && Encoding.ASCII.GetString(data, 8, 4) == "WEBP",
            ".heic" or ".heif" => data.Length >= 12 && Encoding.ASCII.GetString(data, 4, 4) == "ftyp",
            ".json" => data.Length > 1 && (data[0] == (byte)'{' || data[0] == (byte)'['),
            _ => data.Length > 0
        };
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
        }
    }

    private sealed record LocalSyncReceipt(
        string UploadId,
        string Fingerprint,
        string InboxEntryId,
        DateTimeOffset ReceivedAtUtc);
}
