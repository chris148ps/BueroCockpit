using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BueroCockpit.Data;
using Microsoft.Data.Sqlite;

namespace BueroCockpit.Services;

public sealed class BackupExchangeService
{
    public const int CurrentBackupFormatVersion = 1;
    private const string ManifestFileName = "manifest.json";
    private static readonly HashSet<string> DeviceLocalRootFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "settings.local.json",
        "local-network-devices.json",
        "local-network-sync-state.json",
        "backup-exchange-state.local.json",
        "backup-exchange-journal.local.jsonl",
        "storage-location.json",
        "storage-location.local.json"
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dataDirectory;
    private readonly string _statePath;
    private readonly string _journalPath;
    private readonly Action? _afterCurrentDataMoved;

    public BackupExchangeService(
        string? dataDirectory = null,
        string? statePath = null,
        string? journalPath = null,
        Action? afterCurrentDataMoved = null)
    {
        _dataDirectory = Path.GetFullPath(dataDirectory ?? AppPaths.AppDataDirectory);
        _statePath = Path.GetFullPath(statePath ?? AppPaths.BackupExchangeStatePath);
        _journalPath = Path.GetFullPath(journalPath ?? AppPaths.BackupExchangeJournalPath);
        _afterCurrentDataMoved = afterCurrentDataMoved;
    }

    public BackupExchangeExportResult CreateExport(
        string exchangeDirectory,
        string deviceName,
        string appVersion)
    {
        EnsureExchangeDirectory(exchangeDirectory);
        EnsureDatabaseExists();

        var state = LoadState();
        var backupId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;
        var fileName = CreateExchangeFileName(createdAtUtc.ToLocalTime(), deviceName);
        var finalPath = Path.Combine(Path.GetFullPath(exchangeDirectory), fileName);
        var localArchivePath = Path.Combine(
            Path.GetTempPath(),
            $"buerocockpit-export-{Guid.NewGuid():N}.zip");

        try
        {
            var manifest = CreateArchive(
                localArchivePath,
                backupId,
                createdAtUtc,
                deviceName,
                appVersion,
                state.CurrentBackupId,
                state.CurrentDatabaseSha256);
            CopyArchiveAtomically(localArchivePath, finalPath);

            state.DeviceName = deviceName;
            state.LastExportedBackupId = manifest.BackupId;
            state.LastExportedDatabaseSha256 = manifest.DatabaseSha256;
            state.CurrentBackupId = manifest.BackupId;
            state.CurrentDatabaseSha256 = manifest.DatabaseSha256;
            state.CurrentBackupCreatedAtUtc = manifest.CreatedAtUtc;
            SaveState(state);
            AppendJournal("Export", manifest, finalPath, "Success");
            return new BackupExchangeExportResult(finalPath, manifest);
        }
        finally
        {
            TryDeleteFile(localArchivePath);
        }
    }

    public IReadOnlyList<BackupExchangeArchiveInfo> ListArchives(string exchangeDirectory)
    {
        EnsureExchangeDirectory(exchangeDirectory);

        return Directory.EnumerateFiles(exchangeDirectory, "BueroCockpit_Backup_*.zip")
            .Select(TryReadArchiveInfo)
            .OrderByDescending(item => item.Manifest?.CreatedAtUtc ?? File.GetLastWriteTimeUtc(item.ArchivePath))
            .ThenByDescending(item => item.ArchivePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public BackupImportAssessment AssessImport(BackupExchangeManifest manifest)
    {
        var state = LoadState();
        var warnings = new List<string>();
        var currentDatabaseHash = File.Exists(DatabasePath)
            ? ComputeCurrentDatabaseRevision()
            : string.Empty;

        if (state.LastImportedBackupId == manifest.BackupId ||
            state.CurrentBackupId == manifest.BackupId)
        {
            return new BackupImportAssessment(true, false, []);
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentDatabaseSha256) &&
            !string.Equals(currentDatabaseHash, state.CurrentDatabaseSha256, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Der lokale Datenstand wurde seit dem letzten Import oder Export verändert.");
        }

        if (state.CurrentBackupId is Guid currentBackupId &&
            manifest.ParentBackupId != currentBackupId)
        {
            warnings.Add("Das Backup stammt nicht direkt vom aktuellen lokalen Datenstand ab (ParentBackupId weicht ab).");
        }
        else if (state.CurrentBackupId is null && File.Exists(DatabasePath))
        {
            warnings.Add("Für den vorhandenen lokalen Datenstand ist noch keine eindeutige Backup-Abstammung gespeichert.");
        }

        if (state.CurrentBackupCreatedAtUtc is DateTimeOffset currentCreatedAt &&
            manifest.CreatedAtUtc < currentCreatedAt)
        {
            warnings.Add("Das ausgewählte Backup ist älter als der zuletzt bekannte lokale Datenstand.");
        }

        if (!string.IsNullOrWhiteSpace(state.DeviceName) &&
            string.Equals(state.DeviceName, manifest.DeviceName, StringComparison.OrdinalIgnoreCase) &&
            state.CurrentBackupCreatedAtUtc is DateTimeOffset sameDeviceCreatedAt &&
            manifest.CreatedAtUtc <= sameDeviceCreatedAt)
        {
            warnings.Add("Das Backup stammt von diesem Gerät und erscheint veraltet.");
        }

        return new BackupImportAssessment(false, warnings.Count > 0, warnings);
    }

    public BackupExchangeImportResult Import(
        string archivePath,
        string currentDeviceName,
        string appVersion,
        bool allowConflictingImport)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Das ausgewählte Austausch-Backup wurde nicht gefunden.", archivePath);
        }

        EnsureDatabaseExists();
        var parentDirectory = Directory.GetParent(_dataDirectory)?.FullName
            ?? throw new InvalidOperationException("Der lokale Datenordner besitzt keinen gültigen übergeordneten Ordner.");
        var extractedDirectory = Path.Combine(parentDirectory, $".buerocockpit-import-{Guid.NewGuid():N}");
        var previousDirectory = Path.Combine(parentDirectory, $".buerocockpit-previous-{Guid.NewGuid():N}");
        var failedDirectory = Path.Combine(parentDirectory, $".buerocockpit-failed-{Guid.NewGuid():N}");
        BackupExchangeManifest manifest;

        try
        {
            manifest = ExtractAndValidateArchive(archivePath, extractedDirectory);
            File.Delete(Path.Combine(extractedDirectory, ManifestFileName));
            var assessment = AssessImport(manifest);
            if (assessment.IsSameBackup)
            {
                return new BackupExchangeImportResult(false, true, string.Empty, manifest);
            }

            if (assessment.RequiresExplicitConfirmation && !allowConflictingImport)
            {
                throw new BackupExchangeConfirmationRequiredException(assessment);
            }

            var rollbackDirectory = Path.Combine(_dataDirectory, "Backups");
            Directory.CreateDirectory(rollbackDirectory);
            var rollbackPath = CreateRollbackArchive(
                rollbackDirectory,
                currentDeviceName,
                appVersion);

            CopyDirectoryIfExists(rollbackDirectory, Path.Combine(extractedDirectory, "Backups"));
            PreserveDeviceLocalFiles(extractedDirectory);

            SqliteConnection.ClearAllPools();
            Directory.Move(_dataDirectory, previousDirectory);
            try
            {
                _afterCurrentDataMoved?.Invoke();
                Directory.Move(extractedDirectory, _dataDirectory);
                SqliteConnection.ClearAllPools();

                var state = LoadState();
                state.DeviceName = currentDeviceName;
                state.LastImportedBackupId = manifest.BackupId;
                state.LastImportedDatabaseSha256 = manifest.DatabaseSha256;
                state.CurrentBackupId = manifest.BackupId;
                state.CurrentDatabaseSha256 = manifest.DatabaseSha256;
                state.CurrentBackupCreatedAtUtc = manifest.CreatedAtUtc;
                SaveState(state);
            }
            catch
            {
                if (Directory.Exists(_dataDirectory))
                {
                    Directory.Move(_dataDirectory, failedDirectory);
                }

                if (Directory.Exists(previousDirectory))
                {
                    Directory.Move(previousDirectory, _dataDirectory);
                }

                SqliteConnection.ClearAllPools();
                throw;
            }

            TryDeleteDirectory(previousDirectory);
            TryDeleteDirectory(failedDirectory);
            AppendJournal("Import", manifest, archivePath, "Success");
            return new BackupExchangeImportResult(true, false, rollbackPath, manifest);
        }
        catch (Exception ex)
        {
            AppendJournal("Import", null, archivePath, $"Failed: {ex.Message}");
            throw;
        }
        finally
        {
            TryDeleteDirectory(extractedDirectory);
            if (Directory.Exists(previousDirectory) && !Directory.Exists(_dataDirectory))
            {
                Directory.Move(previousDirectory, _dataDirectory);
            }
        }
    }

    public BackupExchangeManifest ValidateArchive(string archivePath)
    {
        var validationDirectory = Path.Combine(
            Path.GetTempPath(),
            $"buerocockpit-validate-{Guid.NewGuid():N}");
        try
        {
            return ExtractAndValidateArchive(archivePath, validationDirectory);
        }
        finally
        {
            TryDeleteDirectory(validationDirectory);
        }
    }

    private BackupExchangeManifest CreateArchive(
        string archivePath,
        Guid backupId,
        DateTimeOffset createdAtUtc,
        string deviceName,
        string appVersion,
        Guid? parentBackupId,
        string parentDatabaseSha256)
    {
        var stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"buerocockpit-archive-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var stagedDatabasePath = Path.Combine(stagingDirectory, "buerocockpit.db");
            BackupService.BackupDatabase(DatabasePath, stagedDatabasePath);
            CopyProductiveFiles(stagingDirectory);

            var settingsPath = Path.Combine(stagingDirectory, "settings.json");
            if (!File.Exists(settingsPath))
            {
                File.WriteAllText(settingsPath, "{}\n", Encoding.UTF8);
            }

            var databaseHash = ComputeSha256(stagedDatabasePath);
            var databaseSize = new FileInfo(stagedDatabasePath).Length;
            var manifest = new BackupExchangeManifest
            {
                BackupFormatVersion = CurrentBackupFormatVersion,
                BackupId = backupId,
                CreatedAtUtc = createdAtUtc,
                CreatedAtLocal = createdAtUtc.ToLocalTime(),
                DeviceName = NormalizeDeviceName(deviceName),
                OperatingSystem = GetOperatingSystemDescription(),
                AppVersion = appVersion,
                DatabaseFileName = "buerocockpit.db",
                DatabaseSize = databaseSize,
                DatabaseSha256 = databaseHash,
                SourceRevisionId = databaseHash,
                ParentBackupId = parentBackupId,
                ParentDatabaseSha256 = parentDatabaseSha256,
                DataSchemaVersion = ReadSchemaVersion(stagedDatabasePath),
                Files = CollectFileEntries(stagingDirectory)
            };

            File.WriteAllText(
                Path.Combine(stagingDirectory, ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions),
                Encoding.UTF8);
            ZipFile.CreateFromDirectory(stagingDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return manifest;
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private string CreateRollbackArchive(
        string rollbackDirectory,
        string deviceName,
        string appVersion)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var fileName = $"BueroCockpit_Rueckfall_{createdAtUtc.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{NormalizeFileName(deviceName)}.zip";
        var rollbackPath = Path.Combine(rollbackDirectory, fileName);
        var state = LoadState();
        CreateArchive(
            rollbackPath,
            Guid.NewGuid(),
            createdAtUtc,
            deviceName,
            appVersion,
            state.CurrentBackupId,
            state.CurrentDatabaseSha256);
        return rollbackPath;
    }

    private BackupExchangeManifest ExtractAndValidateArchive(string archivePath, string destinationDirectory)
    {
        if (!File.Exists(archivePath) || new FileInfo(archivePath).Length == 0)
        {
            throw new InvalidDataException("Das Austausch-Backup fehlt oder ist leer.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                if (!destinationPath.StartsWith(destinationRoot, PathComparison))
                {
                    throw new InvalidDataException("Das ZIP-Archiv enthält einen unzulässigen Dateipfad.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: false);
            }
        }

        var manifestPath = Path.Combine(destinationDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("Das ZIP-Archiv enthält keine manifest.json.");
        }

        var manifest = JsonSerializer.Deserialize<BackupExchangeManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions) ?? throw new InvalidDataException("Die manifest.json ist ungültig.");
        ValidateManifest(manifest, destinationDirectory);
        return manifest;
    }

    private void ValidateManifest(BackupExchangeManifest manifest, string directory)
    {
        if (manifest.BackupFormatVersion <= 0 ||
            manifest.BackupFormatVersion > CurrentBackupFormatVersion)
        {
            throw new InvalidDataException($"Backup-Format {manifest.BackupFormatVersion} wird von dieser App nicht unterstützt.");
        }

        if (manifest.BackupId == Guid.Empty || manifest.Files.Count == 0)
        {
            throw new InvalidDataException("Die manifest.json enthält keine vollständigen Backup-Metadaten.");
        }

        foreach (var file in manifest.Files)
        {
            var fullPath = GetSafeManifestFilePath(directory, file.Path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidDataException($"Die im Manifest aufgeführte Datei fehlt: {file.Path}");
            }

            var actualSize = new FileInfo(fullPath).Length;
            var actualHash = ComputeSha256(fullPath);
            if (actualSize != file.Size ||
                !string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Prüfsumme oder Größe stimmt nicht: {file.Path}");
            }
        }

        var actualFiles = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .Where(path => !string.Equals(path, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var manifestFiles = manifest.Files
            .Select(file => file.Path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        if (!actualFiles.SequenceEqual(manifestFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Dateiliste und Manifest stimmen nicht vollständig überein.");
        }

        var databasePath = GetSafeManifestFilePath(directory, manifest.DatabaseFileName);
        if (!File.Exists(databasePath))
        {
            throw new InvalidDataException("Die im Manifest angegebene SQLite-Datenbank fehlt.");
        }

        if (new FileInfo(databasePath).Length != manifest.DatabaseSize ||
            !string.Equals(ComputeSha256(databasePath), manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Die Datenbank stimmt nicht mit den Manifest-Metadaten überein.");
        }

        ValidateDatabase(databasePath, manifest.DataSchemaVersion);
    }

    private void ValidateDatabase(string databasePath, int? manifestSchemaVersion)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"SQLite integrity_check meldet: {result}");
            }

            command.CommandText = "PRAGMA user_version;";
            var schemaVersion = Convert.ToInt32(command.ExecuteScalar());
            if (manifestSchemaVersion is int expectedSchemaVersion && schemaVersion != expectedSchemaVersion)
            {
                throw new InvalidDataException("Die Schema-Version der Datenbank stimmt nicht mit dem Manifest überein.");
            }

            var currentSchemaVersion = File.Exists(DatabasePath)
                ? ReadSchemaVersion(DatabasePath)
                : schemaVersion;
            if (schemaVersion > currentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Die Datenbank-Schema-Version {schemaVersion} ist neuer als die unterstützte Version {currentSchemaVersion}.");
            }
        }
        catch (SqliteException ex)
        {
            throw new InvalidDataException("Die SQLite-Datenbank ist beschädigt oder nicht lesbar.", ex);
        }
    }

    private void CopyProductiveFiles(string destinationDirectory)
    {
        foreach (var filePath in Directory.EnumerateFiles(_dataDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_dataDirectory, filePath).Replace('\\', '/');
            if (ShouldExclude(relativePath) ||
                string.Equals(relativePath, "buerocockpit.db", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(filePath, destinationPath, overwrite: false);
        }
    }

    private static bool ShouldExclude(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment =>
                string.Equals(segment, "Backups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "Debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "Logs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "Testdaten", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var fileName = Path.GetFileName(normalized);
        return (segments.Length == 1 && DeviceLocalRootFileNames.Contains(fileName)) ||
               string.Equals(fileName, "buerocockpit.lock", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, ManifestFileName, StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith(".restore-", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<BackupExchangeFileEntry> CollectFileEntries(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => new BackupExchangeFileEntry
            {
                Path = Path.GetRelativePath(directory, path).Replace('\\', '/'),
                Size = new FileInfo(path).Length,
                Sha256 = ComputeSha256(path)
            })
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();
    }

    private void PreserveDeviceLocalFiles(string importedDataDirectory)
    {
        foreach (var fileName in DeviceLocalRootFileNames)
        {
            var sourcePath = Path.Combine(_dataDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            File.Copy(sourcePath, Path.Combine(importedDataDirectory, fileName), overwrite: true);
        }
    }

    private BackupExchangeArchiveInfo TryReadArchiveInfo(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var manifestEntry = archive.GetEntry(ManifestFileName)
                ?? throw new InvalidDataException("manifest.json fehlt.");
            using var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<BackupExchangeManifest>(reader.ReadToEnd(), JsonOptions)
                ?? throw new InvalidDataException("manifest.json ist ungültig.");
            return new BackupExchangeArchiveInfo(archivePath, manifest, string.Empty);
        }
        catch (Exception ex)
        {
            return new BackupExchangeArchiveInfo(archivePath, null, ex.Message);
        }
    }

    private BackupExchangeState LoadState()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return new BackupExchangeState();
            }

            return JsonSerializer.Deserialize<BackupExchangeState>(
                File.ReadAllText(_statePath),
                JsonOptions) ?? new BackupExchangeState();
        }
        catch
        {
            return new BackupExchangeState();
        }
    }

    private void SaveState(BackupExchangeState state)
    {
        var directory = Path.GetDirectoryName(_statePath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".backup-exchange-state-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions), Encoding.UTF8);
            File.Move(tempPath, _statePath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private void AppendJournal(
        string operation,
        BackupExchangeManifest? manifest,
        string archivePath,
        string result)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_journalPath)!);
            var entry = new BackupExchangeJournalEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Operation = operation,
                BackupId = manifest?.BackupId,
                ArchivePath = archivePath,
                Result = result
            };
            File.AppendAllText(
                _journalPath,
                JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Das lokale Journal darf einen abgeschlossenen Import/Export nicht nachträglich scheitern lassen.
        }
    }

    private static void CopyArchiveAtomically(string localArchivePath, string finalPath)
    {
        if (File.Exists(finalPath))
        {
            throw new IOException($"Im Austauschordner existiert bereits ein Archiv mit diesem Namen: {Path.GetFileName(finalPath)}");
        }

        var tempPath = Path.Combine(
            Path.GetDirectoryName(finalPath)!,
            $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var source = new FileStream(localArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
                target.Flush(flushToDisk: true);
            }

            File.Move(tempPath, finalPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void EnsureExchangeDirectory(string exchangeDirectory)
    {
        if (string.IsNullOrWhiteSpace(exchangeDirectory) || !Directory.Exists(exchangeDirectory))
        {
            throw new DirectoryNotFoundException(
                "Der konfigurierte Backup-Austauschordner wurde nicht gefunden. Bitte den Ordner in den Einstellungen neu auswählen.");
        }
    }

    private void EnsureDatabaseExists()
    {
        if (!File.Exists(DatabasePath))
        {
            throw new FileNotFoundException(
                "Im lokalen Datenordner wurde keine produktive BüroCockpit-Datenbank gefunden. Bitte zuerst ein Backup manuell einspielen.",
                DatabasePath);
        }
    }

    private string DatabasePath => Path.Combine(_dataDirectory, "buerocockpit.db");

    private string ComputeCurrentDatabaseRevision()
    {
        var temporaryDatabasePath = Path.Combine(
            Path.GetTempPath(),
            $"buerocockpit-revision-{Guid.NewGuid():N}.db");
        try
        {
            BackupService.BackupDatabase(DatabasePath, temporaryDatabasePath);
            return ComputeSha256(temporaryDatabasePath);
        }
        finally
        {
            TryDeleteFile(temporaryDatabasePath);
        }
    }

    private static string CreateExchangeFileName(DateTimeOffset localTimestamp, string deviceName)
    {
        return $"BueroCockpit_Backup_{localTimestamp:yyyy-MM-dd_HH-mm-ss}_{NormalizeFileName(deviceName)}.zip";
    }

    private static string NormalizeDeviceName(string deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName) ? "Unbekanntes-Geraet" : deviceName.Trim();
    }

    private static string NormalizeFileName(string value)
    {
        var normalized = NormalizeDeviceName(value);
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalidCharacter, '-');
        }

        return string.Join('-', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetOperatingSystemDescription()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        return Environment.OSVersion.Platform.ToString();
    }

    private static int ReadSchemaVersion(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string GetSafeManifestFilePath(string directory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Das Manifest enthält einen ungültigen Dateipfad.");
        }

        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, PathComparison))
        {
            throw new InvalidDataException("Das Manifest enthält einen unzulässigen Dateipfad.");
        }

        return fullPath;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void CopyDirectoryIfExists(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: false);
        }
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
            // Nur Aufräumen temporärer Dateien.
        }
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
            // Nur Aufräumen temporärer Verzeichnisse.
        }
    }
}

public sealed class BackupExchangeManifest
{
    public int BackupFormatVersion { get; set; }
    public Guid BackupId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset CreatedAtLocal { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string DatabaseFileName { get; set; } = string.Empty;
    public long DatabaseSize { get; set; }
    public string DatabaseSha256 { get; set; } = string.Empty;
    public string SourceRevisionId { get; set; } = string.Empty;
    public Guid? ParentBackupId { get; set; }
    public string ParentDatabaseSha256 { get; set; } = string.Empty;
    public int? DataSchemaVersion { get; set; }
    public IReadOnlyList<BackupExchangeFileEntry> Files { get; set; } = [];
}

public sealed class BackupExchangeFileEntry
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class BackupExchangeState
{
    public string DeviceName { get; set; } = string.Empty;
    public Guid? LastImportedBackupId { get; set; }
    public string LastImportedDatabaseSha256 { get; set; } = string.Empty;
    public Guid? LastExportedBackupId { get; set; }
    public string LastExportedDatabaseSha256 { get; set; } = string.Empty;
    public Guid? CurrentBackupId { get; set; }
    public string CurrentDatabaseSha256 { get; set; } = string.Empty;
    public DateTimeOffset? CurrentBackupCreatedAtUtc { get; set; }
}

public sealed class BackupExchangeJournalEntry
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid? BackupId { get; set; }
    public string ArchivePath { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public sealed record BackupExchangeExportResult(
    string ArchivePath,
    BackupExchangeManifest Manifest);

public sealed record BackupExchangeImportResult(
    bool Imported,
    bool NoChange,
    string RollbackArchivePath,
    BackupExchangeManifest Manifest);

public sealed record BackupExchangeArchiveInfo(
    string ArchivePath,
    BackupExchangeManifest? Manifest,
    string Error);

public sealed record BackupImportAssessment(
    bool IsSameBackup,
    bool RequiresExplicitConfirmation,
    IReadOnlyList<string> Warnings);

public sealed class BackupExchangeConfirmationRequiredException : InvalidOperationException
{
    public BackupExchangeConfirmationRequiredException(BackupImportAssessment assessment)
        : base("Dieser Datenstand stammt nicht direkt von Ihrem aktuellen Datenstand ab. Beim Import werden lokale Änderungen vollständig ersetzt. Es findet keine Zusammenführung statt.")
    {
        Assessment = assessment;
    }

    public BackupImportAssessment Assessment { get; }
}
