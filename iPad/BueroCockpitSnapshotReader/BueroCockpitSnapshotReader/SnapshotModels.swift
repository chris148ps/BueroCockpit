import Foundation

struct SnapshotMetadata: Equatable, Sendable {
    let formatVersion: Int?
    let exportedAt: String?
    let appName: String?
    let appVersion: String?
    let deviceName: String?
    let source: String?
}

struct SnapshotCategory: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let order: Int?
}

struct SnapshotCategoryGroup: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let categoryIDs: [String]
    let order: Int
}

struct SnapshotTask: Identifiable, Equatable, Sendable {
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

extension SnapshotTask {
    var displayPrimaryTitle: String {
        SnapshotDisplayFormatter.displayText(customerName)
            ?? SnapshotDisplayFormatter.displayText(title)
            ?? "Aufgabe"
    }

    var displaySecondaryTitle: String? {
        guard let customer = SnapshotDisplayFormatter.displayText(customerName),
              let title = SnapshotDisplayFormatter.displayText(title),
              customer.caseInsensitiveCompare(title) != .orderedSame else {
            return nil
        }
        return title
    }

    var displayDetailMetadata: String? {
        SnapshotDisplayFormatter.joinedMetadata([
            displayStatus,
            displayCategoryNames.first,
            displayCreatedAt
        ])
    }

    var displayListMetadata: String? {
        SnapshotDisplayFormatter.joinedMetadata([
            displayStatus,
            displayCategoryNames.first,
            displayCreatedAt.map { "Erstellt \($0)" }
        ])
    }

    var searchableText: String {
        ([title, customerName, shortText, notes, status].compactMap { $0 } + categoryNames)
            .joined(separator: "\n")
    }

    var displayStatus: String? {
        SnapshotDisplayFormatter.displayText(status)
    }

    var displayCategoryNames: [String] {
        SnapshotDisplayFormatter.deduplicatedDisplayNames(categoryNames)
    }

    var displayDueDate: String? {
        SnapshotDisplayFormatter.displayDate(dueDate)
    }

    var displayReminderDate: String? {
        SnapshotDisplayFormatter.displayDate(reminderDate)
    }

    var displayCreatedAt: String? {
        SnapshotDisplayFormatter.displayDate(createdAt)
    }

    var displayUpdatedAt: String? {
        SnapshotDisplayFormatter.displayDate(updatedAt)
    }

    var displayMaterialOrderedAt: String? {
        SnapshotDisplayFormatter.displayDate(materialOrderedAt)
    }
}

struct SnapshotAttachmentIndex: Identifiable, Equatable, Sendable {
    let id: String
    let taskId: String
    let fileName: String
    let relativePath: String
    let isImportant: Bool
    let fileExists: Bool
    let sourceIndex: Int
}

struct SnapshotDocument: Equatable, Sendable {
    let metadata: SnapshotMetadata
    let categories: [SnapshotCategory]
    let tasks: [SnapshotTask]
    let attachments: [SnapshotAttachmentIndex]
    let sourceURL: URL
}

extension SnapshotMetadata {
    var displayAppVersion: String? {
        SnapshotDisplayFormatter.shortVersion(from: appVersion)
    }

    var displayBuildIdentifier: String? {
        SnapshotDisplayFormatter.buildIdentifier(from: appVersion)
    }

    var displayExportedAt: String? {
        SnapshotDisplayFormatter.displayDate(exportedAt)
    }
}

enum SnapshotDisplayFormatter {
    private static let germanDateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "de_DE_POSIX")
        formatter.timeZone = .current
        formatter.dateFormat = "dd.MM.yyyy, HH:mm"
        return formatter
    }()

    static func displayDate(_ value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }

        if let date = parseISO8601Date(value) {
            return germanDateFormatter.string(from: date)
        }

        return value
    }

    static func displayText(_ value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }

        return value.replacingOccurrences(of: "_", with: " ")
    }

    static func joinedMetadata(_ values: [String?]) -> String? {
        var seen = Set<String>()
        let items = values.compactMap(displayText).filter { value in
            seen.insert(value.lowercased()).inserted
        }
        return items.isEmpty ? nil : items.joined(separator: " · ")
    }

    static func shortVersion(from value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }

        return value.split(separator: "+", maxSplits: 1, omittingEmptySubsequences: false).first.map(String.init) ?? value
    }

    static func buildIdentifier(from value: String?) -> String? {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }

        let components = value.split(separator: "+", maxSplits: 1, omittingEmptySubsequences: false)
        guard components.count == 2 else {
            return nil
        }

        let build = String(components[1])
        return build.isEmpty ? nil : build
    }

    private static func parseISO8601Date(_ value: String) -> Date? {
        let withFractionalSeconds = ISO8601DateFormatter()
        withFractionalSeconds.formatOptions = [.withInternetDateTime, .withFractionalSeconds]

        if let date = withFractionalSeconds.date(from: value) {
            return date
        }

        let withoutFractionalSeconds = ISO8601DateFormatter()
        withoutFractionalSeconds.formatOptions = [.withInternetDateTime]
        return withoutFractionalSeconds.date(from: value)
    }

    static func deduplicatedDisplayNames(_ values: [String]) -> [String] {
        var seen = Set<String>()
        var result: [String] = []

        for value in values {
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty else {
                continue
            }

            let key = trimmed.lowercased()
            guard seen.insert(key).inserted else {
                continue
            }

            result.append(trimmed)
        }

        return result
    }
}

enum SnapshotLoadState: Equatable, Sendable {
    case idle
    case loading
    case ready
    case empty(String)
    case failure(String)
}

enum SnapshotReaderError: LocalizedError, Equatable {
    case invalidPackageSelection
    case securityScopedAccessDenied(String)
    case localCopyFailed(String)
    case missingRequiredFile(String, String?)
    case invalidJSON(String, String?)
    case unreadableFolder(String)
    case unreadableSnapshotPackage(String, String)

    var errorDescription: String? {
        switch self {
        case .invalidPackageSelection:
            return "Bitte eine BüroCockpit-Snapshot-Datei auswählen."
        case .securityScopedAccessDenied(let fileName):
            return "Sicherheitszugriff wurde verweigert: \(fileName)"
        case .localCopyFailed:
            return "Die Snapshot-Datei konnte nicht lokal gespeichert werden. Bitte wähle sie erneut aus."
        case .missingRequiredFile(let fileName, _):
            switch fileName.lowercased() {
            case "metadata.json":
                return "Die Snapshot-Datei ist unvollständig: metadata.json fehlt."
            case "categories.json":
                return "Die Snapshot-Datei ist unvollständig: categories.json fehlt."
            case "tasks.json":
                return "Die Snapshot-Datei ist unvollständig: tasks.json fehlt."
            default:
                return "Die Snapshot-Datei ist unvollständig: \(fileName) fehlt."
            }
        case .invalidJSON(let fileName, _):
            return "Die Daten in \(fileName) konnten nicht gelesen werden. Bitte exportiere den Snapshot erneut."
        case .unreadableFolder(let folderName):
            return "Snapshot-Ordner konnte nicht geöffnet werden: \(folderName)"
        case .unreadableSnapshotPackage(let fileName, _):
            return "Die Snapshot-Datei \(fileName) konnte nicht gelesen werden. Bitte wähle eine neue latest.bcsnapshot aus."
        }
    }
}
