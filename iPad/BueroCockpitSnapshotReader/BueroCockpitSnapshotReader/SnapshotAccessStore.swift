import Foundation

enum SnapshotLocalStorage {
    private static let currentDirectoryName = "BueroCockpit"
    private static let legacyDirectoryName = "BueroCockpitSnapshotReader"

    static func snapshotsDirectory() throws -> URL {
        let fileManager = FileManager.default
        let applicationSupportURL = try fileManager.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        let currentAppDirectory = applicationSupportURL.appendingPathComponent(currentDirectoryName, isDirectory: true)
        let currentSnapshotsDirectory = currentAppDirectory.appendingPathComponent("Snapshots", isDirectory: true)
        try fileManager.createDirectory(at: currentSnapshotsDirectory, withIntermediateDirectories: true)
        try migrateLegacySnapshots(
            from: applicationSupportURL
                .appendingPathComponent(legacyDirectoryName, isDirectory: true)
                .appendingPathComponent("Snapshots", isDirectory: true),
            to: currentSnapshotsDirectory,
            fileManager: fileManager
        )
        return currentSnapshotsDirectory
    }

    static func importedSnapshotsDirectory() throws -> URL {
        let fileManager = FileManager.default
        let documentsURL = try fileManager.url(
            for: .documentDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        let directory = documentsURL.appendingPathComponent("ImportedSnapshots", isDirectory: true)
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    static func attachmentsDirectory(forSnapshot snapshotURL: URL) throws -> URL {
        let safeSnapshotName = sanitizedDirectoryName(from: snapshotURL.lastPathComponent)
        let attributes = try? FileManager.default.attributesOfItem(atPath: snapshotURL.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        let modifiedAt = (attributes?[.modificationDate] as? Date)?.timeIntervalSince1970 ?? 0
        let snapshotCacheKey = "\(safeSnapshotName)_\(size)_\(Int64(modifiedAt))"
        let directory = try snapshotsDirectory()
            .appendingPathComponent("Attachments", isDirectory: true)
            .appendingPathComponent(snapshotCacheKey, isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory
    }

    private static func migrateLegacySnapshots(from legacyDirectory: URL, to currentDirectory: URL, fileManager: FileManager) throws {
        guard fileManager.fileExists(atPath: legacyDirectory.path) else {
            return
        }

        for sourceURL in try fileManager.contentsOfDirectory(
            at: legacyDirectory,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        ) {
            let destinationURL = currentDirectory.appendingPathComponent(sourceURL.lastPathComponent)
            guard !fileManager.fileExists(atPath: destinationURL.path) else {
                continue
            }
            try fileManager.moveItem(at: sourceURL, to: destinationURL)
        }
    }

    private static func sanitizedDirectoryName(from value: String) -> String {
        let invalidCharacters = CharacterSet(charactersIn: "/\\?%*|\"<>:")
            .union(.controlCharacters)
            .union(.whitespacesAndNewlines)
        let components = value.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        let result = components.joined(separator: "_")
        return result.isEmpty ? "Snapshot" : result
    }
}

enum SnapshotAccessError: LocalizedError, Sendable {
    case noCachedSnapshot

    var errorDescription: String? {
        switch self {
        case .noCachedSnapshot:
            return "Es ist keine lokale Live-Datei verfügbar. Bitte Sync/live.bclive erneut importieren."
        }
    }
}

final class SnapshotAccessStore: @unchecked Sendable {
    private enum Key {
        static let bookmark = "snapshotLocationBookmark"
        static let sourceFileName = "snapshotSourceFileName"
        static let setupCompleted = "snapshotSetupCompleted"
    }

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        if defaults.data(forKey: Key.bookmark) != nil {
            defaults.removeObject(forKey: Key.bookmark)
            defaults.removeObject(forKey: Key.sourceFileName)
            defaults.removeObject(forKey: Key.setupCompleted)
        }
    }

    var hasLocalSnapshot: Bool {
        (try? cachedSnapshotURL()) != nil
    }

    var isSetupCompleted: Bool {
        defaults.bool(forKey: Key.setupCompleted) && hasLocalSnapshot
    }

    var savedFileName: String? {
        defaults.string(forKey: Key.sourceFileName)
    }

    func saveLocalSnapshot(fileName: String) {
        defaults.removeObject(forKey: Key.bookmark)
        defaults.set(fileName, forKey: Key.sourceFileName)
        defaults.set(true, forKey: Key.setupCompleted)
    }

    func cachedSnapshotURL() throws -> URL {
        guard let fileName = savedFileName, !fileName.isEmpty else {
            throw SnapshotAccessError.noCachedSnapshot
        }

        let url = try SnapshotLocalStorage.importedSnapshotsDirectory()
            .appendingPathComponent(fileName, isDirectory: false)

        guard FileManager.default.fileExists(atPath: url.path) else {
            throw SnapshotAccessError.noCachedSnapshot
        }

        return url
    }

    func reset() {
        if let cachedURL = try? cachedSnapshotURL() {
            try? FileManager.default.removeItem(at: cachedURL)
        }
        defaults.removeObject(forKey: Key.bookmark)
        defaults.removeObject(forKey: Key.sourceFileName)
        defaults.removeObject(forKey: Key.setupCompleted)
    }
}
