import Foundation
import UIKit

enum MobileInboxError: LocalizedError, Sendable {
    case folderNotSelected
    case folderUnavailable
    case bookmarkFailed
    case directoryCouldNotBeCreated(String)
    case imageDataIsEmpty(String)
    case imageCouldNotBeDecoded(String)
    case imageCouldNotBeEncoded(String)
    case photoCouldNotBeWritten(String)
    case photoFileIsEmpty(String)
    case sketchDataIsEmpty(String)
    case sketchCouldNotBeWritten(String)
    case sketchFileIsEmpty(String)
    case drawingCouldNotBeWritten(String)
    case drawingFileIsEmpty(String)
    case jsonCouldNotBeWritten(String)

    var errorDescription: String? {
        switch self {
        case .folderNotSelected:
            return "Bitte zuerst einen Mobile-Inbox-Ordner wählen."
        case .folderUnavailable:
            return "Der Mobile-Inbox-Ordner ist nicht mehr verfügbar. Bitte erneut auswählen."
        case .bookmarkFailed:
            return "Der Mobile-Inbox-Ordner konnte nicht gespeichert werden. Bitte erneut auswählen."
        case .directoryCouldNotBeCreated(let path):
            return "Der Mobile-Inbox-Zielordner konnte nicht angelegt werden: \(path)"
        case .imageDataIsEmpty(let name):
            return "Das Foto \(name) enthält keine Bilddaten."
        case .imageCouldNotBeDecoded(let name):
            return "Das Foto \(name) konnte nicht gelesen werden."
        case .imageCouldNotBeEncoded(let name):
            return "Das Foto \(name) konnte nicht als JPG gespeichert werden."
        case .photoCouldNotBeWritten(let name):
            return "Das Foto \(name) konnte nicht im Mobile-Inbox-Ordner gespeichert werden."
        case .photoFileIsEmpty(let name):
            return "Das Foto \(name) wurde ohne Inhalt gespeichert. Bitte erneut auswählen."
        case .sketchDataIsEmpty(let name):
            return "Die Skizze \(name) enthält keine Bilddaten."
        case .sketchCouldNotBeWritten(let name):
            return "Die Skizze \(name) konnte nicht im Mobile-Inbox-Ordner gespeichert werden."
        case .sketchFileIsEmpty(let name):
            return "Die Skizze \(name) wurde ohne Inhalt gespeichert. Bitte erneut zeichnen."
        case .drawingCouldNotBeWritten(let name):
            return "Die bearbeitbare Skizze \(name) konnte nicht gespeichert werden."
        case .drawingFileIsEmpty(let name):
            return "Die bearbeitbare Skizze \(name) wurde ohne Inhalt gespeichert."
        case .jsonCouldNotBeWritten(let path):
            return "Die Aufgabe konnte nicht gespeichert werden: \(path)"
        }
    }
}

final class MobileInboxStore: @unchecked Sendable {
    private enum Key {
        static let folderBookmark = "mobileInboxFolderBookmark"
        static let folderDisplayPath = "mobileInboxFolderDisplayPath"
    }

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    var selectedFolderDisplayPath: String? {
        defaults.string(forKey: Key.folderDisplayPath)
    }

    var hasSelectedFolder: Bool {
        defaults.data(forKey: Key.folderBookmark) != nil
    }

    func saveSelectedFolder(_ folderURL: URL) throws {
        let accessGranted = folderURL.startAccessingSecurityScopedResource()
        defer {
            if accessGranted {
                folderURL.stopAccessingSecurityScopedResource()
            }
        }

        do {
            let bookmark = try folderURL.bookmarkData(
                options: [],
                includingResourceValuesForKeys: nil,
                relativeTo: nil
            )
            defaults.set(bookmark, forKey: Key.folderBookmark)
            defaults.set(folderURL.path, forKey: Key.folderDisplayPath)
        } catch {
            throw MobileInboxError.bookmarkFailed
        }
    }

    func resolveSelectedFolder() throws -> URL {
        guard let bookmark = defaults.data(forKey: Key.folderBookmark) else {
            throw MobileInboxError.folderNotSelected
        }

        var isStale = false
        let url = try URL(
            resolvingBookmarkData: bookmark,
            options: [],
            relativeTo: nil,
            bookmarkDataIsStale: &isStale
        )

        guard !isStale else {
            defaults.removeObject(forKey: Key.folderBookmark)
            defaults.removeObject(forKey: Key.folderDisplayPath)
            throw MobileInboxError.folderUnavailable
        }

        return url
    }

    func mobileInboxURL(for selectedFolder: URL) -> URL {
        if selectedFolder.lastPathComponent.caseInsensitiveCompare("mobile-inbox") == .orderedSame {
            return selectedFolder
        }

        return selectedFolder.appendingPathComponent("mobile-inbox", isDirectory: true)
    }
}

final class MobileInboxWriter: @unchecked Sendable {
    private let store: MobileInboxStore
    private let fileManager: FileManager
    private let encoder: JSONEncoder
    private let isoFormatter: ISO8601DateFormatter

    init(
        store: MobileInboxStore = MobileInboxStore(),
        fileManager: FileManager = .default
    ) {
        self.store = store
        self.fileManager = fileManager
        encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        isoFormatter = ISO8601DateFormatter()
        isoFormatter.formatOptions = [.withInternetDateTime]
    }

    func save(_ draft: MobileInspectionDraft, now: Date = Date()) throws -> MobileInspectionSaveResult {
        let selectedFolder = try store.resolveSelectedFolder()
        let accessGranted = selectedFolder.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw MobileInboxError.folderUnavailable
        }
        defer {
            selectedFolder.stopAccessingSecurityScopedResource()
        }

        let entryID = makeEntryID(createdAt: now)
        let directoryName = "\(entryID)-\(sanitizedCustomerName(draft.customerName))"
        let inboxURL = store.mobileInboxURL(for: selectedFolder)
        let entryURL = inboxURL.appendingPathComponent(directoryName, isDirectory: true)
        let originalsURL = entryURL.appendingPathComponent("originals", isDirectory: true)
        let previewsURL = entryURL.appendingPathComponent("previews", isDirectory: true)
        let annotatedURL = entryURL.appendingPathComponent("annotated", isDirectory: true)
        let sketchesURL = entryURL.appendingPathComponent("sketches", isDirectory: true)
        var didFinish = false
        defer {
            if !didFinish {
                try? fileManager.removeItem(at: entryURL)
            }
        }

        do {
            try fileManager.createDirectory(at: originalsURL, withIntermediateDirectories: true)
            try fileManager.createDirectory(at: previewsURL, withIntermediateDirectories: true)
            if draft.photos.contains(where: { $0.annotatedData != nil }) {
                try fileManager.createDirectory(at: annotatedURL, withIntermediateDirectories: true)
            }
            if !draft.sketches.isEmpty {
                try fileManager.createDirectory(at: sketchesURL, withIntermediateDirectories: true)
            }
        } catch {
            throw MobileInboxError.directoryCouldNotBeCreated(entryURL.path)
        }

        let savedPhotos = try draft.photos.enumerated().map { index, input in
            try savePhoto(
                input,
                index: index + 1,
                originalsURL: originalsURL,
                previewsURL: previewsURL,
                annotatedURL: annotatedURL
            )
        }

        let savedSketches = try draft.sketches.enumerated().map { index, input in
            try saveSketch(input, index: index + 1, sketchesURL: sketchesURL)
        }

        let task = MobileInspectionTask(
            id: entryID,
            schemaVersion: 1,
            createdAt: isoFormatter.string(from: now),
            source: "ios",
            status: "new",
            customerName: draft.customerName.trimmedForMobileInbox,
            address: draft.address.trimmedForMobileInbox,
            phone: draft.phone.trimmedForMobileInbox,
            email: draft.email.trimmedForMobileInbox,
            title: draft.title.trimmedForMobileInbox,
            category: draft.category.trimmedForMobileInbox,
            notes: draft.notes.trimmedForMobileInbox,
            photos: savedPhotos,
            sketches: savedSketches.isEmpty ? nil : savedSketches
        )

        let jsonURL = entryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
        let data = try encoder.encode(task)
        do {
            try data.write(to: jsonURL, options: [.atomic])
        } catch {
            throw MobileInboxError.jsonCouldNotBeWritten(jsonURL.path)
        }
        didFinish = true
        return MobileInspectionSaveResult(entryID: entryID, entryURL: entryURL)
    }

    private func savePhoto(
        _ input: MobileInspectionPhotoInput,
        index: Int,
        originalsURL: URL,
        previewsURL: URL,
        annotatedURL: URL
    ) throws -> MobileInspectionPhoto {
        let photoID = String(format: "foto-%03d", index)
        let originalFileName = "\(photoID).jpg"
        let previewFileName = "\(photoID)-thumb.jpg"

        let originalData = try resizedJPEGData(
            from: input.data,
            maxPixelLength: 2400,
            compressionQuality: 0.8,
            displayName: input.fileName
        )
        let previewData = try resizedJPEGData(
            from: input.data,
            maxPixelLength: 400,
            compressionQuality: 0.7,
            displayName: input.fileName
        )

        let originalURL = originalsURL.appendingPathComponent(originalFileName, isDirectory: false)
        let previewURL = previewsURL.appendingPathComponent(previewFileName, isDirectory: false)
        try writePhotoData(originalData, to: originalURL, displayName: input.fileName)
        try writePhotoData(previewData, to: previewURL, displayName: input.fileName)

        let annotatedPath: String?
        let annotatedPreviewPath: String?
        if let annotatedData = input.annotatedData {
            let annotatedFileName = "\(photoID)-markiert.jpg"
            let annotatedPreviewFileName = "\(photoID)-markiert-thumb.jpg"
            let markedDisplayName = "\(input.fileName) markiert"
            let markedData = try resizedJPEGData(
                from: annotatedData,
                maxPixelLength: 2400,
                compressionQuality: 0.85,
                displayName: markedDisplayName
            )
            let markedPreviewData = try resizedJPEGData(
                from: annotatedData,
                maxPixelLength: 400,
                compressionQuality: 0.7,
                displayName: markedDisplayName
            )
            try writePhotoData(
                markedData,
                to: annotatedURL.appendingPathComponent(annotatedFileName, isDirectory: false),
                displayName: markedDisplayName
            )
            try writePhotoData(
                markedPreviewData,
                to: annotatedURL.appendingPathComponent(annotatedPreviewFileName, isDirectory: false),
                displayName: markedDisplayName
            )
            annotatedPath = "annotated/\(annotatedFileName)"
            annotatedPreviewPath = "annotated/\(annotatedPreviewFileName)"
        } else {
            annotatedPath = nil
            annotatedPreviewPath = nil
        }

        return MobileInspectionPhoto(
            id: photoID,
            originalPath: "originals/\(originalFileName)",
            previewPath: "previews/\(previewFileName)",
            annotatedPath: annotatedPath,
            annotatedPreviewPath: annotatedPreviewPath
        )
    }

    private func saveSketch(
        _ input: MobileInspectionSketchInput,
        index: Int,
        sketchesURL: URL
    ) throws -> MobileInspectionSketch {
        guard !input.data.isEmpty else {
            throw MobileInboxError.sketchDataIsEmpty(input.fileName)
        }

        let sketchID = String(format: "skizze-%03d", index)
        let pngFileName = "\(sketchID).png"
        let pngURL = sketchesURL.appendingPathComponent(pngFileName, isDirectory: false)
        try writeSketchData(input.data, to: pngURL, displayName: input.fileName)

        let drawingPath: String?
        if let drawingData = input.drawingData, !drawingData.isEmpty {
            let drawingFileName = "\(sketchID).pkdrawing"
            let drawingURL = sketchesURL.appendingPathComponent(drawingFileName, isDirectory: false)
            try writeDrawingData(drawingData, to: drawingURL, displayName: input.fileName)
            drawingPath = "sketches/\(drawingFileName)"
        } else {
            drawingPath = nil
        }

        return MobileInspectionSketch(
            id: sketchID,
            path: "sketches/\(pngFileName)",
            drawingPath: drawingPath
        )
    }

    private func resizedJPEGData(
        from data: Data,
        maxPixelLength: CGFloat,
        compressionQuality: CGFloat,
        displayName: String
    ) throws -> Data {
        guard !data.isEmpty else {
            throw MobileInboxError.imageDataIsEmpty(displayName)
        }

        guard let image = UIImage(data: data) else {
            throw MobileInboxError.imageCouldNotBeDecoded(displayName)
        }

        let size = image.size
        let longestSide = max(size.width, size.height)
        guard longestSide > 0 else {
            throw MobileInboxError.imageCouldNotBeDecoded(displayName)
        }

        let targetSize: CGSize
        if longestSide > maxPixelLength {
            let scale = maxPixelLength / longestSide
            targetSize = CGSize(width: size.width * scale, height: size.height * scale)
        } else {
            targetSize = size
        }

        let renderer = UIGraphicsImageRenderer(size: targetSize)
        let renderedImage = renderer.image { _ in
            image.draw(in: CGRect(origin: .zero, size: targetSize))
        }

        guard let jpgData = renderedImage.jpegData(compressionQuality: compressionQuality) else {
            throw MobileInboxError.imageCouldNotBeEncoded(displayName)
        }
        guard !jpgData.isEmpty else {
            throw MobileInboxError.imageCouldNotBeEncoded(displayName)
        }
        return jpgData
    }

    private func writePhotoData(_ data: Data, to url: URL, displayName: String) throws {
        guard !data.isEmpty else {
            throw MobileInboxError.imageCouldNotBeEncoded(displayName)
        }

        var coordinationError: NSError?
        var writeError: Error?
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(writingItemAt: url, options: [.forReplacing], error: &coordinationError) { writableURL in
            do {
                try data.write(to: writableURL, options: [.atomic])
            } catch {
                writeError = error
            }
        }

        if coordinationError != nil || writeError != nil {
            throw MobileInboxError.photoCouldNotBeWritten(displayName)
        }

        let attributes = try? fileManager.attributesOfItem(atPath: url.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        guard size > 0 else {
            try? fileManager.removeItem(at: url)
            throw MobileInboxError.photoFileIsEmpty(displayName)
        }
    }

    private func writeSketchData(_ data: Data, to url: URL, displayName: String) throws {
        guard !data.isEmpty else {
            throw MobileInboxError.sketchDataIsEmpty(displayName)
        }

        var coordinationError: NSError?
        var writeError: Error?
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(writingItemAt: url, options: [.forReplacing], error: &coordinationError) { writableURL in
            do {
                try data.write(to: writableURL, options: [.atomic])
            } catch {
                writeError = error
            }
        }

        if coordinationError != nil || writeError != nil {
            throw MobileInboxError.sketchCouldNotBeWritten(displayName)
        }

        let attributes = try? fileManager.attributesOfItem(atPath: url.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        guard size > 0 else {
            try? fileManager.removeItem(at: url)
            throw MobileInboxError.sketchFileIsEmpty(displayName)
        }
    }

    private func writeDrawingData(_ data: Data, to url: URL, displayName: String) throws {
        guard !data.isEmpty else {
            throw MobileInboxError.drawingFileIsEmpty(displayName)
        }

        var coordinationError: NSError?
        var writeError: Error?
        let coordinator = NSFileCoordinator(filePresenter: nil)
        coordinator.coordinate(writingItemAt: url, options: [.forReplacing], error: &coordinationError) { writableURL in
            do {
                try data.write(to: writableURL, options: [.atomic])
            } catch {
                writeError = error
            }
        }

        if coordinationError != nil || writeError != nil {
            throw MobileInboxError.drawingCouldNotBeWritten(displayName)
        }

        let attributes = try? fileManager.attributesOfItem(atPath: url.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        guard size > 0 else {
            try? fileManager.removeItem(at: url)
            throw MobileInboxError.drawingFileIsEmpty(displayName)
        }
    }

    private func makeEntryID(createdAt: Date) -> String {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone.current
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        return "mobile-\(formatter.string(from: createdAt))"
    }

    private func sanitizedCustomerName(_ name: String) -> String {
        let trimmed = name.trimmedForMobileInbox
        guard !trimmed.isEmpty else {
            return "ohne-name"
        }

        let invalidCharacters = CharacterSet(charactersIn: "/\\?%*|\"<>:")
            .union(.controlCharacters)
            .union(.whitespacesAndNewlines)
        let components = trimmed.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        let result = components.joined(separator: "-").lowercased()
        return result.isEmpty ? "ohne-name" : result
    }
}

private extension String {
    var trimmedForMobileInbox: String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
