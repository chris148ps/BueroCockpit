import Foundation
import UIKit

final class MobileInboxReader: @unchecked Sendable {
    private struct RawTask: Decodable {
        let id: String?
        let createdAt: String?
        let status: String?
        let customerName: String?
        let address: String?
        let phone: String?
        let email: String?
        let title: String?
        let category: String?
        let notes: String?
        let photos: [RawPhoto]?
        let sketches: [RawSketch]?
        let files: [RawFile]?
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

    private struct RawFile: Decodable {
        let id: String?
        let fileName: String?
        let path: String?
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
                address: raw.address?.nonEmptyValue ?? "",
                phone: raw.phone?.nonEmptyValue ?? "",
                email: raw.email?.nonEmptyValue ?? "",
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
                fileCount: raw.files?.count ?? 0,
                attachmentIssueSummary: Self.attachmentIssueSummary(for: raw, entryURL: directoryURL, fileManager: fileManager),
                entryURL: directoryURL
            )
        }

        return entries.sorted {
            $0.createdAt.localizedStandardCompare($1.createdAt) == .orderedDescending
        }
    }

    private static func attachmentIssueSummary(
        for raw: RawTask,
        entryURL: URL,
        fileManager: FileManager
    ) -> String? {
        var missingCount = 0
        var unreadableCount = 0

        for photo in raw.photos ?? [] {
            for path in [
                photo.originalPath,
                photo.previewPath,
                photo.annotatedPath,
                photo.annotatedPreviewPath
            ] {
                inspectImageReference(
                    path,
                    entryURL: entryURL,
                    fileManager: fileManager,
                    missingCount: &missingCount,
                    unreadableCount: &unreadableCount
                )
            }
        }

        for sketch in raw.sketches ?? [] {
            inspectImageReference(
                sketch.path,
                entryURL: entryURL,
                fileManager: fileManager,
                missingCount: &missingCount,
                unreadableCount: &unreadableCount
            )
            inspectFileReference(
                sketch.drawingPath,
                entryURL: entryURL,
                fileManager: fileManager,
                missingCount: &missingCount
            )
        }

        for file in raw.files ?? [] {
            inspectFileReference(
                file.path,
                entryURL: entryURL,
                fileManager: fileManager,
                missingCount: &missingCount
            )
        }

        var parts: [String] = []
        if missingCount == 1 {
            parts.append("1 Anhang fehlt")
        } else if missingCount > 1 {
            parts.append("\(missingCount) Anhänge fehlen")
        }

        if unreadableCount == 1 {
            parts.append("1 Vorschau ist nicht lesbar")
        } else if unreadableCount > 1 {
            parts.append("\(unreadableCount) Vorschauen sind nicht lesbar")
        }

        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    private static func inspectImageReference(
        _ path: String?,
        entryURL: URL,
        fileManager: FileManager,
        missingCount: inout Int,
        unreadableCount: inout Int
    ) {
        guard let url = resolvedURL(path, entryURL: entryURL) else {
            return
        }

        guard fileManager.fileExists(atPath: url.path) else {
            missingCount += 1
            return
        }

        if UIImage(contentsOfFile: url.path) == nil {
            unreadableCount += 1
        }
    }

    private static func inspectFileReference(
        _ path: String?,
        entryURL: URL,
        fileManager: FileManager,
        missingCount: inout Int
    ) {
        guard let url = resolvedURL(path, entryURL: entryURL) else {
            return
        }

        if !fileManager.fileExists(atPath: url.path) {
            missingCount += 1
        }
    }

    private static func resolvedURL(_ path: String?, entryURL: URL) -> URL? {
        guard let value = path?.nonEmptyValue else {
            return nil
        }

        if value.hasPrefix("/") {
            return URL(fileURLWithPath: value)
        }

        return entryURL.appendingPathComponent(value, isDirectory: false)
    }
}

private extension String {
    var nonEmptyValue: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
