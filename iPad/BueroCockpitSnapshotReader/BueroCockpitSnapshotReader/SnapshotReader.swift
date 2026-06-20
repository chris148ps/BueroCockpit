import Foundation

final class SnapshotReader: @unchecked Sendable {
    func readSnapshot(from sourceURL: URL) throws -> SnapshotDocument {
        let accessGranted = sourceURL.startAccessingSecurityScopedResource()
        var snapshotAccessGranted = false
        var snapshotURL: URL?
        defer {
            if snapshotAccessGranted, let snapshotURL {
                snapshotURL.stopAccessingSecurityScopedResource()
            }
            if accessGranted {
                sourceURL.stopAccessingSecurityScopedResource()
            }
        }

        let resolvedSnapshotURL = try resolveSnapshotDirectory(from: sourceURL)
        snapshotURL = resolvedSnapshotURL
        if resolvedSnapshotURL != sourceURL {
            snapshotAccessGranted = resolvedSnapshotURL.startAccessingSecurityScopedResource()
        }

        guard FileManager.default.fileExists(atPath: resolvedSnapshotURL.path) else {
            throw SnapshotReaderError.unreadableFolder(resolvedSnapshotURL.lastPathComponent)
        }

        let rawMetadata: RawMetadata = try decodeRequiredFile(named: "metadata.json", at: resolvedSnapshotURL)
        let rawCategories: [RawCategory] = try decodeRequiredArray(named: "categories.json", at: resolvedSnapshotURL)
        let rawTasks: [RawTask] = try decodeRequiredArray(named: "tasks.json", at: resolvedSnapshotURL)
        let attachments = decodeOptionalAttachments(at: resolvedSnapshotURL)

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
            sourceURL: resolvedSnapshotURL
        )
    }

    private func resolveSnapshotDirectory(from sourceURL: URL) throws -> URL {
        if isMetadataFile(sourceURL) {
            let snapshotURL = sourceURL.deletingLastPathComponent()
            if containsSnapshotFiles(at: snapshotURL) {
                return snapshotURL
            }

            throw SnapshotReaderError.missingRequiredFile("metadata.json")
        }

        let candidateDirectories = [
            sourceURL,
            sourceURL.appendingPathComponent("Sync", isDirectory: true).appendingPathComponent("snapshots", isDirectory: true)
        ]

        for candidate in candidateDirectories {
            if containsSnapshotFiles(at: candidate) {
                return candidate
            }
        }

        throw SnapshotReaderError.missingRequiredFile("metadata.json")
    }

    private func containsSnapshotFiles(at folderURL: URL) -> Bool {
        FileManager.default.fileExists(atPath: folderURL.appendingPathComponent("metadata.json").path)
    }

    private func isMetadataFile(_ url: URL) -> Bool {
        url.lastPathComponent.caseInsensitiveCompare("metadata.json") == .orderedSame
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
