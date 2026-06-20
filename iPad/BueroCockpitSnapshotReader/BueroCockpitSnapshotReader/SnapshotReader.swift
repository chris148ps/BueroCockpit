import Foundation

final class SnapshotReader: @unchecked Sendable {
    func readSnapshot(from folderURL: URL) throws -> SnapshotDocument {
        let accessGranted = folderURL.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                folderURL.stopAccessingSecurityScopedResource()
            }
        }

        let snapshotURL = try resolveSnapshotDirectory(from: folderURL)
        guard FileManager.default.fileExists(atPath: snapshotURL.path) else {
            throw SnapshotReaderError.unreadableFolder(snapshotURL.lastPathComponent)
        }

        let rawMetadata: RawMetadata = try decodeRequiredFile(named: "metadata.json", at: snapshotURL)
        let rawCategories: [RawCategory] = try decodeRequiredArray(named: "categories.json", at: snapshotURL)
        let rawTasks: [RawTask] = try decodeRequiredArray(named: "tasks.json", at: snapshotURL)
        let attachments = decodeOptionalAttachments(at: snapshotURL)

        let metadata = SnapshotMetadata(
            formatVersion: rawMetadata.formatVersion,
            exportedAt: rawMetadata.exportedAt,
            appName: rawMetadata.appName,
            appVersion: rawMetadata.appVersion,
            deviceName: rawMetadata.deviceName,
            source: rawMetadata.source
        )

        let categories: [SnapshotCategory] = rawCategories.enumerated().compactMap { index, raw -> SnapshotCategory? in
            guard let id = raw.id, let name = raw.name else {
                return nil
            }

            return SnapshotCategory(id: id, name: name, order: raw.order ?? index)
        }

        let tasks: [SnapshotTask] = rawTasks.enumerated().compactMap { index, raw -> SnapshotTask? in
            guard let id = raw.id, let title = raw.title else {
                return nil
            }

            return SnapshotTask(
                id: id,
                title: title,
                customerName: raw.customerName,
                categoryIds: raw.categoryIds ?? [],
                categoryNames: raw.categoryNames ?? [],
                dueDate: raw.dueDate,
                reminderDate: raw.reminderDate,
                createdAt: raw.createdAt,
                updatedAt: raw.updatedAt,
                materialOrderedAt: raw.materialOrderedAt,
                status: raw.status,
                notes: raw.notes,
                shortText: raw.shortText,
                attachmentRefs: raw.attachmentRefs ?? [],
                sourceIndex: index
            )
        }

        let normalizedAttachments: [SnapshotAttachmentIndex] = attachments.enumerated().compactMap { index, raw -> SnapshotAttachmentIndex? in
            guard let id = raw.id, let taskId = raw.taskId, let fileName = raw.fileName, let relativePath = raw.relativePath else {
                return nil
            }

            return SnapshotAttachmentIndex(
                id: id,
                taskId: taskId,
                fileName: fileName,
                relativePath: relativePath,
                isImportant: raw.isImportant ?? false,
                fileExists: raw.fileExists ?? false,
                sourceIndex: index
            )
        }

        return SnapshotDocument(
            metadata: metadata,
            categories: categories,
            tasks: tasks,
            attachments: normalizedAttachments,
            sourceURL: snapshotURL
        )
    }

    private func resolveSnapshotDirectory(from folderURL: URL) throws -> URL {
        let candidateDirectories = [
            folderURL,
            folderURL.appendingPathComponent("Sync", isDirectory: true).appendingPathComponent("snapshots", isDirectory: true)
        ]

        for candidate in candidateDirectories {
            if FileManager.default.fileExists(atPath: candidate.appendingPathComponent("metadata.json").path) {
                return candidate
            }
        }

        throw SnapshotReaderError.missingRequiredFile("metadata.json")
    }

    private func decodeRequiredFile<T: Decodable>(named fileName: String, at folderURL: URL) throws -> T {
        let fileURL = folderURL.appendingPathComponent(fileName)
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            throw SnapshotReaderError.missingRequiredFile(fileName)
        }

        do {
            let data = try Data(contentsOf: fileURL, options: [.mappedIfSafe])
            return try JSONDecoder.snapshotDecoder.decode(T.self, from: data)
        } catch {
            throw SnapshotReaderError.invalidJSON(fileName)
        }
    }

    private func decodeRequiredArray<T: Decodable>(named fileName: String, at folderURL: URL) throws -> [T] {
        try decodeRequiredFile(named: fileName, at: folderURL)
    }

    private func decodeOptionalAttachments(at folderURL: URL) -> [RawAttachment] {
        let fileURL = folderURL.appendingPathComponent("attachments-index.json")
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            return []
        }

        do {
            let data = try Data(contentsOf: fileURL, options: [.mappedIfSafe])
            return try JSONDecoder.snapshotDecoder.decode([RawAttachment].self, from: data)
        } catch {
            return []
        }
    }
}

private extension JSONDecoder {
    static var snapshotDecoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .useDefaultKeys
        return decoder
    }
}

private struct RawMetadata: Decodable {
    let formatVersion: Int?
    let exportedAt: String?
    let appName: String?
    let appVersion: String?
    let deviceName: String?
    let source: String?
}

private struct RawCategory: Decodable {
    let id: String?
    let name: String?
    let order: Int?
}

private struct RawTask: Decodable {
    let id: String?
    let title: String?
    let customerName: String?
    let categoryIds: [String]?
    let categoryNames: [String]?
    let dueDate: String?
    let reminderDate: String?
    let createdAt: String?
    let updatedAt: String?
    let materialOrderedAt: String?
    let status: String?
    let notes: String?
    let shortText: String?
    let attachmentRefs: [String]?
}

private struct RawAttachment: Decodable {
    let id: String?
    let taskId: String?
    let fileName: String?
    let relativePath: String?
    let isImportant: Bool?
    let fileExists: Bool?
}
