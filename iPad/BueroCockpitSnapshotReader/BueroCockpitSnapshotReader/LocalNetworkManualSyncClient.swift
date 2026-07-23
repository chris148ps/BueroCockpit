import CryptoKit
import Foundation

enum LocalNetworkManualSyncPhase: String, CaseIterable, Sendable {
    case searchingDesktop = "Desktop wird gesucht"
    case connecting = "Verbindung wird hergestellt"
    case checkingPairing = "Kopplung wird geprüft"
    case comparingData = "Daten werden verglichen"
    case downloadingDesktopData = "Desktopdaten werden geladen"
    case transferringData = "Daten werden übertragen"
    case transferringPhotos = "Fotos werden übertragen"
    case updatingLocalData = "Lokaler Datenbestand wird aktualisiert"
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
    var receivedObjects = 0
    var receivedNewObjects = 0
    var receivedChangedObjects = 0
    var receivedReferenceObjects = 0
    var receivedAttachments = 0
    var receivedTombstones = 0
    var sentNewObjects = 0
    var sentChangedObjects = 0
    var unchangedObjects = 0
    var unchangedFiles = 0
    var conflicts = 0
    var skippedObjects = 0
    var skippedFiles = 0
    var failedObjects = 0
    var messages: [String] = []
    var downloadedSnapshotURL: URL?
    var delta: LocalNetworkDeltaResponse?
    var ackToken: String?
    var serverRevision: String?
    var serverSequence: Int64?
    var lastConfirmedClientSequence: Int64?

    var completionText: String {
        if receivedObjects == 0 &&
            receivedReferenceObjects == 0 &&
            receivedAttachments == 0 &&
            receivedTombstones == 0 &&
            transferredObjects == 0 &&
            transferredFiles == 0 &&
            failedObjects == 0 &&
            conflicts == 0 {
            return "Keine Änderungen vorhanden"
        }

        var parts = [
            "Empfangen neu/geändert: \(receivedNewObjects)/\(receivedChangedObjects)",
            "Gesendet neu/geändert: \(sentNewObjects)/\(sentChangedObjects)",
            "Anhänge: \(receivedAttachments + transferredFiles)",
            "Unverändert übersprungen: \(unchangedObjects + unchangedFiles)"
        ]
        if receivedReferenceObjects > 0 {
            parts.append("Referenzdaten: \(receivedReferenceObjects)")
        }
        if receivedTombstones > 0 {
            parts.append("Löschungen: \(receivedTombstones)")
        }
        if conflicts > 0 {
            parts.append("Konflikte: \(conflicts)")
        }
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
    case snapshotUnavailable
    case snapshotInvalid
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
        case .snapshotUnavailable:
            "Die Desktopdaten konnten nicht abgerufen werden. Bitte den lokalen Sync-Dienst am Desktop neu starten und erneut versuchen."
        case .snapshotInvalid:
            "Die vom Desktop empfangenen Daten sind unvollständig. Der bisherige lokale Datenbestand bleibt erhalten."
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
        confirmedServerRevision: String?,
        nextClientSequence: Int64,
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
        let delta = try await downloadDelta(
            address: normalizedAddress,
            port: port,
            deviceId: deviceId,
            trustKey: trustKey,
            confirmedServerRevision: confirmedServerRevision
        )
        var downloadedSnapshot: DownloadedSnapshot?
        if delta.requiresFullSync {
            await progress(.downloadingDesktopData)
            downloadedSnapshot = try await downloadSnapshot(
                address: normalizedAddress,
                port: port,
                deviceId: deviceId,
                trustKey: trustKey
            )
        }
        var keepDownloadedSnapshot = false
        defer {
            if !keepDownloadedSnapshot, let url = downloadedSnapshot?.url {
                try? fileManager.removeItem(at: url)
            }
        }

        var summary = LocalNetworkManualSyncSummary(
            receivedObjects: delta.requiresFullSync ? 0 : delta.counts.tasks,
            downloadedSnapshotURL: downloadedSnapshot?.url,
            delta: delta.requiresFullSync ? nil : delta,
            ackToken: downloadedSnapshot?.ackToken ?? delta.ackToken,
            serverRevision: downloadedSnapshot?.serverRevision ?? delta.toRevision,
            serverSequence: downloadedSnapshot?.serverSequence ?? delta.serverSequence,
            lastConfirmedClientSequence: delta.lastConfirmedClientSequence
        )
        summary.receivedReferenceObjects = delta.requiresFullSync ? 0 : delta.counts.categories + delta.counts.technicians
        summary.receivedAttachments = delta.requiresFullSync ? 0 : delta.counts.attachments
        summary.receivedTombstones = delta.requiresFullSync ? 0 : delta.counts.tombstones
        summary.unchangedObjects = delta.requiresFullSync ? 0 : delta.counts.unchangedObjects
        summary.unchangedFiles = delta.requiresFullSync ? 0 : delta.counts.unchangedFiles
        var selectedFolder: URL?
        do {
            selectedFolder = try mobileInboxStore.resolveSelectedFolder()
        } catch MobileInboxError.folderNotSelected {
            summary.messages.append("Keine lokalen mobilen Eingänge zum Senden ausgewählt.")
        } catch {
            throw LocalNetworkManualSyncError.mobileInboxUnavailable
        }
        if let selectedFolder, !selectedFolder.startAccessingSecurityScopedResource() {
            throw LocalNetworkManualSyncError.mobileInboxUnavailable
        }
        defer { selectedFolder?.stopAccessingSecurityScopedResource() }

        let entryURLs: [URL]
        do {
            entryURLs = try selectedFolder.map {
                try pendingEntryDirectories(in: mobileInboxStore.mobileInboxURL(for: $0))
            } ?? []
        } catch let error as LocalNetworkManualSyncError {
            throw error
        } catch let error as CocoaError where error.code == .fileNoSuchFile || error.code == .fileReadNoSuchFile {
            throw LocalNetworkManualSyncError.fileMissing
        } catch {
            throw LocalNetworkManualSyncError.fileUnreadable
        }
        var clientSequence = max(nextClientSequence, (delta.lastConfirmedClientSequence ?? 0) + 1)
        for entryURL in entryURLs {
            try Task.checkCancellation()
            let package: UploadPackage
            do {
                guard let builtPackage = try buildPackage(
                    from: entryURL,
                    deviceId: deviceId,
                    clientSequence: clientSequence
                ) else {
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
            summary.transferredObjects += result.transferredObjects
            summary.transferredPhotos += result.transferredPhotos
            summary.transferredFiles += result.transferredFiles
            summary.skippedObjects += result.skippedObjects
            summary.skippedFiles += result.skippedFiles
            summary.failedObjects += result.failedObjects
            summary.messages.append(contentsOf: result.messages)
            if result.status == "conflict" {
                summary.conflicts += 1
            }
            if result.status == "accepted" || result.status == "skipped" {
                try markPackageTransferred(entryURL, uploadResult: result)
                if result.status == "accepted" {
                    if package.operation == "update" {
                        summary.sentChangedObjects += 1
                    } else {
                        summary.sentNewObjects += 1
                    }
                }
                summary.lastConfirmedClientSequence = clientSequence
                clientSequence += 1
            }
        }

        await progress(.updatingLocalData)
        keepDownloadedSnapshot = true
        return summary
    }

    func confirm(
        address: String,
        port: Int,
        deviceId: String,
        trustKey: String,
        ackToken: String,
        serverRevision: String,
        lastConfirmedClientSequence: Int64?
    ) async throws -> LocalNetworkAckResponse {
        var request = try makeRequest(address: address, port: port, path: "/local-sync/ack", method: "POST")
        authorize(&request, deviceId: deviceId, trustKey: trustKey)
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONEncoder().encode(LocalNetworkAckRequest(
            ackToken: ackToken,
            serverRevision: serverRevision,
            lastConfirmedClientSequence: lastConfirmedClientSequence
        ))
        let (data, response) = try await session.data(for: request)
        guard let response = response as? HTTPURLResponse,
              (200...299).contains(response.statusCode),
              let result = try? JSONDecoder().decode(LocalNetworkAckResponse.self, from: data),
              result.status == "confirmed" else {
            throw LocalNetworkManualSyncError.transferInterrupted
        }
        return result
    }

    private func downloadDelta(
        address: String,
        port: Int,
        deviceId: String,
        trustKey: String,
        confirmedServerRevision: String?
    ) async throws -> LocalNetworkDeltaResponse {
        var components = URLComponents()
        components.scheme = "http"
        components.host = address
        components.port = port
        components.path = "/local-sync/changes"
        if let confirmedServerRevision, !confirmedServerRevision.isEmpty {
            components.queryItems = [URLQueryItem(name: "since", value: confirmedServerRevision)]
        }
        guard let url = components.url else { throw LocalNetworkManualSyncError.invalidAddress }
        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.timeoutInterval = 120
        authorize(&request, deviceId: deviceId, trustKey: trustKey)
        do {
            let (data, response) = try await session.data(for: request)
            guard let response = response as? HTTPURLResponse,
                  (200...299).contains(response.statusCode) else {
                throw LocalNetworkManualSyncError.snapshotUnavailable
            }
            let delta = try JSONDecoder().decode(LocalNetworkDeltaResponse.self, from: data)
            guard delta.apiVersion == "local-sync-delta-v1" else {
                throw LocalNetworkManualSyncError.snapshotInvalid
            }
            return delta
        } catch let error as LocalNetworkManualSyncError {
            throw error
        } catch let error as URLError {
            throw networkError(for: error, duringTransfer: true)
        } catch {
            throw LocalNetworkManualSyncError.snapshotInvalid
        }
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

    private func downloadSnapshot(
        address: String,
        port: Int,
        deviceId: String,
        trustKey: String
    ) async throws -> DownloadedSnapshot {
        var request = try makeRequest(
            address: address,
            port: port,
            path: "/local-sync/snapshot",
            method: "GET"
        )
        authorize(&request, deviceId: deviceId, trustKey: trustKey)
        request.timeoutInterval = 120

        let temporaryDownloadURL: URL
        let response: URLResponse
        do {
            (temporaryDownloadURL, response) = try await session.download(for: request)
        } catch let error as URLError {
            throw networkError(for: error, duringTransfer: true)
        } catch {
            throw LocalNetworkManualSyncError.snapshotUnavailable
        }

        guard let httpResponse = response as? HTTPURLResponse else {
            throw LocalNetworkManualSyncError.invalidResponse
        }
        if httpResponse.statusCode == 403 {
            throw LocalNetworkManualSyncError.pairingInvalid
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            throw LocalNetworkManualSyncError.snapshotUnavailable
        }

        let attributes = try? fileManager.attributesOfItem(atPath: temporaryDownloadURL.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        guard size > 0 else {
            throw LocalNetworkManualSyncError.snapshotInvalid
        }

        let ownedURL = fileManager.temporaryDirectory
            .appendingPathComponent("buerocockpit-sync-\(UUID().uuidString).bcsnapshot", isDirectory: false)
        do {
            try fileManager.copyItem(at: temporaryDownloadURL, to: ownedURL)
            guard let ackToken = httpResponse.value(forHTTPHeaderField: "X-BueroCockpit-Ack-Token"),
                  let serverRevision = httpResponse.value(forHTTPHeaderField: "X-BueroCockpit-Change-Version"),
                  let sequenceText = httpResponse.value(forHTTPHeaderField: "X-BueroCockpit-Server-Sequence"),
                  let serverSequence = Int64(sequenceText) else {
                try? fileManager.removeItem(at: ownedURL)
                throw LocalNetworkManualSyncError.snapshotInvalid
            }
            return DownloadedSnapshot(
                url: ownedURL,
                ackToken: ackToken,
                serverRevision: serverRevision,
                serverSequence: serverSequence
            )
        } catch {
            throw LocalNetworkManualSyncError.snapshotUnavailable
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

    private func buildPackage(from entryURL: URL, deviceId: String, clientSequence: Int64) throws -> UploadPackage? {
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
            schemaVersion: (task.schemaVersion ?? 1) >= 2 ? "local-sync-inbox-v2" : "local-sync-inbox-v1",
            createdAtUtc: Date(),
            files: files,
            clientSequence: clientSequence
        )
        return UploadPackage(
            request: request,
            originalPhotoCount: files.filter { $0.purpose == "original-photo" }.count,
            operation: task.operation?.lowercased() == "update" ? "update" : "create"
        )
    }

    private func markPackageTransferred(_ entryURL: URL, uploadResult: UploadResponse) throws {
        let taskURL = entryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
        let data = try Data(contentsOf: taskURL)
        guard var object = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw LocalNetworkManualSyncError.invalidPackage("Lokaler Aufgabenstatus konnte nicht bestätigt werden.")
        }
        object["status"] = "transferred"
        object["transportStatus"] = uploadResult.status
        object["transferredAt"] = ISO8601DateFormatter().string(from: uploadResult.receivedAtUtc)
        let updatedData = try JSONSerialization.data(withJSONObject: object, options: [.prettyPrinted, .sortedKeys])
        try updatedData.write(to: taskURL, options: [.atomic])
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
    let schemaVersion: Int?
    let operation: String?
}

private struct UploadPackage: Sendable {
    let request: UploadRequest
    let originalPhotoCount: Int
    let operation: String
}

private struct DownloadedSnapshot: Sendable {
    let url: URL
    let ackToken: String
    let serverRevision: String
    let serverSequence: Int64
}

private struct UploadRequest: Encodable, Sendable {
    let uploadId: String
    let deviceId: String
    let schemaVersion: String
    let createdAtUtc: Date
    let files: [UploadFile]
    let clientSequence: Int64
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

struct LocalNetworkDeltaResponse: Decodable, Equatable, Sendable {
    let apiVersion: String
    let requiresFullSync: Bool
    let fromRevision: String?
    let toRevision: String
    let serverSequence: Int64
    let lastConfirmedClientSequence: Int64?
    let ackToken: String?
    let tasks: [LocalNetworkDeltaTask]
    let categories: [LocalNetworkDeltaCategory]
    let technicians: [LocalNetworkDeltaTechnician]
    let attachments: [LocalNetworkDeltaAttachment]
    let tombstones: LocalNetworkDeltaTombstones
    let files: [LocalNetworkDeltaFile]
    let counts: LocalNetworkDeltaCounts
}

struct LocalNetworkDeltaTask: Codable, Equatable, Sendable {
    let id: String
    let title: String
    let customerName: String?
    let customerAddress: String?
    let customerEmail: String?
    let customerPhone: String?
    let currentCategoryId: String?
    let categoryIds: [String]?
    let categoryNames: [String]?
    let dueDate: String?
    let reminderDate: String?
    let followUpReason: String?
    let createdAt: String?
    let updatedAt: String?
    let materialOrderedAt: String?
    let status: String?
    let technician: String?
    let workflowType: String?
    let workflowStep: String?
    let notes: String?
    let shortText: String?
    let attachmentRefs: [String]?
}

struct LocalNetworkDeltaCategory: Codable, Equatable, Sendable {
    let id: String
    let name: String
    let order: Int?
    let parentId: String?
}

struct LocalNetworkDeltaTechnician: Codable, Equatable, Sendable {
    let id: String
    let name: String
    let abbreviation: String?
    let email: String?
    let phone: String?
}

struct LocalNetworkDeltaAttachment: Codable, Equatable, Sendable {
    let id: String
    let taskId: String
    let fileName: String
    let originalFileName: String?
    let displayName: String?
    let relativePath: String
    let packagePath: String?
    let previewPath: String?
    let contentHash: String?
    let contentType: String?
    let sizeBytes: Int64?
    let isImportant: Bool?
    let fileExists: Bool?
    let existsInSnapshot: Bool?
    let previewAvailable: Bool?
    let originalAvailableInLiveSync: Bool?
    let originalDownloadMode: String?
    let reason: String?
    let exportHint: String?
}

struct LocalNetworkDeltaTombstones: Decodable, Equatable, Sendable {
    let taskIds: [String]
    let categoryIds: [String]
    let technicianIds: [String]
    let attachmentIds: [String]
}

struct LocalNetworkDeltaFile: Decodable, Equatable, Sendable {
    let relativePath: String
    let sizeBytes: Int64
    let sha256: String
    let dataBase64: String
}

struct LocalNetworkDeltaCounts: Decodable, Equatable, Sendable {
    let tasks: Int
    let categories: Int
    let technicians: Int
    let attachments: Int
    let files: Int
    let tombstones: Int
    let unchangedObjects: Int
    let unchangedFiles: Int
}

private struct LocalNetworkAckRequest: Encodable, Sendable {
    let ackToken: String
    let serverRevision: String
    let lastConfirmedClientSequence: Int64?
}

struct LocalNetworkAckResponse: Decodable, Equatable, Sendable {
    let apiVersion: String
    let status: String
    let serverRevision: String
    let serverSequence: Int64
    let lastConfirmedClientSequence: Int64
    let message: String
}
