import Foundation
import Compression

final class SnapshotReader: @unchecked Sendable {
    func prepareAttachment(_ attachment: SnapshotAttachmentIndex, from sourceURL: URL) throws -> URL {
        if let localURL = attachment.localURL,
           FileManager.default.fileExists(atPath: localURL.path) {
            return localURL
        }

        guard isSnapshotPackage(sourceURL),
              let packagePath = attachment.packagePath else {
            throw SnapshotAttachmentError.missingFromSnapshot
        }

        do {
            let archive = try SnapshotPackageArchive(contentsOf: sourceURL)
            guard archive.containsEntry(named: packagePath) else {
                throw SnapshotAttachmentError.missingFromSnapshot
            }
            return try extractAttachment(attachment, archive: archive, sourceURL: sourceURL)
        } catch let attachmentError as SnapshotAttachmentError {
            throw attachmentError
        } catch {
            throw SnapshotAttachmentError.extractionFailed(error.localizedDescription)
        }
    }

    func readSnapshot(from sourceURL: URL) throws -> SnapshotDocument {
        SnapshotPerformanceLog.event("Reader started")
        if isSnapshotPackage(sourceURL) {
            let localPackageURL = try importSnapshotPackage(from: sourceURL)
            return try readSnapshotPackage(from: localPackageURL)
        }

        let accessGranted = sourceURL.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                sourceURL.stopAccessingSecurityScopedResource()
            }
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
        let attachments: [RawAttachment]
        if resolvedSnapshotURL.lastPathComponent.caseInsensitiveCompare("live") == .orderedSame {
            attachments = try decodeRequiredArray(named: "attachments-index.json", at: resolvedSnapshotURL)
        } else {
            attachments = decodeOptionalAttachments(at: resolvedSnapshotURL)
        }
        SnapshotPerformanceLog.event("JSON decoded")

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

        let normalizedAttachments = normalizeAttachments(attachments, folderURL: resolvedSnapshotURL)

        return SnapshotDocument(
            metadata: metadata,
            categories: categories,
            tasks: tasks,
            attachments: normalizedAttachments,
            sourceURL: resolvedSnapshotURL
        )
    }

    func readCachedSnapshot(from sourceURL: URL) throws -> SnapshotDocument {
        SnapshotPerformanceLog.event("Cached package read started")
        guard isSnapshotPackage(sourceURL) else {
            throw SnapshotReaderError.invalidPackageSelection
        }

        return try readSnapshotPackage(from: sourceURL)
    }

    private func readSnapshotPackage(from sourceURL: URL) throws -> SnapshotDocument {
        do {
            let archive = try SnapshotPackageArchive(contentsOf: sourceURL)
            SnapshotPerformanceLog.event("Package read")

            let rawMetadata: RawMetadata = try decodeRequiredPackageFile(named: "metadata.json", in: archive)
            let rawCategories: [RawCategory] = try decodeRequiredPackageArray(named: "categories.json", in: archive)
            let rawTasks: [RawTask] = try decodeRequiredPackageArray(named: "tasks.json", in: archive)
            let attachments: [RawAttachment]
            if sourceURL.pathExtension.caseInsensitiveCompare("bclive") == .orderedSame {
                attachments = try decodeRequiredPackageArray(named: "attachments-index.json", in: archive)
            } else {
                attachments = decodeOptionalPackageAttachments(in: archive)
            }
            SnapshotPerformanceLog.event("JSON decoded")

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

            let normalizedAttachments = normalizeAttachments(attachments, archive: archive, sourceURL: sourceURL)

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
            throw SnapshotReaderError.unreadableSnapshotPackage(sourceURL.lastPathComponent, error.localizedDescription)
        }
    }

    private func importSnapshotPackage(from sourceURL: URL) throws -> URL {
        SnapshotPerformanceLog.event("Local copy started")
        guard sourceURL.startAccessingSecurityScopedResource() else {
            throw SnapshotReaderError.localCopyFailed(sourceURL.lastPathComponent)
        }
        defer {
            sourceURL.stopAccessingSecurityScopedResource()
        }

        let snapshotsDirectory = try localSnapshotsDirectory()
        let fileExtension = sourceURL.pathExtension.lowercased()
        let destinationURL = snapshotsDirectory.appendingPathComponent("current.\(fileExtension)", isDirectory: false)

        let sourceInfo = try? FileManager.default.attributesOfItem(atPath: sourceURL.path)
        let sourceExists = FileManager.default.fileExists(atPath: sourceURL.path)
        let sourceSize = (sourceInfo?[.size] as? NSNumber)?.int64Value

        var coordinationError: NSError?
        var transferError: Error?
        let temporaryURL = snapshotsDirectory.appendingPathComponent(".import-\(UUID().uuidString).\(fileExtension)", isDirectory: false)
        defer { try? FileManager.default.removeItem(at: temporaryURL) }
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(readingItemAt: sourceURL, options: [.withoutChanges], error: &coordinationError) { readableURL in
            do {
                try FileManager.default.copyItem(at: readableURL, to: temporaryURL)
            } catch {
                transferError = error
            }
        }

        if let coordinationError {
            throw SnapshotReaderError.localCopyFailed(coordinationError.localizedDescription)
        }

        if let transferError {
            throw SnapshotReaderError.localCopyFailed(
                """
                \(sourceURL.lastPathComponent)
                Lokale Ziel-URL: \(destinationURL.path)
                Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")
                \(transferError.localizedDescription)
                """
            )
        }

        let temporaryAttributes = try? FileManager.default.attributesOfItem(atPath: temporaryURL.path)
        let temporarySize = (temporaryAttributes?[.size] as? NSNumber)?.int64Value
        guard FileManager.default.fileExists(atPath: temporaryURL.path), let temporarySize, temporarySize > 0 else {
            throw SnapshotReaderError.localCopyFailed(
                "Quelle vorhanden: \(sourceExists ? "ja" : "nein"), Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")"
            )
        }

        do {
            try validateImportedPackage(at: temporaryURL)
        } catch {
            throw SnapshotReaderError.localLiveFileUnreadable(error.localizedDescription)
        }

        do {
            if FileManager.default.fileExists(atPath: destinationURL.path) {
                _ = try FileManager.default.replaceItemAt(destinationURL, withItemAt: temporaryURL)
            } else {
                try FileManager.default.moveItem(at: temporaryURL, to: destinationURL)
            }
        } catch {
            throw SnapshotReaderError.localCopyFailed(error.localizedDescription)
        }

        cleanupOtherImportedPackages(keeping: destinationURL, in: snapshotsDirectory)
        SnapshotPerformanceLog.event("Local copy finished")
        return destinationURL
    }

    private func localSnapshotsDirectory() throws -> URL {
        try SnapshotLocalStorage.importedSnapshotsDirectory()
    }

    private func validateImportedPackage(at packageURL: URL) throws {
        let archive = try SnapshotPackageArchive(contentsOf: packageURL)
        let _: RawMetadata = try decodeRequiredPackageFile(named: "metadata.json", in: archive)
        let _: [RawCategory] = try decodeRequiredPackageArray(named: "categories.json", in: archive)
        let _: [RawTask] = try decodeRequiredPackageArray(named: "tasks.json", in: archive)
        if packageURL.pathExtension.caseInsensitiveCompare("bclive") == .orderedSame {
            let _: [RawAttachment] = try decodeRequiredPackageArray(named: "attachments-index.json", in: archive)
        }
    }

    private func cleanupOtherImportedPackages(keeping currentURL: URL, in directory: URL) {
        for fileExtension in ["bclive", "bcsnapshot", "zip"] {
            let candidate = directory.appendingPathComponent("current.\(fileExtension)", isDirectory: false)
            if candidate != currentURL {
                try? FileManager.default.removeItem(at: candidate)
            }
        }
    }

    private func resolveSnapshotDirectory(from sourceURL: URL) throws -> URL {
        if isMetadataFile(sourceURL) {
            let snapshotURL = sourceURL.deletingLastPathComponent()
            if containsSnapshotFiles(at: snapshotURL) {
                return snapshotURL
            }

            throw SnapshotReaderError.missingRequiredFile("metadata.json", nil)
        }

        let candidateDirectories = [
            sourceURL,
            sourceURL.appendingPathComponent("live", isDirectory: true),
            sourceURL.appendingPathComponent("Sync", isDirectory: true).appendingPathComponent("live", isDirectory: true),
            sourceURL.appendingPathComponent("Sync", isDirectory: true).appendingPathComponent("snapshots", isDirectory: true)
        ]

        for candidate in candidateDirectories {
            if containsSnapshotFiles(at: candidate) {
                return candidate
            }
        }

        throw SnapshotReaderError.missingRequiredFile("metadata.json", nil)
    }

    private func isSnapshotPackage(_ url: URL) -> Bool {
        let extensionName = url.pathExtension.lowercased()
        return extensionName == "bclive" || extensionName == "bcsnapshot" || extensionName == "zip"
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
            throw SnapshotReaderError.missingRequiredFile(fileName, nil)
        }

        do {
            let data = try Data(contentsOf: fileURL, options: [.mappedIfSafe])
            return try JSONDecoder.snapshotDecoder.decode(T.self, from: data)
        } catch {
            throw SnapshotReaderError.invalidJSON(fileName, error.localizedDescription)
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
            throw SnapshotReaderError.missingRequiredFile(fileName, archive.diagnostics(forMissingFile: fileName))
        }

        do {
            return try JSONDecoder.snapshotDecoder.decode(T.self, from: data)
        } catch let readerError as SnapshotReaderError {
            throw readerError
        } catch {
            throw SnapshotReaderError.invalidJSON(fileName, "\(error.localizedDescription)\n\(archive.summary)")
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

    private func normalizeAttachments(_ attachments: [RawAttachment], folderURL: URL) -> [SnapshotAttachmentIndex] {
        attachments.enumerated().compactMap { index, raw -> SnapshotAttachmentIndex? in
            guard let id = raw.id, let taskId = raw.taskId, let fileName = raw.fileName, let relativePath = raw.relativePath else {
                return nil
            }

            let localURL = [raw.packagePath, raw.previewPath]
                .compactMap { $0 }
                .lazy
                .map { folderURL.appendingPathComponent($0) }
                .first { FileManager.default.fileExists(atPath: $0.path) }

            return SnapshotAttachmentIndex(
                id: id,
                taskId: taskId,
                fileName: fileName,
                originalFileName: raw.originalFileName,
                displayName: raw.displayName,
                relativePath: relativePath,
                packagePath: raw.packagePath,
                previewAvailable: raw.previewAvailable ?? (raw.previewPath != nil),
                originalAvailableInLiveSync: raw.originalAvailableInLiveSync ?? false,
                originalDownloadMode: raw.originalDownloadMode,
                reason: raw.reason,
                contentType: raw.contentType,
                sizeBytes: raw.sizeBytes,
                isImportant: raw.isImportant ?? false,
                fileExists: raw.fileExists ?? false,
                existsInSnapshot: raw.existsInSnapshot ?? (localURL != nil),
                exportHint: raw.exportHint,
                localURL: localURL,
                sourceIndex: index
            )
        }
    }

    private func normalizeAttachments(_ attachments: [RawAttachment], archive: SnapshotPackageArchive, sourceURL: URL) -> [SnapshotAttachmentIndex] {
        attachments.enumerated().compactMap { index, raw -> SnapshotAttachmentIndex? in
            guard let id = raw.id, let taskId = raw.taskId, let fileName = raw.fileName, let relativePath = raw.relativePath else {
                return nil
            }

            let originalExistsInArchive = raw.packagePath.map(archive.containsEntry(named:)) ?? false
            let previewExistsInArchive = raw.previewPath.map(archive.containsEntry(named:)) ?? false
            let readablePackagePath = originalExistsInArchive
                ? raw.packagePath
                : previewExistsInArchive ? raw.previewPath : nil
            let cachedFileName = readablePackagePath.map { ($0 as NSString).lastPathComponent } ?? fileName
            let localURL = readablePackagePath != nil
                ? cachedAttachmentURL(
                    id: id,
                    fileName: cachedFileName,
                    expectedSize: originalExistsInArchive ? raw.sizeBytes : nil,
                    sourceURL: sourceURL
                )
                : nil

            return SnapshotAttachmentIndex(
                id: id,
                taskId: taskId,
                fileName: fileName,
                originalFileName: raw.originalFileName,
                displayName: raw.displayName,
                relativePath: relativePath,
                packagePath: readablePackagePath,
                previewAvailable: raw.previewAvailable ?? previewExistsInArchive,
                originalAvailableInLiveSync: raw.originalAvailableInLiveSync ?? false,
                originalDownloadMode: raw.originalDownloadMode,
                reason: raw.reason,
                contentType: raw.contentType,
                sizeBytes: raw.sizeBytes,
                isImportant: raw.isImportant ?? false,
                fileExists: raw.fileExists ?? false,
                existsInSnapshot: originalExistsInArchive,
                exportHint: raw.exportHint,
                localURL: localURL,
                sourceIndex: index
            )
        }
    }

    private func extractAttachment(_ attachment: SnapshotAttachmentIndex, archive: SnapshotPackageArchive, sourceURL: URL) throws -> URL {
        let cachedFileName = attachment.packagePath.map { ($0 as NSString).lastPathComponent } ?? attachment.fileName
        if let cachedURL = cachedAttachmentURL(
            id: attachment.id,
            fileName: cachedFileName,
            expectedSize: attachment.existsInSnapshot ? attachment.sizeBytes : nil,
            sourceURL: sourceURL
        ) {
            return cachedURL
        }

        guard let packagePath = attachment.packagePath,
              let data = archive.data(named: packagePath) else {
            throw SnapshotAttachmentError.missingFromSnapshot
        }

        do {
            let attachmentsDirectory = try SnapshotLocalStorage.attachmentsDirectory(forSnapshot: sourceURL)
            let attachmentDirectory = attachmentsDirectory.appendingPathComponent(sanitizedPathComponent(attachment.id), isDirectory: true)
            try FileManager.default.createDirectory(at: attachmentDirectory, withIntermediateDirectories: true)
            let destinationURL = attachmentDirectory.appendingPathComponent(sanitizedFileName(cachedFileName), isDirectory: false)
            try data.write(to: destinationURL, options: [.atomic])
            return destinationURL
        } catch {
            throw SnapshotAttachmentError.extractionFailed(error.localizedDescription)
        }
    }

    private func cachedAttachmentURL(id: String, fileName: String, expectedSize: Int64?, sourceURL: URL) -> URL? {
        guard let attachmentsDirectory = try? SnapshotLocalStorage.attachmentsDirectory(forSnapshot: sourceURL) else {
            return nil
        }
        let url = attachmentsDirectory
            .appendingPathComponent(sanitizedPathComponent(id), isDirectory: true)
            .appendingPathComponent(sanitizedFileName(fileName), isDirectory: false)
        guard FileManager.default.fileExists(atPath: url.path) else {
            return nil
        }
        guard let expectedSize else {
            return url
        }
        let attributes = try? FileManager.default.attributesOfItem(atPath: url.path)
        let actualSize = (attributes?[.size] as? NSNumber)?.int64Value
        return actualSize == expectedSize ? url : nil
    }

    private func sanitizedFileName(_ value: String) -> String {
        let fileName = sanitizedPathComponent(value)
        return fileName.isEmpty ? "Anhang" : fileName
    }

    private func sanitizedPathComponent(_ value: String) -> String {
        let invalidCharacters = CharacterSet(charactersIn: "/\\?%*|\"<>:")
            .union(.controlCharacters)
            .union(.newlines)
        let components = value.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        let result = components.joined(separator: "_").trimmingCharacters(in: .whitespacesAndNewlines)
        return result.isEmpty ? "Anhang" : result
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
    let originalFileName: String?
    let displayName: String?
    let relativePath: String?
    let packagePath: String?
    let previewPath: String?
    let previewAvailable: Bool?
    let originalAvailableInLiveSync: Bool?
    let originalDownloadMode: String?
    let reason: String?
    let contentType: String?
    let sizeBytes: Int64?
    let isImportant: Bool?
    let fileExists: Bool?
    let existsInSnapshot: Bool?
    let exportHint: String?
}

private struct SnapshotPackageArchive {
    private struct Entry {
        let name: String
        let normalizedName: String
        let compressionMethod: UInt16
        let dataOffset: Int
        let compressedSize: Int
        let uncompressedSize: Int
    }

    private let sourceURL: URL
    private let fileName: String
    private let fileSize: Int64
    private let hasLocalFileHeaderSignature: Bool
    private let entries: [Entry]
    private let rootNames: [String]
    private let nestedNames: [String]
    private let decodeIssues: [String]
    private let parseFailed: Bool

    init(contentsOf url: URL) throws {
        sourceURL = url
        fileName = url.lastPathComponent
        if let attributes = try? FileManager.default.attributesOfItem(atPath: url.path),
           let sizeNumber = attributes[.size] as? NSNumber {
            fileSize = sizeNumber.int64Value
        } else {
            fileSize = -1
        }
        let data = try Data(contentsOf: url, options: [.mappedIfSafe])
        hasLocalFileHeaderSignature = data.count >= 4 && Self.readUInt32LE(data, 0) == 0x0403_4B50

        var decodeIssues: [String] = []
        let parsedEntries = Self.parseEntries(from: data, fileName: fileName, decodeIssues: &decodeIssues)
        entries = parsedEntries.entries
        rootNames = parsedEntries.rootNames
        nestedNames = parsedEntries.nestedNames
        self.decodeIssues = decodeIssues
        parseFailed = parsedEntries.parseFailed
    }

    func data(named fileName: String) -> Data? {
        guard let entry = entry(named: fileName),
              let archiveData = try? Data(contentsOf: sourceURL, options: [.mappedIfSafe]),
              entry.dataOffset >= 0,
              entry.compressedSize >= 0,
              entry.dataOffset + entry.compressedSize <= archiveData.count else {
            return nil
        }

        let compressedData = archiveData.subdata(in: entry.dataOffset ..< entry.dataOffset + entry.compressedSize)
        switch entry.compressionMethod {
        case 0:
            return compressedData
        case 8:
            return Self.inflate(data: compressedData, expectedSize: entry.uncompressedSize)
        default:
            return nil
        }
    }

    func containsEntry(named fileName: String) -> Bool {
        entry(named: fileName) != nil
    }

    private func entry(named fileName: String) -> Entry? {
        let normalizedFileName = fileName.lowercased()
        if let exact = entries.first(where: { $0.normalizedName == normalizedFileName }) {
            return exact
        }

        if let suffixMatch = entries.first(where: { $0.normalizedName.hasSuffix("/" + normalizedFileName) }) {
            return suffixMatch
        }

        return entries.first(where: { $0.name.caseInsensitiveCompare(fileName) == .orderedSame })
    }

    var summary: String {
        let fileSizeText: String
        if fileSize >= 0 {
            fileSizeText = "\(fileSize) bytes"
        } else {
            fileSizeText = "unbekannt"
        }

        let signatureText = hasLocalFileHeaderSignature ? "ja" : "nein"
        let rootText = rootNames.isEmpty ? "keine" : rootNames.joined(separator: ", ")
        let nestedText = nestedNames.isEmpty ? "keine" : nestedNames.joined(separator: ", ")
        let issueText = decodeIssues.isEmpty ? "keine" : decodeIssues.joined(separator: " | ")
        let parseText = parseFailed ? "ja" : "nein"

        return """
        Datei vorhanden: ja
        Dateigröße: \(fileSizeText)
        ZIP-Signatur erkannt: \(signatureText)
        Parsing abgebrochen: \(parseText)
        Gefundene Einträge: \(entries.count)
        Root-Einträge: \(rootText)
        Zusätzliche Pfade: \(nestedText)
        Decode-Hinweise: \(issueText)
        """
    }

    func diagnostics(forMissingFile fileName: String) -> String {
        let matchingPaths = entries
            .map(\.name)
            .filter { path in
                path.caseInsensitiveCompare(fileName) == .orderedSame
                    || path.lowercased().hasSuffix("/" + fileName.lowercased())
            }
        let matchingText = matchingPaths.isEmpty ? "keine" : matchingPaths.joined(separator: ", ")
        return """
        \(summary)
        Gesucht: \(fileName)
        Ähnliche Einträge: \(matchingText)
        """
    }

    private static func parseEntries(from data: Data, fileName: String, decodeIssues: inout [String]) -> (entries: [Entry], rootNames: [String], nestedNames: [String], parseFailed: Bool) {
        var offset = 0
        var result: [Entry] = []
        var rootNames: [String] = []
        var nestedNames: [String] = []
        var parseFailed = false

        while offset + 4 <= data.count {
            let signature = readUInt32LE(data, offset)
            guard signature == 0x0403_4B50 else {
                break
            }

            guard offset + 30 <= data.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Lokaler ZIP-Header ist unvollständig.")
                parseFailed = true
                break
            }

            let compressionMethod = readUInt16LE(data, offset + 8)
            let compressedSize = Int(readUInt32LE(data, offset + 18))
            let uncompressedSize = Int(readUInt32LE(data, offset + 22))
            let fileNameLength = Int(readUInt16LE(data, offset + 26))
            let extraFieldLength = Int(readUInt16LE(data, offset + 28))

            offset += 30

            guard offset + fileNameLength + extraFieldLength <= data.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Dateiname oder Zusatzdaten sind unvollständig.")
                parseFailed = true
                break
            }

            let fileNameData = data.subdata(in: offset ..< offset + fileNameLength)
            offset += fileNameLength
            offset += extraFieldLength

            guard offset + compressedSize <= data.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Dateigröße passt nicht zum Paketinhalt.")
                parseFailed = true
                break
            }

            let dataOffset = offset
            offset += compressedSize

            let fileName = String(data: fileNameData, encoding: .utf8) ?? String(decoding: fileNameData, as: UTF8.self)
            let normalizedName = fileName.lowercased()
            switch compressionMethod {
            case 0:
                break
            case 8:
                break
            default:
                decodeIssues.append("Eintrag \(fileName): Kompressionsmethode \(compressionMethod) wird nicht unterstützt.")
                parseFailed = true
                continue
            }

            if fileName.hasSuffix("/") {
                rootNames.append(fileName)
            } else if !fileName.contains("/") {
                rootNames.append(fileName)
            } else {
                nestedNames.append(fileName)
            }

            result.append(Entry(
                name: fileName,
                normalizedName: normalizedName,
                compressionMethod: compressionMethod,
                dataOffset: dataOffset,
                compressedSize: compressedSize,
                uncompressedSize: uncompressedSize
            ))
        }

        if result.isEmpty {
            decodeIssues.append("Es wurden keine lesbaren Einträge gefunden.")
            parseFailed = true
        }

        return (result, rootNames, nestedNames, parseFailed)
    }

    private static func inflate(data: Data, expectedSize: Int) -> Data? {
        guard !data.isEmpty else {
            return Data()
        }

        var sourceBuffer = Array(data)
        let sourceSize = sourceBuffer.count
        let destinationCapacity = max(expectedSize, max(sourceSize * 4, sourceSize + 1024))
        var destinationBuffer = [UInt8](repeating: 0, count: destinationCapacity)

        let decodedSize = destinationBuffer.withUnsafeMutableBytes { destinationPointer in
            sourceBuffer.withUnsafeMutableBytes { sourcePointer in
                compression_decode_buffer(
                    destinationPointer.bindMemory(to: UInt8.self).baseAddress!,
                    destinationCapacity,
                    sourcePointer.bindMemory(to: UInt8.self).baseAddress!,
                    sourceSize,
                    nil,
                    COMPRESSION_ZLIB
                )
            }
        }

        guard decodedSize > 0 else {
            return nil
        }

        return Data(destinationBuffer.prefix(decodedSize))
    }

    private static func readUInt16LE(_ data: Data, _ offset: Int) -> UInt16 {
        UInt16(data[offset]) | (UInt16(data[offset + 1]) << 8)
    }

    private static func readUInt32LE(_ data: Data, _ offset: Int) -> UInt32 {
        UInt32(data[offset])
            | (UInt32(data[offset + 1]) << 8)
            | (UInt32(data[offset + 2]) << 16)
            | (UInt32(data[offset + 3]) << 24)
    }
}
