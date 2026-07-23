using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BueroCockpit.Data;
using BueroCockpit.Services;
using Microsoft.Data.Sqlite;

var testRoot = Path.Combine(Path.GetTempPath(), $"buerocockpit-backup-exchange-tests-{Guid.NewGuid():N}");
var globalData = Path.Combine(testRoot, "global-data");
var globalLocal = Path.Combine(testRoot, "global-local");
Environment.SetEnvironmentVariable("BUEROCOCKPIT_DATA_DIRECTORY", globalData);
Environment.SetEnvironmentVariable("BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY", globalLocal);

try
{
    Directory.CreateDirectory(testRoot);
    CreateDatabase(globalData, "global");

    Run("Windows nutzt LOCALAPPDATA und nicht OneDrive", TestWindowsLocalPath);
    Run("macOS nutzt Application Support und nicht OneDrive", TestMacLocalPath);
    Run("Umgeleiteter produktiver Datenordner wird blockiert", TestRedirectedDataDirectory);
    Run("Export enthält Manifest, Hashes und Produktivdateien", TestExport);
    Run("Import ersetzt lokal und erzeugt Rückfall-Backup", TestImportAndRollbackBackup);
    Run("Manipuliertes Backup wird vor Änderung abgelehnt", TestTamperedArchive);
    Run("Beschädigte SQLite-Datenbank wird vor Änderung abgelehnt", TestCorruptDatabase);
    Run("Fehlender Austauschordner ändert keine Daten", TestMissingExchangeDirectory);
    Run("Veraltetes Backup erzeugt Warnung", TestOutdatedWarning);
    Run("Abweichende ParentBackupId erzeugt Warnung", TestParentWarning);
    Run("PC-Mac-PC-Roundtrip übernimmt Änderungen vollständig", TestRoundtrip);
    Run("Erneuter Import desselben Backups ist idempotent", TestIdempotency);
    Run("Fehler während Aktivierung stellt vorherigen Stand wieder her", TestActivationRollback);
    Console.WriteLine("Alle Backup-Austauschtests erfolgreich.");
    return 0;
}
finally
{
    try
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
    catch
    {
        // Testartefakte im temporären Systemordner beeinflussen das Ergebnis nicht.
    }
}

void TestWindowsLocalPath()
{
    var path = AppPaths.GetDefaultAppDataDirectoryForPlatform(
        true,
        @"C:\Users\Test\AppData\Local",
        @"C:\Users\Test\AppData\Roaming");
    Equal(@"C:\Users\Test\AppData\Local/BueroCockpit".Replace('/', Path.DirectorySeparatorChar), path);
    False(path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase), "Windows-Datenpfad enthält OneDrive.");
}

void TestMacLocalPath()
{
    var path = AppPaths.GetDefaultAppDataDirectoryForPlatform(
        false,
        "/Users/test/Library/Application Support",
        "/Users/test/Library/Application Support");
    Equal("/Users/test/Library/Application Support/BueroCockpit", path);
    False(path.Contains("CloudStorage", StringComparison.OrdinalIgnoreCase), "macOS-Datenpfad enthält CloudStorage.");
    False(path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase), "macOS-Datenpfad enthält OneDrive.");
}

void TestRedirectedDataDirectory()
{
    var root = Path.Combine(testRoot, $"redirect-{Guid.NewGuid():N}");
    var target = Path.Combine(root, "cloud-target");
    var link = Path.Combine(root, "BueroCockpit");
    Directory.CreateDirectory(target);
    Directory.CreateSymbolicLink(link, target);
    Throws<LocalDataDirectoryRedirectException>(() =>
        StorageLocationService.EnsureProductiveDirectoryIsLocal(link));
}

void TestExport()
{
    using var fixture = CreateFixture("export");
    var result = fixture.SourceService.CreateExport(fixture.Exchange, "Firmen PC", "1.2.3");
    True(File.Exists(result.ArchivePath), "Export-ZIP fehlt.");
    Equal(BackupExchangeService.CurrentBackupFormatVersion, result.Manifest.BackupFormatVersion);
    Equal("Firmen PC", result.Manifest.DeviceName);
    True(result.Manifest.Files.Any(file => file.Path == "buerocockpit.db"), "Datenbank fehlt im Manifest.");
    True(result.Manifest.Files.Any(file => file.Path == "settings.json"), "settings.json fehlt im Manifest.");
    True(result.Manifest.Files.Any(file => file.Path == "Tasks/task-1/attachment.txt"), "Anlage fehlt im Manifest.");
    False(result.Manifest.Files.Any(file => file.Path.Contains("Backups/", StringComparison.OrdinalIgnoreCase)), "Altes Backup wurde exportiert.");
    False(result.Manifest.Files.Any(file => file.Path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)), "Temporäre Datei wurde exportiert.");
    False(result.Manifest.Files.Any(file => file.Path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)), "Lock-Datei wurde exportiert.");
    False(result.Manifest.Files.Any(file => file.Path == "settings.local.json"), "Gerätelokale Einstellungen wurden exportiert.");
    False(result.Manifest.Files.Any(file => file.Path == "backup-exchange-state.local.json"), "Gerätelokaler Austauschzustand wurde exportiert.");
    var validated = fixture.SourceService.ValidateArchive(result.ArchivePath);
    Equal(result.Manifest.DatabaseSha256, validated.DatabaseSha256);
}

void TestImportAndRollbackBackup()
{
    using var fixture = CreateFixture("import");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var result = fixture.TargetService.Import(export.ArchivePath, "Mac", "1.0", allowConflictingImport: true);
    True(result.Imported, "Import wurde nicht ausgeführt.");
    Equal("source", ReadMarker(fixture.TargetData));
    Equal("target-local-setting", File.ReadAllText(Path.Combine(fixture.TargetData, "settings.local.json")));
    True(File.Exists(result.RollbackArchivePath), "Rückfall-Backup fehlt.");
    var rollbackManifest = fixture.TargetService.ValidateArchive(result.RollbackArchivePath);
    True(rollbackManifest.Files.Any(file => file.Path == "buerocockpit.db"), "Rückfall-Backup enthält keine Datenbank.");
}

void TestTamperedArchive()
{
    using var fixture = CreateFixture("tampered");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var tamperedPath = Path.Combine(fixture.Exchange, "BueroCockpit_Backup_2099-01-01_00-00-00_Tampered.zip");
    File.Copy(export.ArchivePath, tamperedPath);
    using (var archive = ZipFile.Open(tamperedPath, ZipArchiveMode.Update))
    {
        var entry = archive.GetEntry("Tasks/task-1/attachment.txt")!;
        entry.Delete();
        var replacement = archive.CreateEntry("Tasks/task-1/attachment.txt");
        using var writer = new StreamWriter(replacement.Open());
        writer.Write("manipuliert");
    }

    Throws<InvalidDataException>(() => fixture.TargetService.Import(tamperedPath, "Mac", "1.0", true));
    Equal("target", ReadMarker(fixture.TargetData));
}

void TestCorruptDatabase()
{
    using var fixture = CreateFixture("corrupt-db");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var corruptPath = Path.Combine(fixture.Exchange, "BueroCockpit_Backup_2099-01-01_00-00-01_Corrupt.zip");
    RewriteArchiveWithCorruptDatabase(export.ArchivePath, corruptPath);
    Throws<InvalidDataException>(() => fixture.TargetService.Import(corruptPath, "Mac", "1.0", true));
    Equal("target", ReadMarker(fixture.TargetData));
}

void TestMissingExchangeDirectory()
{
    using var fixture = CreateFixture("missing");
    var before = ReadMarker(fixture.SourceData);
    Throws<DirectoryNotFoundException>(() => fixture.SourceService.CreateExport(
        Path.Combine(fixture.Root, "nicht-vorhanden"),
        "PC",
        "1.0"));
    Equal(before, ReadMarker(fixture.SourceData));
}

void TestOutdatedWarning()
{
    using var fixture = CreateFixture("outdated");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var state = new BackupExchangeState
    {
        DeviceName = "Mac",
        CurrentBackupId = Guid.NewGuid(),
        CurrentDatabaseSha256 = HashFile(Path.Combine(fixture.TargetData, "buerocockpit.db")),
        CurrentBackupCreatedAtUtc = export.Manifest.CreatedAtUtc.AddHours(1)
    };
    Directory.CreateDirectory(Path.GetDirectoryName(fixture.TargetState)!);
    File.WriteAllText(fixture.TargetState, JsonSerializer.Serialize(state));
    var assessment = fixture.TargetService.AssessImport(export.Manifest);
    True(assessment.RequiresExplicitConfirmation, "Warnung für veraltetes Backup fehlt.");
    True(assessment.Warnings.Any(warning => warning.Contains("älter", StringComparison.OrdinalIgnoreCase)), "Alterswarnung fehlt.");
}

void TestParentWarning()
{
    using var fixture = CreateFixture("parent");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var state = new BackupExchangeState
    {
        DeviceName = "Mac",
        CurrentBackupId = Guid.NewGuid(),
        CurrentDatabaseSha256 = HashFile(Path.Combine(fixture.TargetData, "buerocockpit.db")),
        CurrentBackupCreatedAtUtc = export.Manifest.CreatedAtUtc
    };
    Directory.CreateDirectory(Path.GetDirectoryName(fixture.TargetState)!);
    File.WriteAllText(fixture.TargetState, JsonSerializer.Serialize(state));
    var assessment = fixture.TargetService.AssessImport(export.Manifest);
    True(assessment.RequiresExplicitConfirmation, "Parent-Abweichung wurde nicht erkannt.");
    True(assessment.Warnings.Any(warning => warning.Contains("ParentBackupId", StringComparison.OrdinalIgnoreCase)), "ParentBackupId-Warnung fehlt.");
}

void TestRoundtrip()
{
    using var fixture = CreateFixture("roundtrip");
    var pcExport = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    fixture.TargetService.Import(pcExport.ArchivePath, "Mac", "1.0", true);
    WriteMarker(fixture.TargetData, "mac-change");
    var macExport = fixture.TargetService.CreateExport(fixture.Exchange, "Mac", "1.0");
    Equal(pcExport.Manifest.BackupId, macExport.Manifest.ParentBackupId);
    var pcAssessment = fixture.SourceService.AssessImport(macExport.Manifest);
    False(pcAssessment.RequiresExplicitConfirmation, "Linearer Roundtrip wurde fälschlich als Konflikt bewertet.");
    fixture.SourceService.Import(macExport.ArchivePath, "PC", "1.0", false);
    Equal("mac-change", ReadMarker(fixture.SourceData));
}

void TestIdempotency()
{
    using var fixture = CreateFixture("idempotent");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    fixture.TargetService.Import(export.ArchivePath, "Mac", "1.0", true);
    var result = fixture.TargetService.Import(export.ArchivePath, "Mac", "1.0", false);
    True(result.NoChange, "Zweiter Import wurde nicht als No-Op erkannt.");
    Equal("source", ReadMarker(fixture.TargetData));
}

void TestActivationRollback()
{
    using var fixture = CreateFixture("activation-rollback");
    var export = fixture.SourceService.CreateExport(fixture.Exchange, "PC", "1.0");
    var failingService = new BackupExchangeService(
        fixture.TargetData,
        fixture.TargetState,
        fixture.TargetJournal,
        () => throw new IOException("simulierter Aktivierungsfehler"));
    Throws<IOException>(() => failingService.Import(export.ArchivePath, "Mac", "1.0", true));
    Equal("target", ReadMarker(fixture.TargetData));
    True(Directory.EnumerateFiles(Path.Combine(fixture.TargetData, "Backups"), "BueroCockpit_Rueckfall_*.zip").Any(),
        "Rückfall-Backup wurde vor dem simulierten Fehler nicht erstellt.");
}

TestFixture CreateFixture(string name)
{
    var root = Path.Combine(testRoot, $"{name}-{Guid.NewGuid():N}");
    var sourceData = Path.Combine(root, "pc-data");
    var targetData = Path.Combine(root, "mac-data");
    var exchange = Path.Combine(root, "exchange");
    Directory.CreateDirectory(exchange);
    CreateDatabase(sourceData, "source");
    CreateDatabase(targetData, "target");
    var sourceState = Path.Combine(root, "pc-local", "state.json");
    var targetState = Path.Combine(root, "mac-local", "state.json");
    var sourceJournal = Path.Combine(root, "pc-local", "journal.jsonl");
    var targetJournal = Path.Combine(root, "mac-local", "journal.jsonl");
    return new TestFixture(
        root,
        sourceData,
        targetData,
        exchange,
        sourceState,
        targetState,
        sourceJournal,
        targetJournal,
        new BackupExchangeService(sourceData, sourceState, sourceJournal),
        new BackupExchangeService(targetData, targetState, targetJournal));
}

void CreateDatabase(string dataDirectory, string marker)
{
    Directory.CreateDirectory(dataDirectory);
    var databasePath = Path.Combine(dataDirectory, "buerocockpit.db");
    using var connection = new SqliteConnection($"Data Source={databasePath}");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE Marker (Value TEXT NOT NULL); INSERT INTO Marker (Value) VALUES ($value); PRAGMA user_version=0;";
    command.Parameters.AddWithValue("$value", marker);
    command.ExecuteNonQuery();
    Directory.CreateDirectory(Path.Combine(dataDirectory, "Tasks", "task-1"));
    File.WriteAllText(Path.Combine(dataDirectory, "Tasks", "task-1", "attachment.txt"), $"attachment-{marker}");
    Directory.CreateDirectory(Path.Combine(dataDirectory, "DeskItems", "Files"));
    File.WriteAllText(Path.Combine(dataDirectory, "DeskItems", "Files", "desk.txt"), $"desk-{marker}");
    Directory.CreateDirectory(Path.Combine(dataDirectory, "Backups"));
    File.WriteAllText(Path.Combine(dataDirectory, "Backups", "old.db"), "old-backup");
    File.WriteAllText(Path.Combine(dataDirectory, "settings.json"), "{\"test\":true}");
    File.WriteAllText(Path.Combine(dataDirectory, "settings.local.json"), $"{marker}-local-setting");
    File.WriteAllText(Path.Combine(dataDirectory, "backup-exchange-state.local.json"), "{\"local\":true}");
    File.WriteAllText(Path.Combine(dataDirectory, "buerocockpit.lock"), "lock");
    File.WriteAllText(Path.Combine(dataDirectory, "work.tmp"), "temp");
}

string ReadMarker(string dataDirectory)
{
    using var connection = new SqliteConnection($"Data Source={Path.Combine(dataDirectory, "buerocockpit.db")};Mode=ReadOnly");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT Value FROM Marker LIMIT 1;";
    return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
}

void WriteMarker(string dataDirectory, string marker)
{
    using var connection = new SqliteConnection($"Data Source={Path.Combine(dataDirectory, "buerocockpit.db")}");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "UPDATE Marker SET Value=$value;";
    command.Parameters.AddWithValue("$value", marker);
    command.ExecuteNonQuery();
}

void RewriteArchiveWithCorruptDatabase(string sourceArchive, string targetArchive)
{
    var directory = Path.Combine(testRoot, $"rewrite-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    ZipFile.ExtractToDirectory(sourceArchive, directory);
    var databasePath = Path.Combine(directory, "buerocockpit.db");
    File.WriteAllBytes(databasePath, RandomNumberGenerator.GetBytes(512));
    var manifestPath = Path.Combine(directory, "manifest.json");
    var manifest = JsonSerializer.Deserialize<BackupExchangeManifest>(File.ReadAllText(manifestPath))!;
    manifest.DatabaseSize = new FileInfo(databasePath).Length;
    manifest.DatabaseSha256 = HashFile(databasePath);
    var databaseEntry = manifest.Files.Single(file => file.Path == "buerocockpit.db");
    databaseEntry.Size = manifest.DatabaseSize;
    databaseEntry.Sha256 = manifest.DatabaseSha256;
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    ZipFile.CreateFromDirectory(directory, targetArchive);
    Directory.Delete(directory, recursive: true);
}

string HashFile(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

void Run(string name, Action test)
{
    test();
    Console.WriteLine($"OK: {name}");
}

void True(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

void False(bool condition, string message) => True(!condition, message);

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Erwartet: {expected}; tatsächlich: {actual}");
    }
}

void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Erwartete Ausnahme {typeof(TException).Name} wurde nicht ausgelöst.");
}

sealed record TestFixture(
    string Root,
    string SourceData,
    string TargetData,
    string Exchange,
    string SourceState,
    string TargetState,
    string SourceJournal,
    string TargetJournal,
    BackupExchangeService SourceService,
    BackupExchangeService TargetService) : IDisposable
{
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
