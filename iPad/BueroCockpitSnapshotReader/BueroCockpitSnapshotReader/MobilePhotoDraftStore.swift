import Foundation
import UIKit

enum MobilePhotoDraftSource: String, Codable, CaseIterable, Sendable {
    case iPadCamera
    case photoLibrary
    case iPhoneImport
    case shareExtension
    case fileImport
}

enum MobilePhotoDraftStatus: String, Codable, CaseIterable, Sendable {
    case draft
    case assigned
    case pendingUpload
    case uploaded
    case failed
    case conflict
}

struct MobilePhotoDraft: Codable, Identifiable, Equatable, Sendable {
    var id: String
    var createdAt: Date
    var updatedAt: Date
    var sourceDevice: String
    var source: MobilePhotoDraftSource
    var status: MobilePhotoDraftStatus
    var linkedTaskId: String?
    var localImagePath: String?
    var originalLocalPath: String?
    var originalFilename: String?
    var thumbnailPath: String?
    var annotatedLocalPath: String?
    var note: String?
    var syncChangeId: String?
}

struct MobilePhotoDraftCollection: Codable, Equatable, Sendable {
    var drafts: [MobilePhotoDraft] = []

    var openDraftCount: Int {
        drafts.filter { $0.status != .uploaded }.count
    }
}

final class MobilePhotoDraftStore: @unchecked Sendable {
    private let fileManager: FileManager
    private let fileName = "mobile-photo-drafts.json"

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
    }

    func load() -> MobilePhotoDraftCollection {
        do {
            let fileURL = try draftFileURL()
            guard fileManager.fileExists(atPath: fileURL.path) else {
                return MobilePhotoDraftCollection()
            }

            let data = try Data(contentsOf: fileURL)
            return try Self.decoder.decode(MobilePhotoDraftCollection.self, from: data)
        } catch {
            return MobilePhotoDraftCollection()
        }
    }

    func save(_ collection: MobilePhotoDraftCollection) throws {
        let fileURL = try draftFileURL()
        let directoryURL = fileURL.deletingLastPathComponent()
        try fileManager.createDirectory(at: directoryURL, withIntermediateDirectories: true)

        let data = try Self.encoder.encode(collection)
        let temporaryURL = directoryURL.appendingPathComponent(".\(fileName).tmp", isDirectory: false)
        try data.write(to: temporaryURL, options: .atomic)

        if fileManager.fileExists(atPath: fileURL.path) {
            _ = try fileManager.replaceItemAt(fileURL, withItemAt: temporaryURL)
        } else {
            try fileManager.moveItem(at: temporaryURL, to: fileURL)
        }
    }

    func createDummyDraft(sourceDevice: String, source: MobilePhotoDraftSource = .iPadCamera) throws -> MobilePhotoDraftCollection {
        var collection = load()
        let now = Date()
        let draft = MobilePhotoDraft(
            id: UUID().uuidString,
            createdAt: now,
            updatedAt: now,
            sourceDevice: sourceDevice,
            source: source,
            status: .draft,
            linkedTaskId: nil,
            localImagePath: nil,
            originalLocalPath: nil,
            originalFilename: nil,
            thumbnailPath: nil,
            annotatedLocalPath: nil,
            note: "Interner Test-Fotoentwurf ohne Foto",
            syncChangeId: nil
        )
        collection.drafts.insert(draft, at: 0)
        try save(collection)
        return collection
    }

    func importPhotoDraft(
        imageData: Data,
        originalFilename: String?,
        sourceDevice: String,
        source: MobilePhotoDraftSource = .photoLibrary
    ) throws -> MobilePhotoDraftCollection {
        var collection = load()
        let now = Date()
        let id = UUID().uuidString
        let extensionName = Self.preferredImageExtension(for: imageData)
        let sanitizedFilename = Self.sanitizedFilename(
            originalFilename,
            fallback: "Foto-\(id).\(extensionName)"
        )
        let draftDirectoryURL = try draftAssetDirectoryURL(for: id)
        try fileManager.createDirectory(at: draftDirectoryURL, withIntermediateDirectories: true)

        let originalURL = draftDirectoryURL.appendingPathComponent(sanitizedFilename, isDirectory: false)
        try imageData.write(to: originalURL, options: .atomic)

        let thumbnailURL: URL?
        if let thumbnailData = Self.makeThumbnailJPEGData(from: imageData) {
            let url = draftDirectoryURL.appendingPathComponent("thumbnail.jpg", isDirectory: false)
            try thumbnailData.write(to: url, options: .atomic)
            thumbnailURL = url
        } else {
            thumbnailURL = nil
        }

        let draft = MobilePhotoDraft(
            id: id,
            createdAt: now,
            updatedAt: now,
            sourceDevice: sourceDevice,
            source: source,
            status: .draft,
            linkedTaskId: nil,
            localImagePath: originalURL.path,
            originalLocalPath: originalURL.path,
            originalFilename: sanitizedFilename,
            thumbnailPath: thumbnailURL?.path,
            annotatedLocalPath: nil,
            note: nil,
            syncChangeId: nil
        )
        collection.drafts.insert(draft, at: 0)
        try save(collection)
        return collection
    }

    func mobileChange(for draft: MobilePhotoDraft, deviceId: String) -> MobileChange {
        MobileChange(
            id: draft.syncChangeId ?? UUID().uuidString,
            type: .addPhoto,
            entityId: draft.linkedTaskId,
            createdAt: draft.createdAt,
            updatedAt: draft.updatedAt,
            deviceId: deviceId,
            status: .pending,
            retryCount: 0,
            lastError: nil,
            payload: [
                "photoDraftId": .string(draft.id),
                "sourceDevice": .string(draft.sourceDevice),
                "source": .string(draft.source.rawValue),
                "status": .string(draft.status.rawValue),
                "linkedTaskId": draft.linkedTaskId.map(MobileChangePayloadValue.string) ?? .null,
                "localImagePath": draft.localImagePath.map(MobileChangePayloadValue.string) ?? .null,
                "originalLocalPath": draft.originalLocalPath.map(MobileChangePayloadValue.string) ?? .null,
                "originalFilename": draft.originalFilename.map(MobileChangePayloadValue.string) ?? .null,
                "thumbnailPath": draft.thumbnailPath.map(MobileChangePayloadValue.string) ?? .null,
                "annotatedLocalPath": draft.annotatedLocalPath.map(MobileChangePayloadValue.string) ?? .null,
                "note": draft.note.map(MobileChangePayloadValue.string) ?? .null
            ]
        )
    }

    private func draftFileURL() throws -> URL {
        let applicationSupportURL = try fileManager.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        return applicationSupportURL
            .appendingPathComponent("BueroCockpit", isDirectory: true)
            .appendingPathComponent(fileName, isDirectory: false)
    }

    private func draftAssetDirectoryURL(for draftID: String) throws -> URL {
        let applicationSupportURL = try fileManager.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        return applicationSupportURL
            .appendingPathComponent("BueroCockpit", isDirectory: true)
            .appendingPathComponent("MobilePhotoDrafts", isDirectory: true)
            .appendingPathComponent(draftID, isDirectory: true)
    }

    private static func sanitizedFilename(_ filename: String?, fallback: String) -> String {
        let trimmed = filename?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let candidate = trimmed.isEmpty ? fallback : trimmed
        let invalidCharacters = CharacterSet(charactersIn: "/\\:")
            .union(.newlines)
            .union(.controlCharacters)
        let parts = candidate.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        return parts.joined(separator: "-").trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? fallback
            : parts.joined(separator: "-")
    }

    private static func preferredImageExtension(for data: Data) -> String {
        if data.starts(with: [0x89, 0x50, 0x4E, 0x47]) {
            return "png"
        }
        if data.starts(with: [0xFF, 0xD8, 0xFF]) {
            return "jpg"
        }
        if data.count > 12,
           let type = String(data: data[8..<12], encoding: .ascii),
           ["heic", "heix", "hevc", "hevx"].contains(type) {
            return "heic"
        }
        return "jpg"
    }

    private static func makeThumbnailJPEGData(from data: Data) -> Data? {
        guard let image = UIImage(data: data) else {
            return nil
        }

        let targetSide: CGFloat = 320
        let scale = min(targetSide / max(image.size.width, image.size.height), 1)
        let targetSize = CGSize(width: image.size.width * scale, height: image.size.height * scale)
        let renderer = UIGraphicsImageRenderer(size: targetSize)
        let thumbnail = renderer.image { _ in
            image.draw(in: CGRect(origin: .zero, size: targetSize))
        }
        return thumbnail.jpegData(compressionQuality: 0.78)
    }

    private static var encoder: JSONEncoder {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        return encoder
    }

    private static var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }
}
