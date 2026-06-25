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
    case iCloudSourceUnavailable
    case iCloudBookmarkFailed

    var errorDescription: String? {
        switch self {
        case .noCachedSnapshot:
            return "Es ist keine lokale Live-Datei verfügbar. Bitte Sync/live.bclive erneut importieren."
        case .iCloudSourceUnavailable:
            return "Bitte iCloud-Datei erneut auswählen."
        case .iCloudBookmarkFailed:
            return "Der iCloud-Dateizugriff konnte nicht gespeichert werden. Bitte iCloud-Datei erneut auswählen."
        }
    }
}

final class SnapshotAccessStore: @unchecked Sendable {
    private enum Key {
        static let legacyBookmark = "snapshotLocationBookmark"
        static let sourceFileName = "snapshotSourceFileName"
        static let setupCompleted = "snapshotSetupCompleted"
        static let iCloudSourceBookmark = "snapshotICloudSourceBookmark"
        static let syncSetupVersion = "syncSetupVersion"
    }

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        migrateLegacySetupStateIfNeeded()
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

    var hasICloudSourceBookmark: Bool {
        defaults.data(forKey: Key.iCloudSourceBookmark) != nil
    }

    var hasCurrentLiveSnapshot: Bool {
        (try? cachedLiveSnapshotURL()) != nil
    }

    func clearICloudSourceBookmark() {
        defaults.removeObject(forKey: Key.iCloudSourceBookmark)
    }

    func saveLocalSnapshot(fileName: String) {
        defaults.removeObject(forKey: Key.legacyBookmark)
        defaults.set(fileName, forKey: Key.sourceFileName)
        defaults.set(true, forKey: Key.setupCompleted)
    }

    private func migrateLegacySetupStateIfNeeded() {
        guard defaults.integer(forKey: Key.syncSetupVersion) < 2 else {
            return
        }

        defaults.removeObject(forKey: Key.legacyBookmark)
        if (try? cachedLiveSnapshotURL()) != nil {
            saveLocalSnapshot(fileName: "current.bclive")
        } else if (try? cachedSnapshotURL()) == nil {
            defaults.removeObject(forKey: Key.sourceFileName)
            defaults.removeObject(forKey: Key.setupCompleted)
        }
        defaults.set(2, forKey: Key.syncSetupVersion)
    }

    func cachedSnapshotURL() throws -> URL {
        guard let fileName = savedFileName, !fileName.isEmpty else {
            return try cachedLiveSnapshotURL()
        }

        let url = try SnapshotLocalStorage.importedSnapshotsDirectory()
            .appendingPathComponent(fileName, isDirectory: false)

        guard FileManager.default.fileExists(atPath: url.path) else {
            let liveURL = try cachedLiveSnapshotURL()
            saveLocalSnapshot(fileName: liveURL.lastPathComponent)
            return liveURL
        }

        return url
    }

    func cachedLiveSnapshotURL() throws -> URL {
        let url = try SnapshotLocalStorage.importedSnapshotsDirectory()
            .appendingPathComponent("current.bclive", isDirectory: false)

        guard FileManager.default.fileExists(atPath: url.path) else {
            throw SnapshotAccessError.noCachedSnapshot
        }

        return url
    }

    private func saveICloudSourceBookmark(for sourceURL: URL) throws {
        do {
            let bookmark = try sourceURL.bookmarkData(
                options: [],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            defaults.set(bookmark, forKey: Key.iCloudSourceBookmark)
        } catch {
            throw SnapshotAccessError.iCloudBookmarkFailed
        }
    }

    func resolveICloudSourceURL() throws -> URL {
        guard let bookmark = defaults.data(forKey: Key.iCloudSourceBookmark) else {
            throw SnapshotAccessError.iCloudSourceUnavailable
        }

        var isStale = false
        let url = try URL(
            resolvingBookmarkData: bookmark,
            options: [],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )

        guard !isStale else {
            defaults.removeObject(forKey: Key.iCloudSourceBookmark)
            throw SnapshotAccessError.iCloudSourceUnavailable
        }

        return url
    }

    func copySecurityScopedLiveSnapshotToTemporary(from sourceURL: URL) throws -> URL {
        try copySecurityScopedLiveSnapshotToTemporary(from: sourceURL, saveBookmark: false).temporaryURL
    }

    func copySecurityScopedLiveSnapshotToTemporary(from sourceURL: URL, saveBookmark: Bool) throws -> (temporaryURL: URL, bookmarkWarning: String?) {
        guard sourceURL.pathExtension.caseInsensitiveCompare("bclive") == .orderedSame else {
            throw SnapshotReaderError.invalidPackageSelection
        }

        let accessGranted = sourceURL.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw SnapshotAccessError.iCloudSourceUnavailable
        }
        defer {
            sourceURL.stopAccessingSecurityScopedResource()
        }

        var bookmarkWarning: String?
        if saveBookmark {
            do {
                try saveICloudSourceBookmark(for: sourceURL)
            } catch {
                bookmarkWarning = SnapshotAccessError.iCloudBookmarkFailed.localizedDescription
            }
        }

        let directory = try SnapshotLocalStorage.importedSnapshotsDirectory()
        let temporaryURL = directory.appendingPathComponent(".icloud-\(UUID().uuidString).bclive", isDirectory: false)
        try? FileManager.default.removeItem(at: temporaryURL)

        var coordinationError: NSError?
        var transferError: Error?
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(readingItemAt: sourceURL, options: [.withoutChanges], error: &coordinationError) { readableURL in
            do {
                try FileManager.default.copyItem(at: readableURL, to: temporaryURL)
            } catch {
                transferError = error
            }
        }

        if let coordinationError {
            try? FileManager.default.removeItem(at: temporaryURL)
            throw SnapshotReaderError.localCopyFailed(coordinationError.localizedDescription)
        }

        if let transferError {
            try? FileManager.default.removeItem(at: temporaryURL)
            throw SnapshotReaderError.localCopyFailed(transferError.localizedDescription)
        }

        guard FileManager.default.fileExists(atPath: temporaryURL.path) else {
            throw SnapshotReaderError.localCopyFailed(sourceURL.lastPathComponent)
        }

        return (temporaryURL, bookmarkWarning)
    }

    func installDownloadedLiveSnapshot(from sourceURL: URL) throws -> URL {
        let fileManager = FileManager.default
        let directory = try SnapshotLocalStorage.importedSnapshotsDirectory()
        let destinationURL = directory.appendingPathComponent("current.bclive", isDirectory: false)
        let stagedURL = directory.appendingPathComponent(".download-\(UUID().uuidString).bclive", isDirectory: false)
        defer { try? fileManager.removeItem(at: stagedURL) }

        try fileManager.copyItem(at: sourceURL, to: stagedURL)
        if fileManager.fileExists(atPath: destinationURL.path) {
            _ = try fileManager.replaceItemAt(destinationURL, withItemAt: stagedURL)
        } else {
            try fileManager.moveItem(at: stagedURL, to: destinationURL)
        }
        saveLocalSnapshot(fileName: destinationURL.lastPathComponent)
        return destinationURL
    }

    func reset() {
        if let cachedURL = try? cachedSnapshotURL() {
            try? FileManager.default.removeItem(at: cachedURL)
        }
        defaults.removeObject(forKey: Key.legacyBookmark)
        defaults.removeObject(forKey: Key.sourceFileName)
        defaults.removeObject(forKey: Key.setupCompleted)
        defaults.removeObject(forKey: Key.iCloudSourceBookmark)
    }
}
