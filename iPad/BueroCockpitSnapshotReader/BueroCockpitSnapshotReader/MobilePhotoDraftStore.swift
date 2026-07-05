import Foundation

enum MobilePhotoDraftSource: String, Codable, CaseIterable, Sendable {
    case iPadCamera
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
    var originalLocalPath: String?
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
                "originalLocalPath": draft.originalLocalPath.map(MobileChangePayloadValue.string) ?? .null,
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
