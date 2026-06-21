import Foundation

enum SnapshotAccessError: LocalizedError, Sendable {
    case noSavedLocation
    case bookmarkCreationFailed
    case invalidBookmark
    case staleBookmark
    case noCachedSnapshot

    var errorDescription: String? {
        switch self {
        case .noSavedLocation:
            return "Es ist noch kein Snapshot-Ort gespeichert."
        case .bookmarkCreationFailed:
            return "Der Zugriff auf die ausgewählte Snapshot-Datei konnte nicht gespeichert werden."
        case .invalidBookmark:
            return "Der gespeicherte Snapshot-Ort ist nicht mehr gültig. Bitte wähle die Datei erneut aus."
        case .staleBookmark:
            return "Der gespeicherte Snapshot-Ort ist veraltet. Bitte wähle die Datei erneut aus."
        case .noCachedSnapshot:
            return "Es ist keine lokale Snapshot-Kopie verfügbar."
        }
    }
}

struct ResolvedSnapshotLocation: Sendable {
    let url: URL
    let isStale: Bool
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
    }

    var hasSavedLocation: Bool {
        defaults.data(forKey: Key.bookmark) != nil
    }

    var isSetupCompleted: Bool {
        defaults.bool(forKey: Key.setupCompleted) && hasSavedLocation
    }

    var savedFileName: String? {
        defaults.string(forKey: Key.sourceFileName)
    }

    func makeBookmark(for url: URL) throws -> Data {
        let accessGranted = url.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                url.stopAccessingSecurityScopedResource()
            }
        }

        do {
            // iPadOS preserves the document picker's security scope in a minimal bookmark.
            return try url.bookmarkData(
                options: .minimalBookmark,
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
        } catch {
            throw SnapshotAccessError.bookmarkCreationFailed
        }
    }

    func save(bookmark: Data, fileName: String) {
        defaults.set(bookmark, forKey: Key.bookmark)
        defaults.set(fileName, forKey: Key.sourceFileName)
        defaults.set(true, forKey: Key.setupCompleted)
    }

    func resolveSavedLocation() throws -> ResolvedSnapshotLocation {
        SnapshotPerformanceLog.event("Bookmark resolution started")
        guard let bookmark = defaults.data(forKey: Key.bookmark) else {
            throw SnapshotAccessError.noSavedLocation
        }

        var isStale = false
        do {
            let url = try URL(
                resolvingBookmarkData: bookmark,
                options: [.withoutUI],
                relativeTo: nil,
                bookmarkDataIsStale: &isStale
            )
            SnapshotPerformanceLog.event("Bookmark resolution finished")
            return ResolvedSnapshotLocation(url: url, isStale: isStale)
        } catch {
            throw SnapshotAccessError.invalidBookmark
        }
    }

    func cachedSnapshotURL() throws -> URL {
        guard let fileName = savedFileName, !fileName.isEmpty else {
            throw SnapshotAccessError.noCachedSnapshot
        }

        let applicationSupportURL = try FileManager.default.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        let url = applicationSupportURL
            .appendingPathComponent("BueroCockpitSnapshotReader", isDirectory: true)
            .appendingPathComponent("Snapshots", isDirectory: true)
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
