import Foundation

final class MobileInboxReader: @unchecked Sendable {
    private struct RawTask: Decodable {
        let id: String?
        let createdAt: String?
        let status: String?
        let customerName: String?
        let title: String?
        let category: String?
        let notes: String?
        let photos: [RawPhoto]?
        let sketches: [RawSketch]?
    }

    private struct RawPhoto: Decodable {
        let id: String?
        let originalPath: String?
        let previewPath: String?
        let annotatedPath: String?
        let annotatedPreviewPath: String?
    }

    private struct RawSketch: Decodable {
        let id: String?
        let path: String?
        let drawingPath: String?
    }

    private let store: MobileInboxStore
    private let fileManager: FileManager
    private let decoder: JSONDecoder

    init(
        store: MobileInboxStore = MobileInboxStore(),
        fileManager: FileManager = .default
    ) {
        self.store = store
        self.fileManager = fileManager
        decoder = JSONDecoder()
    }

    func loadPendingEntries() throws -> [MobileInboxPendingEntry] {
        guard store.hasSelectedFolder else {
            return []
        }

        let selectedFolder = try store.resolveSelectedFolder()
        let accessGranted = selectedFolder.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw MobileInboxError.folderUnavailable
        }
        defer {
            selectedFolder.stopAccessingSecurityScopedResource()
        }

        let inboxURL = store.mobileInboxURL(for: selectedFolder)
        guard fileManager.fileExists(atPath: inboxURL.path) else {
            return []
        }

        let directories = try fileManager.contentsOfDirectory(
            at: inboxURL,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        )

        let entries = directories.compactMap { directoryURL -> MobileInboxPendingEntry? in
            guard directoryURL.lastPathComponent.hasPrefix("mobile-") else {
                return nil
            }

            let jsonURL = directoryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
            guard let data = try? Data(contentsOf: jsonURL),
                  let raw = try? decoder.decode(RawTask.self, from: data) else {
                return nil
            }

            let status = raw.status?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            guard status.caseInsensitiveCompare("new") == .orderedSame else {
                return nil
            }

            return MobileInboxPendingEntry(
                id: raw.id?.nonEmptyValue ?? directoryURL.lastPathComponent,
                customerName: raw.customerName?.nonEmptyValue ?? "",
                title: raw.title?.nonEmptyValue ?? "",
                category: raw.category?.nonEmptyValue ?? "",
                notes: raw.notes ?? "",
                createdAt: raw.createdAt?.nonEmptyValue ?? "",
                status: status,
                photoCount: raw.photos?.count ?? 0,
                annotatedPhotoCount: raw.photos?.filter { photo in
                    photo.annotatedPath?.nonEmptyValue != nil ||
                    photo.annotatedPreviewPath?.nonEmptyValue != nil
                }.count ?? 0,
                sketchCount: raw.sketches?.count ?? 0,
                entryURL: directoryURL
            )
        }

        return entries.sorted {
            $0.createdAt.localizedStandardCompare($1.createdAt) == .orderedDescending
        }
    }
}

private extension String {
    var nonEmptyValue: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
