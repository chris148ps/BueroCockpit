import Foundation

struct SnapshotMetadata: Equatable {
    let formatVersion: Int?
    let exportedAt: String?
    let appName: String?
    let appVersion: String?
    let deviceName: String?
    let source: String?
}

struct SnapshotCategory: Identifiable, Equatable {
    let id: String
    let name: String
    let order: Int?
}

struct SnapshotTask: Identifiable, Equatable {
    let id: String
    let title: String
    let customerName: String?
    let categoryIds: [String]
    let categoryNames: [String]
    let dueDate: String?
    let reminderDate: String?
    let createdAt: String?
    let updatedAt: String?
    let materialOrderedAt: String?
    let status: String?
    let notes: String?
    let shortText: String?
    let attachmentRefs: [String]
    let sourceIndex: Int
}

struct SnapshotAttachmentIndex: Identifiable, Equatable {
    let id: String
    let taskId: String
    let fileName: String
    let relativePath: String
    let isImportant: Bool
    let fileExists: Bool
    let sourceIndex: Int
}

struct SnapshotDocument: Equatable {
    let metadata: SnapshotMetadata
    let categories: [SnapshotCategory]
    let tasks: [SnapshotTask]
    let attachments: [SnapshotAttachmentIndex]
    let sourceURL: URL
}

enum SnapshotLoadState: Equatable {
    case idle
    case loading
    case ready
    case empty(String)
    case failure(String)
}

enum SnapshotReaderError: LocalizedError, Equatable {
    case invalidPackageSelection
    case missingRequiredFile(String)
    case invalidJSON(String)
    case unreadableFolder(String)
    case unreadableSnapshotPackage(String)

    var errorDescription: String? {
        switch self {
        case .invalidPackageSelection:
            return "Bitte eine BüroCockpit-Snapshot-Datei auswählen."
        case .missingRequiredFile(let fileName):
            switch fileName.lowercased() {
            case "metadata.json":
                return "Snapshot-Datei ungültig: metadata.json fehlt."
            case "categories.json":
                return "Snapshot-Datei ungültig: categories.json fehlt."
            case "tasks.json":
                return "Snapshot-Datei ungültig: tasks.json fehlt."
            default:
                return "Pflichtdatei fehlt: \(fileName)"
            }
        case .invalidJSON(let fileName):
            return "JSON konnte nicht gelesen werden: \(fileName)"
        case .unreadableFolder(let folderName):
            return "Snapshot-Ordner konnte nicht geöffnet werden: \(folderName)"
        case .unreadableSnapshotPackage(let fileName):
            return "Snapshot-Datei konnte nicht geöffnet werden: \(fileName)"
        }
    }
}
