import Foundation
import Compression

final class SnapshotReader: @unchecked Sendable {
    func readSnapshot(from sourceURL: URL) throws -> SnapshotDocument {
        SnapshotPerformanceLog.event("Reader started")
        let accessGranted = sourceURL.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                sourceURL.stopAccessingSecurityScopedResource()
            }
        }

        if isSnapshotPackage(sourceURL) {
            let localPackageURL = try stageSnapshotPackageToSandbox(from: sourceURL)
            return try readSnapshotPackage(from: localPackageURL)
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
            let attachments = decodeOptionalPackageAttachments(in: archive)
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

    private func stageSnapshotPackageToSandbox(from sourceURL: URL) throws -> URL {
        SnapshotPerformanceLog.event("Local copy started")
        guard sourceURL.startAccessingSecurityScopedResource() else {
            throw SnapshotReaderError.securityScopedAccessDenied(sourceURL.lastPathComponent)
        }
        defer {
            sourceURL.stopAccessingSecurityScopedResource()
        }

        let snapshotsDirectory = try localSnapshotsDirectory()
        let destinationURL = snapshotsDirectory.appendingPathComponent(sourceURL.lastPathComponent, isDirectory: false)

        let sourceInfo = try? FileManager.default.attributesOfItem(atPath: sourceURL.path)
        let sourceExists = FileManager.default.fileExists(atPath: sourceURL.path)
        let sourceSize = (sourceInfo?[.size] as? NSNumber)?.int64Value

        var coordinationError: NSError?
        var readError: Error?
        var writeError: Error?
        var copiedBytes: Int64?
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(readingItemAt: sourceURL, options: [.withoutChanges], error: &coordinationError) { readableURL in
            do {
                let data = try Data(contentsOf: readableURL, options: [.mappedIfSafe])
                copiedBytes = Int64(data.count)
                try data.write(to: destinationURL, options: [.atomic])
            } catch {
                if copiedBytes == nil {
                    readError = error
                } else {
                    writeError = error
                }
            }
        }

        if let coordinationError {
            throw SnapshotReaderError.unreadableSnapshotPackage(
                sourceURL.lastPathComponent,
                """
                Original-Dateiname: \(sourceURL.lastPathComponent)
                Lokale Ziel-URL: \(destinationURL.path)
                Quelle vorhanden: \(sourceExists ? "ja" : "nein")
                Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")
                Koordination: \(coordinationError.localizedDescription)
                """
            )
        }

        if let readError {
            throw SnapshotReaderError.unreadableSnapshotPackage(
                sourceURL.lastPathComponent,
                """
                Original-Dateiname: \(sourceURL.lastPathComponent)
                Lokale Ziel-URL: \(destinationURL.path)
                Quelle vorhanden: \(sourceExists ? "ja" : "nein")
                Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")
                Lesen der OneDrive-Datei fehlgeschlagen: \(readError.localizedDescription)
                """
            )
        }

        if let writeError {
            throw SnapshotReaderError.localCopyFailed(
                """
                \(sourceURL.lastPathComponent)
                Lokale Ziel-URL: \(destinationURL.path)
                Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")
                Schreiben der lokalen Kopie fehlgeschlagen: \(writeError.localizedDescription)
                """
            )
        }

        let destinationExists = FileManager.default.fileExists(atPath: destinationURL.path)
        let destinationAttributes = try? FileManager.default.attributesOfItem(atPath: destinationURL.path)
        let destinationSize = (destinationAttributes?[.size] as? NSNumber)?.int64Value
        guard destinationExists, let destinationSize, destinationSize > 0 else {
            throw SnapshotReaderError.unreadableSnapshotPackage(
                sourceURL.lastPathComponent,
                """
                Original-Dateiname: \(sourceURL.lastPathComponent)
                Lokale Ziel-URL: \(destinationURL.path)
                Quelle vorhanden: \(sourceExists ? "ja" : "nein")
                Quellgröße: \(sourceSize.map { "\($0)" } ?? "unbekannt")
                Lokale Datei vorhanden: \(destinationExists ? "ja" : "nein")
                Lokale Dateigröße: \(String(describing: destinationSize))
                Die lokale Paketdatei wurde nicht erfolgreich geschrieben.
                """
            )
        }

        SnapshotPerformanceLog.event("Local copy finished")
        return destinationURL
    }

    private func localSnapshotsDirectory() throws -> URL {
        try SnapshotLocalStorage.snapshotsDirectory()
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

            let localURL = raw.packagePath.flatMap { packagePath -> URL? in
                let candidateURL = folderURL.appendingPathComponent(packagePath)
                return FileManager.default.fileExists(atPath: candidateURL.path) ? candidateURL : nil
            }

            return SnapshotAttachmentIndex(
                id: id,
                taskId: taskId,
                fileName: fileName,
                relativePath: relativePath,
                packagePath: raw.packagePath,
                contentType: raw.contentType,
                sizeBytes: raw.sizeBytes,
                isImportant: raw.isImportant ?? false,
                fileExists: raw.fileExists ?? false,
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

            let localURL = extractAttachment(raw, archive: archive, sourceURL: sourceURL)

            return SnapshotAttachmentIndex(
                id: id,
                taskId: taskId,
                fileName: fileName,
                relativePath: relativePath,
                packagePath: raw.packagePath,
                contentType: raw.contentType,
                sizeBytes: raw.sizeBytes,
                isImportant: raw.isImportant ?? false,
                fileExists: raw.fileExists ?? false,
                localURL: localURL,
                sourceIndex: index
            )
        }
    }

    private func extractAttachment(_ raw: RawAttachment, archive: SnapshotPackageArchive, sourceURL: URL) -> URL? {
        guard let id = raw.id,
              let fileName = raw.fileName,
              let packagePath = raw.packagePath,
              let data = archive.data(named: packagePath) else {
            return nil
        }

        do {
            let attachmentsDirectory = try SnapshotLocalStorage.attachmentsDirectory(forSnapshotFileName: sourceURL.lastPathComponent)
            let attachmentDirectory = attachmentsDirectory.appendingPathComponent(sanitizedPathComponent(id), isDirectory: true)
            try FileManager.default.createDirectory(at: attachmentDirectory, withIntermediateDirectories: true)
            let destinationURL = attachmentDirectory.appendingPathComponent(sanitizedFileName(fileName), isDirectory: false)
            try data.write(to: destinationURL, options: [.atomic])
            return destinationURL
        } catch {
            return nil
        }
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
    let relativePath: String?
    let packagePath: String?
    let contentType: String?
    let sizeBytes: Int64?
    let isImportant: Bool?
    let fileExists: Bool?
}

private struct SnapshotPackageArchive {
    private struct Entry {
        let name: String
        let normalizedName: String
        let data: Data
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
        hasLocalFileHeaderSignature = data.count >= 4 && Self.readUInt32LE([UInt8](data), 0) == 0x0403_4B50

        var decodeIssues: [String] = []
        let parsedEntries = Self.parseEntries(from: data, fileName: fileName, decodeIssues: &decodeIssues)
        entries = parsedEntries.entries
        rootNames = parsedEntries.rootNames
        nestedNames = parsedEntries.nestedNames
        self.decodeIssues = decodeIssues
        parseFailed = parsedEntries.parseFailed
    }

    func data(named fileName: String) -> Data? {
        let normalizedFileName = fileName.lowercased()
        if let exact = entries.first(where: { $0.normalizedName == normalizedFileName }) {
            return exact.data
        }

        if let suffixMatch = entries.first(where: { $0.normalizedName.hasSuffix("/" + normalizedFileName) }) {
            return suffixMatch.data
        }

        return entries.first(where: { $0.name.caseInsensitiveCompare(fileName) == .orderedSame })?.data
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
        let bytes = [UInt8](data)
        var offset = 0
        var result: [Entry] = []
        var rootNames: [String] = []
        var nestedNames: [String] = []
        var parseFailed = false

        while offset + 4 <= bytes.count {
            let signature = readUInt32LE(bytes, offset)
            guard signature == 0x0403_4B50 else {
                break
            }

            guard offset + 30 <= bytes.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Lokaler ZIP-Header ist unvollständig.")
                parseFailed = true
                break
            }

            let compressionMethod = readUInt16LE(bytes, offset + 8)
            let compressedSize = Int(readUInt32LE(bytes, offset + 18))
            let fileNameLength = Int(readUInt16LE(bytes, offset + 26))
            let extraFieldLength = Int(readUInt16LE(bytes, offset + 28))

            offset += 30

            guard offset + fileNameLength + extraFieldLength <= bytes.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Dateiname oder Zusatzdaten sind unvollständig.")
                parseFailed = true
                break
            }

            let fileNameData = Data(bytes[offset ..< offset + fileNameLength])
            offset += fileNameLength
            offset += extraFieldLength

            guard offset + compressedSize <= bytes.count else {
                decodeIssues.append("Eintrag \(result.count + 1): Dateigröße passt nicht zum Paketinhalt.")
                parseFailed = true
                break
            }

            let compressedData = Data(bytes[offset ..< offset + compressedSize])
            offset += compressedSize

            let fileName = String(data: fileNameData, encoding: .utf8) ?? String(decoding: fileNameData, as: UTF8.self)
            let normalizedName = fileName.lowercased()
            let fileData: Data
            switch compressionMethod {
            case 0:
                fileData = compressedData
            case 8:
                guard let decoded = Self.inflate(data: compressedData) else {
                    decodeIssues.append("Eintrag \(fileName): Deflate-Entpackung fehlgeschlagen.")
                    parseFailed = true
                    continue
                }
                fileData = decoded
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

            result.append(Entry(name: fileName, normalizedName: normalizedName, data: fileData))
        }

        if result.isEmpty {
            decodeIssues.append("Es wurden keine lesbaren Einträge gefunden.")
            parseFailed = true
        }

        return (result, rootNames, nestedNames, parseFailed)
    }

    private static func inflate(data: Data) -> Data? {
        guard !data.isEmpty else {
            return Data()
        }

        var sourceBuffer = Array(data)
        let sourceSize = sourceBuffer.count
        let destinationCapacity = max(sourceSize * 4, sourceSize + 1024)
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
