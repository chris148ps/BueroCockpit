import Foundation

enum MobileChangeType: String, Codable, CaseIterable, Sendable {
    case createTask
    case updateTask
    case addPhoto
    case annotatePhoto
    case addNote
    case addAttachment
}

enum MobileChangeStatus: String, Codable, CaseIterable, Sendable {
    case pending
    case sending
    case sent
    case failed
    case conflict
}

enum MobileChangePayloadValue: Codable, Equatable, Sendable {
    case string(String)
    case number(Double)
    case bool(Bool)
    case object([String: MobileChangePayloadValue])
    case array([MobileChangePayloadValue])
    case null

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([MobileChangePayloadValue].self) {
            self = .array(value)
        } else {
            self = .object(try container.decode([String: MobileChangePayloadValue].self))
        }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .string(let value):
            try container.encode(value)
        case .number(let value):
            try container.encode(value)
        case .bool(let value):
            try container.encode(value)
        case .object(let value):
            try container.encode(value)
        case .array(let value):
            try container.encode(value)
        case .null:
            try container.encodeNil()
        }
    }
}

struct MobileChange: Codable, Identifiable, Equatable, Sendable {
    var id: String
    var type: MobileChangeType
    var entityId: String?
    var createdAt: Date
    var updatedAt: Date
    var deviceId: String
    var status: MobileChangeStatus
    var retryCount: Int
    var lastError: String?
    var payload: [String: MobileChangePayloadValue]
}

struct MobileChangeQueue: Codable, Equatable, Sendable {
    var changes: [MobileChange] = []

    var pendingChangeCount: Int {
        changes.filter { $0.status != .sent }.count
    }
}

final class MobileChangeQueueStore: @unchecked Sendable {
    private let fileManager: FileManager
    private let fileName = "mobile-change-queue.json"

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
    }

    func load() -> MobileChangeQueue {
        do {
            let fileURL = try queueFileURL()
            guard fileManager.fileExists(atPath: fileURL.path) else {
                return MobileChangeQueue()
            }

            let data = try Data(contentsOf: fileURL)
            return try Self.decoder.decode(MobileChangeQueue.self, from: data)
        } catch {
            return MobileChangeQueue()
        }
    }

    func save(_ queue: MobileChangeQueue) throws {
        let fileURL = try queueFileURL()
        let directoryURL = fileURL.deletingLastPathComponent()
        try fileManager.createDirectory(at: directoryURL, withIntermediateDirectories: true)

        let data = try Self.encoder.encode(queue)
        let temporaryURL = directoryURL.appendingPathComponent(".\(fileName).tmp", isDirectory: false)
        try data.write(to: temporaryURL, options: .atomic)

        if fileManager.fileExists(atPath: fileURL.path) {
            _ = try fileManager.replaceItemAt(fileURL, withItemAt: temporaryURL)
        } else {
            try fileManager.moveItem(at: temporaryURL, to: fileURL)
        }
    }

    func enqueue(_ change: MobileChange) throws -> MobileChangeQueue {
        var queue = load()
        queue.changes.append(change)
        try save(queue)
        return queue
    }

    func enqueueDummyAddNote(deviceId: String) throws -> MobileChangeQueue {
        let now = Date()
        let change = MobileChange(
            id: UUID().uuidString,
            type: .addNote,
            entityId: nil,
            createdAt: now,
            updatedAt: now,
            deviceId: deviceId,
            status: .pending,
            retryCount: 0,
            lastError: nil,
            payload: [
                "note": .string("Interner addNote-Testeintrag"),
                "source": .string("MobileChangeQueueStore")
            ]
        )
        return try enqueue(change)
    }

    private func queueFileURL() throws -> URL {
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
