import Foundation

final class SnapshotReader: @unchecked Sendable {
    func readSnapshot(from sourceURL: URL) throws -> SnapshotDocument {
        let accessGranted = sourceURL.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                sourceURL.stopAccessingSecurityScopedResource()
            }
        }

        if isSnapshotPackage(sourceURL) {
            return try readSnapshotPackage(from: sourceURL)
        }

        let resolvedSnapshotURL = try resolveSnapshotDirectory(from: sourceURL)
        var snapshotAccessGranted = false
        if resolvedSnapshotURL != sourceURL {
            snapshotAccessGranted = resolvedSnapshotURL.startAccessingSecurityScopedResource()
        }
        defer {
            if snapshotAccessGranted {
                resolvedSnapshotURL.stopAccessingSecurityScopedResource()
            }
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

    private func readSnapshotPackage(from sourceURL: URL) throws -> SnapshotDocument {
        do {
            let archive = try SnapshotPackageArchive(contentsOf: sourceURL, fileName: sourceURL.lastPathComponent)

            let rawMetadata: RawMetadata = try decodeRequiredPackageFile(named: "metadata.json", in: archive)
            let rawCategories: [RawCategory] = try decodeRequiredPackageArray(named: "categories.json", in: archive)
            let rawTasks: [RawTask] = try decodeRequiredPackageArray(named: "tasks.json", in: archive)
            let attachments = decodeOptionalPackageAttachments(in: archive)

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
                sourceURL: sourceURL
            )
        } catch let readerError as SnapshotReaderError {
            throw readerError
        } catch {
            throw SnapshotReaderError.unreadableSnapshotPackage(sourceURL.lastPathComponent)
        }
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

    private func isSnapshotPackage(_ url: URL) -> Bool {
        let extensionName = url.pathExtension.lowercased()
        return extensionName == "bcsnapshot" || extensionName == "zip"
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

    private func decodeRequiredPackageFile<T: Decodable>(named fileName: String, in archive: SnapshotPackageArchive) throws -> T {
        guard let data = archive.data(named: fileName) else {
            throw SnapshotReaderError.missingRequiredFile(fileName)
        }

        do {
            return try JSONDecoder.snapshotDecoder.decode(T.self, from: data)
        } catch let readerError as SnapshotReaderError {
            throw readerError
        } catch {
            throw SnapshotReaderError.invalidJSON(fileName)
        }
    }

    private func decodeRequiredPackageArray<T: Decodable>(named fileName: String, in archive: SnapshotPackageArchive) throws -> [T] {
        try decodeRequiredPackageFile(named: fileName, in: archive)
    }

    private func decodeOptionalPackageAttachments(in archive: SnapshotPackageArchive) -> [RawAttachment] {
        guard let data = archive.data(named: "attachments-index.json") else {
            return []
        }

        do {
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

private struct SnapshotPackageArchive {
    private let entries: [String: Data]

    init(contentsOf url: URL, fileName: String) throws {
        let data = try Data(contentsOf: url, options: [.mappedIfSafe])
        entries = try Self.parseEntries(from: data, fileName: fileName)
    }

    func data(named fileName: String) -> Data? {
        entries[fileName.lowercased()]
    }

    private static func parseEntries(from data: Data, fileName: String) throws -> [String: Data] {
        let bytes = [UInt8](data)
        var offset = 0
        var result: [String: Data] = [:]

        while offset + 4 <= bytes.count {
            let signature = readUInt32LE(bytes, offset)
            guard signature == 0x0403_4B50 else {
                break
            }

            guard offset + 30 <= bytes.count else {
                throw SnapshotReaderError.unreadableSnapshotPackage(fileName)
            }

            let compressionMethod = readUInt16LE(bytes, offset + 8)
            let compressedSize = Int(readUInt32LE(bytes, offset + 18))
            let fileNameLength = Int(readUInt16LE(bytes, offset + 26))
            let extraFieldLength = Int(readUInt16LE(bytes, offset + 28))

            offset += 30

            guard offset + fileNameLength + extraFieldLength <= bytes.count else {
                throw SnapshotReaderError.unreadableSnapshotPackage(fileName)
            }

            let fileNameData = Data(bytes[offset ..< offset + fileNameLength])
            offset += fileNameLength
            offset += extraFieldLength

            guard offset + compressedSize <= bytes.count else {
                throw SnapshotReaderError.unreadableSnapshotPackage(fileName)
            }

            guard compressionMethod == 0 else {
                throw SnapshotReaderError.unreadableSnapshotPackage(fileName)
            }

            let fileData = Data(bytes[offset ..< offset + compressedSize])
            offset += compressedSize

            let fileName = String(data: fileNameData, encoding: .utf8) ?? String(decoding: fileNameData, as: UTF8.self)
            result[fileName.lowercased()] = fileData
        }

        if result.isEmpty {
            throw SnapshotReaderError.unreadableSnapshotPackage(fileName)
        }

        return result
    }

    private static func readUInt16LE(_ bytes: [UInt8], _ offset: Int) -> UInt16 {
        UInt16(bytes[offset]) | (UInt16(bytes[offset + 1]) << 8)
    }

    private static func readUInt32LE(_ bytes: [UInt8], _ offset: Int) -> UInt32 {
        UInt32(bytes[offset])
            | (UInt32(bytes[offset + 1]) << 8)
            | (UInt32(bytes[offset + 2]) << 16)
            | (UInt32(bytes[offset + 3]) << 24)
    }
}
