import CryptoKit
import Foundation

enum LocalNetworkManualSyncPhase: String, CaseIterable, Sendable {
    case searchingDesktop = "Desktop wird gesucht"
    case connecting = "Verbindung wird hergestellt"
    case checkingPairing = "Kopplung wird geprüft"
    case comparingData = "Daten werden verglichen"
    case transferringData = "Daten werden übertragen"
    case transferringPhotos = "Fotos werden übertragen"
    case confirming = "Übertragung wird bestätigt"
    case completed = "Synchronisation abgeschlossen"

    var progress: Double {
        guard let index = Self.allCases.firstIndex(of: self) else { return 0 }
        return Double(index + 1) / Double(Self.allCases.count)
    }
}

struct LocalNetworkManualSyncSummary: Equatable, Sendable {
    var transferredObjects = 0
    var transferredPhotos = 0
    var transferredFiles = 0
    var skippedObjects = 0
    var skippedFiles = 0
    var failedObjects = 0
    var messages: [String] = []

    var completionText: String {
        var parts = [
            "Übertragene Entwürfe: \(transferredObjects)",
            "Fotos: \(transferredPhotos)",
            "Übersprungen: \(skippedObjects)"
        ]
        if failedObjects > 0 {
            parts.append("Fehlgeschlagen: \(failedObjects)")
        }
        return parts.joined(separator: " · ")
    }
}

enum LocalNetworkManualSyncError: LocalizedError, Sendable {
    case desktopMissing
    case invalidAddress
    case desktopUnavailable
    case desktopNotFound
    case connectionRejected
    case timeout
    case pairingMissing
    case pairingPending
    case pairingInvalid
    case pairingRevoked
    case invalidResponse
    case mobileInboxUnavailable
    case desktopStorageUnavailable
    case fileMissing
    case fileUnreadable
    case invalidPackage(String)
    case transferInterrupted

    var errorDescription: String? {
        switch self {
        case .desktopMissing:
            "Noch kein Desktop vorgemerkt. Bitte zuerst den Desktop in den Sync-Einstellungen auswählen."
        case .invalidAddress:
            "Die gespeicherte Desktop-Adresse ist ungültig. Bitte den Desktop erneut prüfen."
        case .desktopUnavailable:
            "Der Desktop-Sync-Dienst ist nicht erreichbar. Beide Geräte müssen im selben lokalen Netzwerk sein und der Dienst muss am Desktop laufen."
        case .desktopNotFound:
            "Der gespeicherte Desktop wurde nicht gefunden. Bitte Netzwerk und Desktop-Adresse prüfen oder den Desktop erneut auswählen."
        case .connectionRejected:
            "Der Desktop hat die Verbindung abgelehnt. Bitte Sync-Dienst und Gerätefreigabe am Desktop prüfen."
        case .timeout:
            "Die Zeitüberschreitung wurde erreicht. Bitte prüfen, ob beide Geräte im selben Netzwerk sind, und erneut synchronisieren."
        case .pairingMissing:
            "Das iPad ist am Desktop noch nicht vorgemerkt. Bitte ‚Diesen Desktop verwenden‘ erneut ausführen."
        case .pairingPending:
            "Die Kopplung wartet auf Freigabe. Bitte dieses iPad in BüroCockpit unter ‚Lokaler Netzwerk-Sync‘ freigeben."
        case .pairingInvalid:
            "Der Kopplungsnachweis ist ungültig. Bitte den Desktop erneut auswählen und das iPad am Desktop freigeben."
        case .pairingRevoked:
            "Die Freigabe dieses iPads wurde am Desktop widerrufen."
        case .invalidResponse:
            "Der Desktop hat eine ungültige oder unvollständige Antwort geliefert."
        case .mobileInboxUnavailable:
            "Die lokalen mobilen Eingänge sind nicht verfügbar. Bitte den Mobile-Inbox-Ordner erneut auswählen."
        case .desktopStorageUnavailable:
            "Der Desktop konnte die Dateien nicht sicher speichern. Bitte freien Speicher und Datenordner am Desktop prüfen."
        case .fileMissing:
            "Eine zu übertragende Datei ist nicht mehr vorhanden. Der mobile Eingang bleibt erhalten und kann nach Korrektur erneut übertragen werden."
        case .fileUnreadable:
            "Eine Datei oder ein Foto konnte nicht gelesen werden. Bitte die betreffende Anlage im mobilen Eingang prüfen."
        case .invalidPackage(let message):
            "Ein mobiler Eingang ist unvollständig: \(message)"
        case .transferInterrupted:
            "Die Übertragung wurde unterbrochen. Die lokalen Daten bleiben erhalten; ein neuer manueller Versuch ist möglich."
        }
    }
}

final class LocalNetworkManualSyncClient: @unchecked Sendable {
    private let mobileInboxStore: MobileInboxStore
    private let fileManager: FileManager
    private let session: URLSession
    private let maximumFileCount = 250
    private let maximumFileSize = 100 * 1024 * 1024
    private let maximumPackageSize = 220 * 1024 * 1024

    init(
        mobileInboxStore: MobileInboxStore = MobileInboxStore(),
        fileManager: FileManager = .default,
        session: URLSession = .shared
    ) {
        self.mobileInboxStore = mobileInboxStore
        self.fileManager = fileManager
        self.session = session
    }

    func synchronize(
        address: String,
        port: Int,
        deviceId: String,
        trustKey: String,
        progress: @escaping @Sendable (LocalNetworkManualSyncPhase) async -> Void
    ) async throws -> LocalNetworkManualSyncSummary {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalizedAddress.isEmpty else { throw LocalNetworkManualSyncError.desktopMissing }
        guard isValidHost(normalizedAddress), port >= 1024, port <= 65535 else {
            throw LocalNetworkManualSyncError.invalidAddress
        }

        await progress(.searchingDesktop)
        await progress(.connecting)
        try await checkDesktop(address: normalizedAddress, port: port)

        await progress(.checkingPairing)
        try await checkPairing(address: normalizedAddress, port: port, deviceId: deviceId, trustKey: trustKey)

        await progress(.comparingData)
        let selectedFolder: URL
        do {
            selectedFolder = try mobileInboxStore.resolveSelectedFolder()
        } catch {
            throw LocalNetworkManualSyncError.mobileInboxUnavailable
        }
        guard selectedFolder.startAccessingSecurityScopedResource() else {
            throw LocalNetworkManualSyncError.mobileInboxUnavailable
        }
        defer { selectedFolder.stopAccessingSecurityScopedResource() }

        let entryURLs: [URL]
        do {
            entryURLs = try pendingEntryDirectories(in: mobileInboxStore.mobileInboxURL(for: selectedFolder))
        } catch let error as LocalNetworkManualSyncError {
            throw error
        } catch let error as CocoaError where error.code == .fileNoSuchFile || error.code == .fileReadNoSuchFile {
            throw LocalNetworkManualSyncError.fileMissing
        } catch {
            throw LocalNetworkManualSyncError.fileUnreadable
        }
        var summary = LocalNetworkManualSyncSummary()
        for entryURL in entryURLs {
            try Task.checkCancellation()
            let package: UploadPackage
            do {
                guard let builtPackage = try buildPackage(from: entryURL, deviceId: deviceId) else {
                    continue
                }
                package = builtPackage
            } catch let error as LocalNetworkManualSyncError {
                summary.failedObjects += 1
                summary.messages.append(error.localizedDescription)
                continue
            } catch let error as CocoaError where error.code == .fileNoSuchFile || error.code == .fileReadNoSuchFile {
                summary.failedObjects += 1
                summary.messages.append(LocalNetworkManualSyncError.fileMissing.localizedDescription)
                continue
            } catch {
                summary.failedObjects += 1
                summary.messages.append(LocalNetworkManualSyncError.fileUnreadable.localizedDescription)
                continue
            }
            await progress(.transferringData)
            if package.originalPhotoCount > 0 {
                await progress(.transferringPhotos)
            }
            let result = try await upload(
                package,
                address: normalizedAddress,
                port: port,
                deviceId: deviceId,
                trustKey: trustKey
            )
            await progress(.confirming)
            summary.transferredObjects += result.transferredObjects
            summary.transferredPhotos += result.transferredPhotos
            summary.transferredFiles += result.transferredFiles
            summary.skippedObjects += result.skippedObjects
            summary.skippedFiles += result.skippedFiles
            summary.failedObjects += result.failedObjects
            summary.messages.append(contentsOf: result.messages)
        }

        await progress(.completed)
        return summary
    }

    private func checkDesktop(address: String, port: Int) async throws {
        let request = try makeRequest(address: address, port: port, path: "/local-sync/status", method: "GET")
        do {
            let (data, response) = try await session.data(for: request)
            guard let response = response as? HTTPURLResponse, (200...299).contains(response.statusCode) else {
                throw LocalNetworkManualSyncError.desktopUnavailable
            }
            let status = try JSONDecoder().decode(DesktopStatusResponse.self, from: data)
            guard status.app == "BueroCockpit", status.status == "ok" else {
                throw LocalNetworkManualSyncError.invalidResponse
            }
        } catch let error as LocalNetworkManualSyncError {
            throw error
        } catch let error as URLError {
            throw networkError(for: error)
        } catch {
            throw LocalNetworkManualSyncError.desktopUnavailable
        }
    }

    private func checkPairing(address: String, port: Int, deviceId: String, trustKey: String) async throws {
        var request = try makeRequest(address: address, port: port, path: "/local-sync/pairing/status", method: "GET")
        authorize(&request, deviceId: deviceId, trustKey: trustKey)
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: request)
        } catch let error as URLError {
            throw networkError(for: error)
        } catch {
            throw LocalNetworkManualSyncError.desktopUnavailable
        }
        guard let response = response as? HTTPURLResponse,
              let pairing = try? JSONDecoder().decode(PairingStatusResponse.self, from: data) else {
            throw LocalNetworkManualSyncError.invalidResponse
        }
        guard (200...299).contains(response.statusCode), pairing.status == "trusted" else {
            throw pairingError(for: pairing.status)
        }
    }

    private func upload(
        _ package: UploadPackage,
        address: String,
        port: Int,
        deviceId: String,
        trustKey: String
    ) async throws -> UploadResponse {
        var request = try makeRequest(address: address, port: port, path: "/local-sync/mobile-inbox", method: "POST")
        authorize(&request, deviceId: deviceId, trustKey: trustKey)
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.timeoutInterval = 120
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        request.httpBody = try encoder.encode(package.request)

        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: request)
        } catch is CancellationError {
            throw CancellationError()
        } catch let error as URLError {
            throw networkError(for: error, duringTransfer: true)
        } catch {
            throw LocalNetworkManualSyncError.transferInterrupted
        }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        guard let response = response as? HTTPURLResponse else {
            throw LocalNetworkManualSyncError.invalidResponse
        }
        guard let result = try? decoder.decode(UploadResponse.self, from: data) else {
            if response.statusCode == 403 { throw LocalNetworkManualSyncError.connectionRejected }
            if response.statusCode == 413 { throw LocalNetworkManualSyncError.invalidPackage("Das Paket überschreitet das Desktop-Größenlimit.") }
            if response.statusCode == 503 || response.statusCode >= 500 {
                throw LocalNetworkManualSyncError.desktopStorageUnavailable
            }
            throw LocalNetworkManualSyncError.invalidResponse
        }
        if response.statusCode == 409 {
            return result
        }
        guard (200...299).contains(response.statusCode), result.status == "accepted" || result.status == "skipped" else {
            throw LocalNetworkManualSyncError.invalidPackage(result.messages.first ?? "Desktop hat das Paket abgelehnt.")
        }
        return result
    }

    private func pendingEntryDirectories(in inboxURL: URL) throws -> [URL] {
        guard fileManager.fileExists(atPath: inboxURL.path) else { return [] }
        let directories = try fileManager.contentsOfDirectory(
            at: inboxURL,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        )
        return directories
            .filter { $0.lastPathComponent.hasPrefix("mobile-") }
            .sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
    }

    private func buildPackage(from entryURL: URL, deviceId: String) throws -> UploadPackage? {
        let taskURL = entryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
        guard fileManager.fileExists(atPath: taskURL.path) else {
            throw LocalNetworkManualSyncError.invalidPackage("aufgabe.json fehlt in \(entryURL.lastPathComponent).")
        }
        let taskData = try Data(contentsOf: taskURL, options: [.mappedIfSafe])
        let task = try JSONDecoder().decode(MinimalMobileTask.self, from: taskData)
        guard task.status.caseInsensitiveCompare("new") == .orderedSame else { return nil }
        guard isStableIdentifier(task.id) else {
            throw LocalNetworkManualSyncError.invalidPackage("Stabile Objekt-ID ist ungültig.")
        }

        guard let enumerator = fileManager.enumerator(
            at: entryURL,
            includingPropertiesForKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey],
            options: [.skipsHiddenFiles]
        ) else {
            throw LocalNetworkManualSyncError.invalidPackage("Dateien konnten nicht gelesen werden.")
        }

        var files: [UploadFile] = []
        var totalSize = 0
        for case let fileURL as URL in enumerator {
            let values = try fileURL.resourceValues(forKeys: [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey])
            guard values.isRegularFile == true, values.isSymbolicLink != true else { continue }
            let relativePath = relativePath(for: fileURL, below: entryURL)
            guard isAllowedRelativePath(relativePath) else {
                throw LocalNetworkManualSyncError.invalidPackage("Unzulässiger Dateipfad: \(relativePath)")
            }
            let size = values.fileSize ?? 0
            guard size > 0, size <= maximumFileSize, totalSize <= maximumPackageSize - size else {
                throw LocalNetworkManualSyncError.invalidPackage("Datei oder Gesamtpaket ist zu groß: \(relativePath)")
            }
            totalSize += size
            let data = try Data(contentsOf: fileURL, options: [.mappedIfSafe])
            files.append(UploadFile(
                relativePath: relativePath,
                contentType: contentType(for: fileURL),
                sizeBytes: Int64(data.count),
                sha256: SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined(),
                purpose: purpose(for: relativePath),
                data: data
            ))
        }
        guard !files.isEmpty, files.count <= maximumFileCount else {
            throw LocalNetworkManualSyncError.invalidPackage("Dateianzahl ist ungültig.")
        }
        guard files.contains(where: { $0.relativePath.caseInsensitiveCompare("aufgabe.json") == .orderedSame }) else {
            throw LocalNetworkManualSyncError.invalidPackage("aufgabe.json wurde nicht paketiert.")
        }

        let request = UploadRequest(
            uploadId: task.id,
            deviceId: deviceId,
            schemaVersion: "local-sync-inbox-v1",
            createdAtUtc: Date(),
            files: files
        )
        return UploadPackage(
            request: request,
            originalPhotoCount: files.filter { $0.purpose == "original-photo" }.count
        )
    }

    private func makeRequest(address: String, port: Int, path: String, method: String) throws -> URLRequest {
        var components = URLComponents()
        components.scheme = "http"
        components.host = address
        components.port = port
        components.path = path
        guard let url = components.url else { throw LocalNetworkManualSyncError.invalidAddress }
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.timeoutInterval = 8
        return request
    }

    private func authorize(_ request: inout URLRequest, deviceId: String, trustKey: String) {
        request.setValue(deviceId, forHTTPHeaderField: "X-BueroCockpit-Device-Id")
        request.setValue(trustKey, forHTTPHeaderField: "X-BueroCockpit-Trust-Key")
    }

    private func pairingError(for status: String) -> LocalNetworkManualSyncError {
        switch status {
        case "pending": .pairingPending
        case "revoked": .pairingRevoked
        case "invalid": .pairingInvalid
        default: .pairingMissing
        }
    }

    private func networkError(for error: URLError, duringTransfer: Bool = false) -> LocalNetworkManualSyncError {
        switch error.code {
        case .cannotFindHost, .dnsLookupFailed:
            return .desktopNotFound
        case .timedOut:
            return .timeout
        case .cannotConnectToHost:
            return .connectionRejected
        case .networkConnectionLost:
            return duringTransfer ? .transferInterrupted : .desktopUnavailable
        case .notConnectedToInternet:
            return .desktopUnavailable
        default:
            return duringTransfer ? .transferInterrupted : .desktopUnavailable
        }
    }

    private func relativePath(for fileURL: URL, below entryURL: URL) -> String {
        let root = entryURL.standardizedFileURL.path.hasSuffix("/")
            ? entryURL.standardizedFileURL.path
            : entryURL.standardizedFileURL.path + "/"
        return String(fileURL.standardizedFileURL.path.dropFirst(root.count)).replacingOccurrences(of: "\\", with: "/")
    }

    private func isAllowedRelativePath(_ path: String) -> Bool {
        guard !path.isEmpty, !path.hasPrefix("/"), !path.split(separator: "/").contains("..") else { return false }
        if path.caseInsensitiveCompare("aufgabe.json") == .orderedSame { return true }
        guard let first = path.split(separator: "/").first.map(String.init) else { return false }
        return ["originals", "previews", "annotated", "sketches", "files"].contains(first)
    }

    private func purpose(for path: String) -> String {
        if path.hasPrefix("originals/") { return "original-photo" }
        if path.hasPrefix("previews/") { return "photo-preview" }
        if path.hasPrefix("annotated/") { return "annotated-photo" }
        if path.hasPrefix("sketches/") { return path.hasSuffix(".pkdrawing") ? "editable-sketch" : "sketch" }
        if path.hasPrefix("files/") { return "attachment" }
        return "manifest"
    }

    private func contentType(for url: URL) -> String {
        switch url.pathExtension.lowercased() {
        case "json": "application/json"
        case "jpg", "jpeg": "image/jpeg"
        case "png": "image/png"
        case "heic", "heif": "image/heic"
        case "pdf": "application/pdf"
        case "pkdrawing": "application/x-pkdrawing"
        default: "application/octet-stream"
        }
    }

    private func isValidHost(_ value: String) -> Bool {
        !value.contains("/") && !value.contains(":") && !value.contains("?") && !value.contains("#")
    }

    private func isStableIdentifier(_ value: String) -> Bool {
        guard !value.isEmpty, value.count <= 128 else { return false }
        return value.unicodeScalars.allSatisfy {
            CharacterSet.alphanumerics.contains($0) || "._-".unicodeScalars.contains($0)
        }
    }
}

private struct DesktopStatusResponse: Decodable, Sendable {
    let app: String
    let status: String
}

private struct PairingStatusResponse: Decodable, Sendable {
    let status: String
}

private struct MinimalMobileTask: Decodable, Sendable {
    let id: String
    let status: String
}

private struct UploadPackage: Sendable {
    let request: UploadRequest
    let originalPhotoCount: Int
}

private struct UploadRequest: Encodable, Sendable {
    let uploadId: String
    let deviceId: String
    let schemaVersion: String
    let createdAtUtc: Date
    let files: [UploadFile]
}

private struct UploadFile: Encodable, Sendable {
    let relativePath: String
    let contentType: String
    let sizeBytes: Int64
    let sha256: String
    let purpose: String
    let data: Data
}

private struct UploadResponse: Decodable, Sendable {
    let status: String
    let uploadId: String
    let inboxEntryId: String?
    let receivedAtUtc: Date
    let transferredObjects: Int
    let transferredPhotos: Int
    let transferredFiles: Int
    let skippedObjects: Int
    let skippedFiles: Int
    let failedObjects: Int
    let messages: [String]
}
