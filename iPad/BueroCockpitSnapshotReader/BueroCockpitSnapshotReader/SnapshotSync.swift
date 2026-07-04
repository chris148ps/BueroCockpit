import Foundation

enum SyncProviderType: String, Codable, CaseIterable, Identifiable, Sendable {
    case manualFile
    case iCloudDrive
    case googleDriveDirect
    case webDavNas
    case dropbox
    case microsoftGraphOneDrive

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .manualFile: "Manuelle Datei"
        case .iCloudDrive: "iCloud Drive"
        case .googleDriveDirect: "Google Drive Direktlink"
        case .webDavNas: "WebDAV / NAS"
        case .dropbox: "Dropbox"
        case .microsoftGraphOneDrive: "OneDrive / Microsoft Graph"
        }
    }

    var systemImage: String {
        switch self {
        case .manualFile: "doc.badge.arrow.up"
        case .iCloudDrive: "icloud"
        case .googleDriveDirect: "link"
        case .webDavNas: "externaldrive.connected.to.line.below"
        case .dropbox: "shippingbox"
        case .microsoftGraphOneDrive: "cloud"
        }
    }

    var isAvailable: Bool {
        self == .manualFile || self == .iCloudDrive || self == .googleDriveDirect
    }
}

struct SnapshotSyncSettings: Codable, Sendable {
    var providerType: SyncProviderType = .manualFile
    var googleDriveLink: String = ""
    var googleDriveFileId: String = ""
    var lastSuccessfulSync: Date?
    var lastError: String?
    var lastImportDate: Date?
    var localNetworkDesktop: LocalNetworkDesktopPairing = LocalNetworkDesktopPairing()
}

struct LocalNetworkDesktopPairing: Codable, Equatable, Sendable {
    var desktopAddress: String?
    var desktopPort: Int?
    var ipadDeviceId: String?
    var ipadDeviceName: String?
    var ipadPlatform: String?
    var desktopDeviceId: String?
    var desktopName: String?
    var desktopPlatform: String?
    var pairingCode: String?
    var pairedAt: Date?
    var lastSeenAt: Date?
    var trustKey: String?
    var sharedSecret: String?
}

final class SnapshotSyncSettingsStore: @unchecked Sendable {
    private let defaults: UserDefaults
    private let key = "snapshotSyncSettings"

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func load() -> SnapshotSyncSettings {
        guard let data = defaults.data(forKey: key),
              let settings = try? JSONDecoder().decode(SnapshotSyncSettings.self, from: data) else {
            return SnapshotSyncSettings()
        }
        return settings
    }

    func save(_ settings: SnapshotSyncSettings) {
        guard let data = try? JSONEncoder().encode(settings) else { return }
        defaults.set(data, forKey: key)
    }

    func reset() {
        defaults.removeObject(forKey: key)
    }
}

protocol SnapshotSyncProvider: Sendable {
    var type: SyncProviderType { get }
    func downloadLatestSnapshot() async throws -> URL
}

struct ManualFileProvider: SnapshotSyncProvider {
    let type = SyncProviderType.manualFile
    let localURL: URL

    func downloadLatestSnapshot() async throws -> URL {
        localURL
    }
}

enum SnapshotSyncError: LocalizedError, Sendable {
    case invalidGoogleDriveLink
    case invalidHTTPResponse
    case httpStatus(Int)
    case responseIsNeitherSnapshotNorWarningPage
    case downloadFormMissing
    case downloadedFileIsNotZip
    case providerUnavailable(String)

    var errorDescription: String? {
        switch self {
        case .invalidGoogleDriveLink:
            "Der Google-Drive-Link ist ungültig oder enthält keine Datei-ID."
        case .invalidHTTPResponse:
            "Google Drive hat keine gültige HTTP-Antwort geliefert."
        case .httpStatus(let status):
            "Google Drive hat den HTTP-Status \(status) zurückgegeben."
        case .responseIsNeitherSnapshotNorWarningPage:
            "Die Antwort ist weder eine ZIP-Datei noch eine Google-Downloadseite. Prüfen Sie die Freigabe des Links."
        case .downloadFormMissing:
            "Das Google-Downloadformular konnte nicht gefunden oder ausgewertet werden."
        case .downloadedFileIsNotZip:
            "Die heruntergeladene Datei ist keine gültige ZIP-Datei."
        case .providerUnavailable(let name):
            "\(name) ist noch nicht eingerichtet."
        }
    }
}

struct GoogleDriveDirectProvider: SnapshotSyncProvider {
    let type = SyncProviderType.googleDriveDirect
    let fileID: String
    var session: URLSession = .shared

    static func fileID(from input: String) -> String? {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        if !trimmed.contains("/") && !trimmed.contains("?") && trimmed.count >= 10 {
            return trimmed
        }

        guard let components = URLComponents(string: trimmed) else { return nil }
        if let id = components.queryItems?.first(where: { $0.name == "id" })?.value,
           !id.isEmpty {
            return id
        }

        let pathParts = components.path.split(separator: "/").map(String.init)
        if let fileIndex = pathParts.firstIndex(of: "d"), pathParts.indices.contains(fileIndex + 1) {
            return pathParts[fileIndex + 1]
        }
        if let fileIndex = pathParts.firstIndex(of: "file"),
           pathParts.indices.contains(fileIndex + 2),
           pathParts[fileIndex + 1] == "d" {
            return pathParts[fileIndex + 2]
        }
        return nil
    }

    func downloadLatestSnapshot() async throws -> URL {
        guard var components = URLComponents(string: "https://drive.google.com/uc") else {
            throw SnapshotSyncError.invalidGoogleDriveLink
        }
        components.queryItems = [
            URLQueryItem(name: "export", value: "download"),
            URLQueryItem(name: "id", value: fileID)
        ]
        guard let initialURL = components.url else {
            throw SnapshotSyncError.invalidGoogleDriveLink
        }

        let firstDownload = try await download(initialURL)
        if try Self.isZip(firstDownload.url) {
            return firstDownload.url
        }

        let confirmationURL = try Self.confirmationURL(from: firstDownload.url, baseURL: firstDownload.response.url ?? initialURL)
        try? FileManager.default.removeItem(at: firstDownload.url)
        let confirmedDownload = try await download(confirmationURL)
        guard try Self.isZip(confirmedDownload.url) else {
            try? FileManager.default.removeItem(at: confirmedDownload.url)
            throw SnapshotSyncError.downloadedFileIsNotZip
        }
        return confirmedDownload.url
    }

    private func download(_ url: URL) async throws -> (url: URL, response: HTTPURLResponse) {
        let (temporaryURL, response) = try await session.download(from: url)
        guard let response = response as? HTTPURLResponse else {
            throw SnapshotSyncError.invalidHTTPResponse
        }
        guard (200...299).contains(response.statusCode) else {
            throw SnapshotSyncError.httpStatus(response.statusCode)
        }

        let retainedURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("google-drive-\(UUID().uuidString).bclive")
        try FileManager.default.moveItem(at: temporaryURL, to: retainedURL)
        return (retainedURL, response)
    }

    private static func isZip(_ url: URL) throws -> Bool {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let signature = try handle.read(upToCount: 4) ?? Data()
        return signature.starts(with: [0x50, 0x4b, 0x03, 0x04])
            || signature.starts(with: [0x50, 0x4b, 0x05, 0x06])
            || signature.starts(with: [0x50, 0x4b, 0x07, 0x08])
    }

    private static func confirmationURL(from htmlURL: URL, baseURL: URL) throws -> URL {
        let attributes = try FileManager.default.attributesOfItem(atPath: htmlURL.path)
        let size = (attributes[.size] as? NSNumber)?.intValue ?? 0
        guard size > 0, size <= 2_000_000,
              let html = String(data: try Data(contentsOf: htmlURL), encoding: .utf8) else {
            throw SnapshotSyncError.responseIsNeitherSnapshotNorWarningPage
        }

        guard let formTag = firstMatch(
            pattern: #"<form\b[^>]*\bid\s*=\s*[\"']download-form[\"'][^>]*>"#,
            in: html,
            options: [.caseInsensitive]
        ),
        let action = attribute(named: "action", in: formTag),
        let actionURL = URL(string: decodeHTMLEntities(action), relativeTo: baseURL)?.absoluteURL else {
            throw SnapshotSyncError.downloadFormMissing
        }

        guard let formRange = html.range(of: formTag),
              let closingRange = html.range(of: "</form>", options: [.caseInsensitive], range: formRange.upperBound..<html.endIndex) else {
            throw SnapshotSyncError.downloadFormMissing
        }
        let formBody = String(html[formRange.upperBound..<closingRange.lowerBound])
        let inputTags = allMatches(pattern: #"<input\b[^>]*>"#, in: formBody, options: [.caseInsensitive])

        var components = URLComponents(url: actionURL, resolvingAgainstBaseURL: true)
        var queryItems = components?.queryItems ?? []
        for input in inputTags {
            guard let name = attribute(named: "name", in: input) else { continue }
            let value = attribute(named: "value", in: input) ?? ""
            queryItems.append(URLQueryItem(name: decodeHTMLEntities(name), value: decodeHTMLEntities(value)))
        }
        components?.queryItems = queryItems
        guard let result = components?.url else {
            throw SnapshotSyncError.downloadFormMissing
        }
        return result
    }

    private static func attribute(named name: String, in tag: String) -> String? {
        let escapedName = NSRegularExpression.escapedPattern(for: name)
        let pattern = "\\b\(escapedName)\\s*=\\s*([\\\"'])(.*?)\\1"
        guard let expression = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]),
              let match = expression.firstMatch(in: tag, range: NSRange(tag.startIndex..., in: tag)),
              let range = Range(match.range(at: 2), in: tag) else {
            return nil
        }
        return String(tag[range])
    }

    private static func firstMatch(pattern: String, in text: String, options: NSRegularExpression.Options) -> String? {
        allMatches(pattern: pattern, in: text, options: options).first
    }

    private static func allMatches(pattern: String, in text: String, options: NSRegularExpression.Options) -> [String] {
        guard let expression = try? NSRegularExpression(pattern: pattern, options: options) else { return [] }
        return expression.matches(in: text, range: NSRange(text.startIndex..., in: text)).compactMap { match in
            guard let range = Range(match.range, in: text) else { return nil }
            return String(text[range])
        }
    }

    private static func decodeHTMLEntities(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&amp;", with: "&")
            .replacingOccurrences(of: "&quot;", with: "\"")
            .replacingOccurrences(of: "&#39;", with: "'")
            .replacingOccurrences(of: "&lt;", with: "<")
            .replacingOccurrences(of: "&gt;", with: ">")
    }
}

struct WebDavProviderPlaceholder: SnapshotSyncProvider {
    let type = SyncProviderType.webDavNas
    func downloadLatestSnapshot() async throws -> URL {
        throw SnapshotSyncError.providerUnavailable(type.displayName)
    }
}

struct DropboxProviderPlaceholder: SnapshotSyncProvider {
    let type = SyncProviderType.dropbox
    func downloadLatestSnapshot() async throws -> URL {
        throw SnapshotSyncError.providerUnavailable(type.displayName)
    }
}

struct MicrosoftGraphProviderPlaceholder: SnapshotSyncProvider {
    let type = SyncProviderType.microsoftGraphOneDrive
    func downloadLatestSnapshot() async throws -> URL {
        throw SnapshotSyncError.providerUnavailable(type.displayName)
    }
}
