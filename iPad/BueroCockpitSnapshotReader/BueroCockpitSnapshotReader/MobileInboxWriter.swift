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
    case fileCouldNotBeRead(String)
    case fileCouldNotBeWritten(String)
    case fileIsEmpty(String)
    case jsonCouldNotBeWritten(String)
    case entryNotFound
    case entryAlreadyProcessed

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
        case .fileCouldNotBeRead(let name):
            return "Die Datei \(name) konnte nicht gelesen werden."
        case .fileCouldNotBeWritten(let name):
            return "Die Datei \(name) konnte nicht im Mobile-Inbox-Ordner gespeichert werden."
        case .fileIsEmpty(let name):
            return "Die Datei \(name) enthält keine Daten."
        case .jsonCouldNotBeWritten(let path):
            return "Die Aufgabe konnte nicht gespeichert werden: \(path)"
        case .entryNotFound:
            return "Der mobile Eingang existiert nicht mehr."
        case .entryAlreadyProcessed:
            return "Dieser mobile Eingang wurde bereits übernommen und kann nicht mehr bearbeitet werden."
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
    private struct EditableTask: Decodable {
        let id: String?
        let schemaVersion: Int?
        let createdAt: String?
        let status: String?
        let operation: String?
        let desktopTaskId: String?
        let baseRevision: String?
        let confirmedRevision: String?
        let baseValues: MobileInspectionRevisionValues?
        let customerName: String?
        let address: String?
        let phone: String?
        let email: String?
        let title: String?
        let category: String?
        let categoryId: String?
        let workflowType: String?
        let workflowStep: String?
        let dueDate: String?
        let followUpDate: String?
        let followUpReason: String?
        let technician: String?
        let notes: String?
        let photos: [EditablePhoto]?
        let sketches: [EditableSketch]?
        let files: [EditableFile]?
    }

    private struct EditablePhoto: Decodable {
        let id: String?
        let originalPath: String?
        let previewPath: String?
        let annotatedPath: String?
        let annotatedPreviewPath: String?
    }

    private struct EditableSketch: Decodable {
        let id: String?
        let path: String?
        let drawingPath: String?
    }

    private struct EditableFile: Decodable {
        let id: String?
        let fileName: String?
        let path: String?
    }

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
        let filesURL = entryURL.appendingPathComponent("files", isDirectory: true)
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
            if !draft.files.isEmpty {
                try fileManager.createDirectory(at: filesURL, withIntermediateDirectories: true)
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
        let savedFiles = try draft.files.enumerated().map { index, input in
            try saveFile(input, index: index + 1, filesURL: filesURL)
        }

        let task = MobileInspectionTask(
            id: entryID,
            schemaVersion: 2,
            createdAt: isoFormatter.string(from: now),
            source: "ios",
            status: "new",
            operation: draft.isDesktopTaskUpdate ? "update" : "create",
            desktopTaskId: draft.desktopTaskId,
            baseRevision: draft.baseRevision,
            confirmedRevision: draft.confirmedRevision,
            baseValues: draft.baseValues,
            customerName: draft.customerName.trimmedForMobileInbox,
            address: draft.address.trimmedForMobileInbox,
            phone: draft.phone.trimmedForMobileInbox,
            email: draft.email.trimmedForMobileInbox,
            title: draft.title.trimmedForMobileInbox,
            category: draft.category.trimmedForMobileInbox,
            categoryId: draft.categoryId.trimmedForMobileInbox,
            workflowType: draft.workflowType.trimmedForMobileInbox,
            workflowStep: draft.workflowStep.trimmedForMobileInbox,
            dueDate: draft.dueDate,
            followUpDate: draft.followUpDate,
            followUpReason: draft.followUpReason.trimmedForMobileInbox,
            technician: draft.technician.trimmedForMobileInbox,
            notes: draft.notes.trimmedForMobileInbox,
            photos: savedPhotos,
            sketches: savedSketches.isEmpty ? nil : savedSketches,
            files: savedFiles.isEmpty ? nil : savedFiles
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

    func loadDraftForEditing(entryID: String) throws -> MobileInspectionDraft {
        let selectedFolder = try store.resolveSelectedFolder()
        let accessGranted = selectedFolder.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw MobileInboxError.folderUnavailable
        }
        defer {
            selectedFolder.stopAccessingSecurityScopedResource()
        }

        let located = try locatePendingEntry(entryID: entryID, selectedFolder: selectedFolder)
        let raw = located.task
        let photos = try (raw.photos ?? []).enumerated().map { index, photo in
            MobileInspectionPhotoInput(
                id: photo.id?.trimmedForMobileInbox.isEmpty == false ? photo.id! : UUID().uuidString,
                fileName: "Foto \(index + 1)",
                data: try readEntryFile(photo.originalPath, entryURL: located.entryURL, displayName: "Foto \(index + 1)"),
                previewData: try? readEntryFile(photo.previewPath, entryURL: located.entryURL, displayName: "Vorschau \(index + 1)"),
                annotatedData: try? readEntryFile(photo.annotatedPath, entryURL: located.entryURL, displayName: "Markiertes Foto \(index + 1)"),
                annotatedPreviewData: try? readEntryFile(photo.annotatedPreviewPath, entryURL: located.entryURL, displayName: "Markierte Vorschau \(index + 1)")
            )
        }
        let sketches = try (raw.sketches ?? []).enumerated().map { index, sketch in
            MobileInspectionSketchInput(
                id: sketch.id?.trimmedForMobileInbox.isEmpty == false ? sketch.id! : UUID().uuidString,
                fileName: "Skizze \(index + 1)",
                data: try readEntryFile(sketch.path, entryURL: located.entryURL, displayName: "Skizze \(index + 1)"),
                previewData: nil,
                drawingData: try? readEntryFile(sketch.drawingPath, entryURL: located.entryURL, displayName: "Skizzen-Rohdaten \(index + 1)")
            )
        }
        let files = try (raw.files ?? []).enumerated().map { index, file in
            let fileName = file.fileName?.trimmedForMobileInbox.isEmpty == false
                ? file.fileName!
                : file.path.map { URL(fileURLWithPath: $0).lastPathComponent } ?? "Datei \(index + 1)"
            return MobileInspectionFileInput(
                id: file.id?.trimmedForMobileInbox.isEmpty == false ? file.id! : UUID().uuidString,
                fileName: fileName,
                data: try readEntryFile(file.path, entryURL: located.entryURL, displayName: fileName)
            )
        }

        return MobileInspectionDraft(
            editingEntryID: located.entryID,
            editingEntryDirectoryName: located.entryURL.lastPathComponent,
            originalCreatedAt: raw.createdAt,
            desktopTaskId: raw.desktopTaskId,
            baseRevision: raw.baseRevision,
            confirmedRevision: raw.confirmedRevision,
            baseValues: raw.baseValues,
            customerName: raw.customerName ?? "",
            address: raw.address ?? "",
            phone: raw.phone ?? "",
            email: raw.email ?? "",
            title: raw.title ?? "",
            category: raw.category ?? "",
            categoryId: raw.categoryId ?? "",
            workflowType: raw.workflowType ?? "",
            workflowStep: raw.workflowStep ?? "",
            dueDate: raw.dueDate,
            followUpDate: raw.followUpDate,
            followUpReason: raw.followUpReason ?? "",
            technician: raw.technician ?? "",
            notes: raw.notes ?? "",
            photos: photos,
            sketches: sketches,
            files: files
        )
    }

    func update(entryID: String, draft: MobileInspectionDraft) throws -> MobileInspectionSaveResult {
        let selectedFolder = try store.resolveSelectedFolder()
        let accessGranted = selectedFolder.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw MobileInboxError.folderUnavailable
        }
        defer {
            selectedFolder.stopAccessingSecurityScopedResource()
        }

        let located = try locatePendingEntry(entryID: entryID, selectedFolder: selectedFolder)
        let createdAt = located.task.createdAt?.trimmedForMobileInbox.isEmpty == false
            ? located.task.createdAt!
            : isoFormatter.string(from: Date())

        let stagingURL = located.entryURL.appendingPathComponent(".edit-\(UUID().uuidString)", isDirectory: true)
        let originalsURL = stagingURL.appendingPathComponent("originals", isDirectory: true)
        let previewsURL = stagingURL.appendingPathComponent("previews", isDirectory: true)
        let annotatedURL = stagingURL.appendingPathComponent("annotated", isDirectory: true)
        let sketchesURL = stagingURL.appendingPathComponent("sketches", isDirectory: true)
        let filesURL = stagingURL.appendingPathComponent("files", isDirectory: true)
        defer {
            try? fileManager.removeItem(at: stagingURL)
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
            if !draft.files.isEmpty {
                try fileManager.createDirectory(at: filesURL, withIntermediateDirectories: true)
            }
        } catch {
            throw MobileInboxError.directoryCouldNotBeCreated(located.entryURL.path)
        }

        let savedPhotos = try draft.photos.enumerated().map { index, input in
            try savePhoto(input, index: index + 1, originalsURL: originalsURL, previewsURL: previewsURL, annotatedURL: annotatedURL)
        }
        let savedSketches = try draft.sketches.enumerated().map { index, input in
            try saveSketch(input, index: index + 1, sketchesURL: sketchesURL)
        }
        let savedFiles = try draft.files.enumerated().map { index, input in
            try saveFile(input, index: index + 1, filesURL: filesURL)
        }
        let task = MobileInspectionTask(
            id: located.entryID,
            schemaVersion: located.task.schemaVersion ?? (draft.isDesktopTaskUpdate ? 2 : 1),
            createdAt: createdAt,
            source: "ios",
            status: "new",
            operation: draft.isDesktopTaskUpdate ? "update" : located.task.operation,
            desktopTaskId: draft.desktopTaskId,
            baseRevision: draft.baseRevision,
            confirmedRevision: draft.confirmedRevision,
            baseValues: draft.baseValues,
            customerName: draft.customerName.trimmedForMobileInbox,
            address: draft.address.trimmedForMobileInbox,
            phone: draft.phone.trimmedForMobileInbox,
            email: draft.email.trimmedForMobileInbox,
            title: draft.title.trimmedForMobileInbox,
            category: draft.category.trimmedForMobileInbox,
            categoryId: draft.categoryId.trimmedForMobileInbox,
            workflowType: draft.workflowType.trimmedForMobileInbox,
            workflowStep: draft.workflowStep.trimmedForMobileInbox,
            dueDate: draft.dueDate,
            followUpDate: draft.followUpDate,
            followUpReason: draft.followUpReason.trimmedForMobileInbox,
            technician: draft.technician.trimmedForMobileInbox,
            notes: draft.notes.trimmedForMobileInbox,
            photos: savedPhotos,
            sketches: savedSketches.isEmpty ? nil : savedSketches,
            files: savedFiles.isEmpty ? nil : savedFiles
        )
        let jsonURL = located.entryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
        let jsonData = try encoder.encode(task)

        for directoryName in ["originals", "previews", "annotated", "sketches", "files"] {
            let targetURL = located.entryURL.appendingPathComponent(directoryName, isDirectory: true)
            try? fileManager.removeItem(at: targetURL)
            let stagedDirectory = stagingURL.appendingPathComponent(directoryName, isDirectory: true)
            if fileManager.fileExists(atPath: stagedDirectory.path) {
                try fileManager.moveItem(at: stagedDirectory, to: targetURL)
            }
        }
        do {
            try jsonData.write(to: jsonURL, options: [.atomic])
        } catch {
            throw MobileInboxError.jsonCouldNotBeWritten(jsonURL.path)
        }
        return MobileInspectionSaveResult(entryID: located.entryID, entryURL: located.entryURL)
    }

    func deletePendingEntry(entryID: String) throws {
        let selectedFolder = try store.resolveSelectedFolder()
        let accessGranted = selectedFolder.startAccessingSecurityScopedResource()
        guard accessGranted else {
            throw MobileInboxError.folderUnavailable
        }
        defer {
            selectedFolder.stopAccessingSecurityScopedResource()
        }

        let located = try locatePendingEntry(entryID: entryID, selectedFolder: selectedFolder)
        try fileManager.removeItem(at: located.entryURL)
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

    private func saveFile(
        _ input: MobileInspectionFileInput,
        index: Int,
        filesURL: URL
    ) throws -> MobileInspectionFile {
        guard !input.data.isEmpty else {
            throw MobileInboxError.fileIsEmpty(input.fileName)
        }

        let fileID = String(format: "datei-%03d", index)
        let fileName = "\(fileID)-\(sanitizedFileName(input.fileName))"
        let fileURL = filesURL.appendingPathComponent(fileName, isDirectory: false)
        try writeFileData(input.data, to: fileURL, displayName: input.fileName)
        return MobileInspectionFile(
            id: fileID,
            fileName: input.fileName,
            path: "files/\(fileName)"
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

    private func writeFileData(_ data: Data, to url: URL, displayName: String) throws {
        guard !data.isEmpty else {
            throw MobileInboxError.fileIsEmpty(displayName)
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
            throw MobileInboxError.fileCouldNotBeWritten(displayName)
        }

        let attributes = try? fileManager.attributesOfItem(atPath: url.path)
        let size = (attributes?[.size] as? NSNumber)?.int64Value ?? 0
        guard size > 0 else {
            try? fileManager.removeItem(at: url)
            throw MobileInboxError.fileIsEmpty(displayName)
        }
    }

    private func locatePendingEntry(entryID: String, selectedFolder: URL) throws -> (entryID: String, entryURL: URL, task: EditableTask) {
        let inboxURL = store.mobileInboxURL(for: selectedFolder)
        guard fileManager.fileExists(atPath: inboxURL.path) else {
            throw MobileInboxError.entryNotFound
        }

        let directories = try fileManager.contentsOfDirectory(
            at: inboxURL,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        )
        for directoryURL in directories where directoryURL.lastPathComponent.hasPrefix("mobile-") {
            let jsonURL = directoryURL.appendingPathComponent("aufgabe.json", isDirectory: false)
            guard let data = try? Data(contentsOf: jsonURL),
                  let task = try? JSONDecoder().decode(EditableTask.self, from: data) else {
                continue
            }
            let taskID = task.id?.trimmedForMobileInbox.isEmpty == false ? task.id! : directoryURL.lastPathComponent
            guard taskID == entryID || directoryURL.lastPathComponent == entryID else {
                continue
            }
            let status = task.status?.trimmedForMobileInbox ?? ""
            guard status.caseInsensitiveCompare("new") == .orderedSame else {
                throw MobileInboxError.entryAlreadyProcessed
            }
            return (taskID, directoryURL, task)
        }

        throw MobileInboxError.entryNotFound
    }

    private func readEntryFile(_ path: String?, entryURL: URL, displayName: String) throws -> Data {
        guard let resolvedURL = resolvedEntryURL(path, entryURL: entryURL) else {
            throw MobileInboxError.fileCouldNotBeRead(displayName)
        }
        do {
            let data = try Data(contentsOf: resolvedURL)
            guard !data.isEmpty else {
                throw MobileInboxError.fileIsEmpty(displayName)
            }
            return data
        } catch let error as MobileInboxError {
            throw error
        } catch {
            throw MobileInboxError.fileCouldNotBeRead(displayName)
        }
    }

    private func resolvedEntryURL(_ path: String?, entryURL: URL) -> URL? {
        guard let value = path?.trimmedForMobileInbox, !value.isEmpty else {
            return nil
        }
        if value.hasPrefix("/") {
            return URL(fileURLWithPath: value)
        }
        return entryURL.appendingPathComponent(value, isDirectory: false)
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

    private func sanitizedFileName(_ name: String) -> String {
        let trimmed = name.trimmedForMobileInbox
        let fallback = trimmed.isEmpty ? "datei" : trimmed
        let invalidCharacters = CharacterSet(charactersIn: "/\\?%*|\"<>:")
            .union(.controlCharacters)
            .union(.whitespacesAndNewlines)
        let components = fallback.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        let result = components.joined(separator: "-")
        return result.isEmpty ? "datei" : result
    }
}

private extension String {
    var trimmedForMobileInbox: String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }
}

final class MobileInspectionDraftStore: @unchecked Sendable {
    private struct StoredDraft: Codable {
        let schemaVersion: Int
        let updatedAt: Date
        let editingEntryID: String?
        let editingEntryDirectoryName: String?
        let originalCreatedAt: String?
        let desktopTaskId: String?
        let baseRevision: String?
        let confirmedRevision: String?
        let baseValues: MobileInspectionRevisionValues?
        let customerName: String
        let address: String
        let phone: String
        let email: String
        let title: String
        let category: String
        let categoryId: String?
        let workflowType: String?
        let workflowStep: String?
        let dueDate: String?
        let followUpDate: String?
        let followUpReason: String?
        let technician: String?
        let notes: String
        let photos: [StoredPhoto]
        let sketches: [StoredSketch]
        let files: [StoredFile]?
    }

    private struct StoredPhoto: Codable {
        let id: String
        let fileName: String
        let dataPath: String
        let previewPath: String?
        let annotatedPath: String?
        let annotatedPreviewPath: String?
    }

    private struct StoredSketch: Codable {
        let id: String
        let fileName: String
        let dataPath: String
        let previewPath: String?
        let drawingPath: String?
    }

    private struct StoredFile: Codable {
        let id: String
        let fileName: String
        let dataPath: String
    }

    private let fileManager: FileManager
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
        encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
    }

    func hasDraft() -> Bool {
        fileManager.fileExists(atPath: draftURL.path)
    }

    func load() throws -> MobileInspectionDraft? {
        guard fileManager.fileExists(atPath: draftURL.path) else {
            return nil
        }

        let storedDraft = try decoder.decode(StoredDraft.self, from: Data(contentsOf: draftURL))
        let photos = storedDraft.photos.compactMap(loadPhoto)
        let sketches = storedDraft.sketches.compactMap(loadSketch)
        let files = (storedDraft.files ?? []).compactMap(loadFile)
        return MobileInspectionDraft(
            editingEntryID: storedDraft.editingEntryID,
            editingEntryDirectoryName: storedDraft.editingEntryDirectoryName,
            originalCreatedAt: storedDraft.originalCreatedAt,
            desktopTaskId: storedDraft.desktopTaskId,
            baseRevision: storedDraft.baseRevision,
            confirmedRevision: storedDraft.confirmedRevision,
            baseValues: storedDraft.baseValues,
            customerName: storedDraft.customerName,
            address: storedDraft.address,
            phone: storedDraft.phone,
            email: storedDraft.email,
            title: storedDraft.title,
            category: storedDraft.category,
            categoryId: storedDraft.categoryId ?? "",
            workflowType: storedDraft.workflowType ?? "",
            workflowStep: storedDraft.workflowStep ?? "",
            dueDate: storedDraft.dueDate,
            followUpDate: storedDraft.followUpDate,
            followUpReason: storedDraft.followUpReason ?? "",
            technician: storedDraft.technician ?? "",
            notes: storedDraft.notes,
            photos: photos,
            sketches: sketches,
            files: files
        )
    }

    func save(_ draft: MobileInspectionDraft) throws {
        guard draft.hasUserContent else {
            try discard()
            return
        }

        try fileManager.createDirectory(at: draftDirectoryURL, withIntermediateDirectories: true)
        try fileManager.createDirectory(at: photosDirectoryURL, withIntermediateDirectories: true)
        try fileManager.createDirectory(at: sketchesDirectoryURL, withIntermediateDirectories: true)
        try fileManager.createDirectory(at: filesDirectoryURL, withIntermediateDirectories: true)

        let storedPhotos = try draft.photos.enumerated().map { index, photo in
            try savePhoto(photo, index: index + 1)
        }
        try removeStaleFiles(in: photosDirectoryURL, keeping: storedPhotos.flatMap {
            [$0.dataPath, $0.previewPath, $0.annotatedPath, $0.annotatedPreviewPath].compactMap { $0 }
        })

        let storedSketches = try draft.sketches.enumerated().map { index, sketch in
            try saveSketch(sketch, index: index + 1)
        }
        try removeStaleFiles(in: sketchesDirectoryURL, keeping: storedSketches.flatMap {
            [$0.dataPath, $0.previewPath, $0.drawingPath].compactMap { $0 }
        })

        let storedFiles = try draft.files.enumerated().map { index, file in
            try saveFile(file, index: index + 1)
        }
        try removeStaleFiles(in: filesDirectoryURL, keeping: storedFiles.map(\.dataPath))

        let storedDraft = StoredDraft(
            schemaVersion: 1,
            updatedAt: Date(),
            editingEntryID: draft.editingEntryID,
            editingEntryDirectoryName: draft.editingEntryDirectoryName,
            originalCreatedAt: draft.originalCreatedAt,
            desktopTaskId: draft.desktopTaskId,
            baseRevision: draft.baseRevision,
            confirmedRevision: draft.confirmedRevision,
            baseValues: draft.baseValues,
            customerName: draft.customerName,
            address: draft.address,
            phone: draft.phone,
            email: draft.email,
            title: draft.title,
            category: draft.category,
            categoryId: draft.categoryId,
            workflowType: draft.workflowType,
            workflowStep: draft.workflowStep,
            dueDate: draft.dueDate,
            followUpDate: draft.followUpDate,
            followUpReason: draft.followUpReason,
            technician: draft.technician,
            notes: draft.notes,
            photos: storedPhotos,
            sketches: storedSketches,
            files: storedFiles
        )
        try encoder.encode(storedDraft).write(to: draftURL, options: [.atomic])
    }

    func discard() throws {
        guard fileManager.fileExists(atPath: draftDirectoryURL.path) else {
            return
        }

        try fileManager.removeItem(at: draftDirectoryURL)
    }

    private var draftDirectoryURL: URL {
        let applicationSupportURL = try? fileManager.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        let rootURL = applicationSupportURL ?? fileManager.temporaryDirectory
        return rootURL
            .appendingPathComponent("BueroCockpit", isDirectory: true)
            .appendingPathComponent("MobileInspectionDraft", isDirectory: true)
    }

    private var draftURL: URL {
        draftDirectoryURL.appendingPathComponent("draft.json", isDirectory: false)
    }

    private var photosDirectoryURL: URL {
        draftDirectoryURL.appendingPathComponent("photos", isDirectory: true)
    }

    private var sketchesDirectoryURL: URL {
        draftDirectoryURL.appendingPathComponent("sketches", isDirectory: true)
    }

    private var filesDirectoryURL: URL {
        draftDirectoryURL.appendingPathComponent("files", isDirectory: true)
    }

    private func savePhoto(_ photo: MobileInspectionPhotoInput, index: Int) throws -> StoredPhoto {
        let name = String(format: "photo-%03d", index)
        let dataPath = "photos/\(name).jpg"
        let previewPath = photo.previewData == nil ? nil : "photos/\(name)-thumb.jpg"
        let annotatedPath = photo.annotatedData == nil ? nil : "photos/\(name)-marked.jpg"
        let annotatedPreviewPath = photo.annotatedPreviewData == nil ? nil : "photos/\(name)-marked-thumb.jpg"

        try write(photo.data, relativePath: dataPath)
        if let previewData = photo.previewData, let previewPath {
            try write(previewData, relativePath: previewPath)
        }
        if let annotatedData = photo.annotatedData, let annotatedPath {
            try write(annotatedData, relativePath: annotatedPath)
        }
        if let annotatedPreviewData = photo.annotatedPreviewData, let annotatedPreviewPath {
            try write(annotatedPreviewData, relativePath: annotatedPreviewPath)
        }

        return StoredPhoto(
            id: photo.id,
            fileName: photo.fileName,
            dataPath: dataPath,
            previewPath: previewPath,
            annotatedPath: annotatedPath,
            annotatedPreviewPath: annotatedPreviewPath
        )
    }

    private func saveSketch(_ sketch: MobileInspectionSketchInput, index: Int) throws -> StoredSketch {
        let name = String(format: "sketch-%03d", index)
        let dataPath = "sketches/\(name).png"
        let previewPath = sketch.previewData == nil ? nil : "sketches/\(name)-thumb.jpg"
        let drawingPath = sketch.drawingData == nil ? nil : "sketches/\(name).pkdrawing"

        try write(sketch.data, relativePath: dataPath)
        if let previewData = sketch.previewData, let previewPath {
            try write(previewData, relativePath: previewPath)
        }
        if let drawingData = sketch.drawingData, let drawingPath {
            try write(drawingData, relativePath: drawingPath)
        }

        return StoredSketch(
            id: sketch.id,
            fileName: sketch.fileName,
            dataPath: dataPath,
            previewPath: previewPath,
            drawingPath: drawingPath
        )
    }

    private func saveFile(_ file: MobileInspectionFileInput, index: Int) throws -> StoredFile {
        let name = String(format: "file-%03d", index)
        let dataPath = "files/\(name)-\(draftSafeFileName(file.fileName))"

        try write(file.data, relativePath: dataPath)
        return StoredFile(
            id: file.id,
            fileName: file.fileName,
            dataPath: dataPath
        )
    }

    private func loadPhoto(_ photo: StoredPhoto) -> MobileInspectionPhotoInput? {
        guard let data = try? read(relativePath: photo.dataPath) else {
            return nil
        }

        return MobileInspectionPhotoInput(
            id: photo.id,
            fileName: photo.fileName,
            data: data,
            previewData: photo.previewPath.flatMap { try? read(relativePath: $0) },
            annotatedData: photo.annotatedPath.flatMap { try? read(relativePath: $0) },
            annotatedPreviewData: photo.annotatedPreviewPath.flatMap { try? read(relativePath: $0) }
        )
    }

    private func loadSketch(_ sketch: StoredSketch) -> MobileInspectionSketchInput? {
        guard let data = try? read(relativePath: sketch.dataPath) else {
            return nil
        }

        return MobileInspectionSketchInput(
            id: sketch.id,
            fileName: sketch.fileName,
            data: data,
            previewData: sketch.previewPath.flatMap { try? read(relativePath: $0) },
            drawingData: sketch.drawingPath.flatMap { try? read(relativePath: $0) }
        )
    }

    private func loadFile(_ file: StoredFile) -> MobileInspectionFileInput? {
        guard let data = try? read(relativePath: file.dataPath) else {
            return nil
        }

        return MobileInspectionFileInput(
            id: file.id,
            fileName: file.fileName,
            data: data
        )
    }

    private func write(_ data: Data, relativePath: String) throws {
        let url = draftDirectoryURL.appendingPathComponent(relativePath, isDirectory: false)
        try data.write(to: url, options: [.atomic])
    }

    private func read(relativePath: String) throws -> Data {
        try Data(contentsOf: draftDirectoryURL.appendingPathComponent(relativePath, isDirectory: false))
    }

    private func removeStaleFiles(in directoryURL: URL, keeping relativePaths: [String]) throws {
        guard fileManager.fileExists(atPath: directoryURL.path) else {
            return
        }

        let keepNames = Set(relativePaths.map { URL(fileURLWithPath: $0).lastPathComponent })
        for url in try fileManager.contentsOfDirectory(at: directoryURL, includingPropertiesForKeys: nil) {
            if !keepNames.contains(url.lastPathComponent) {
                try fileManager.removeItem(at: url)
            }
        }
    }

    private func draftSafeFileName(_ name: String) -> String {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        let fallback = trimmed.isEmpty ? "datei" : trimmed
        let invalidCharacters = CharacterSet(charactersIn: "/\\?%*|\"<>:")
            .union(.controlCharacters)
            .union(.whitespacesAndNewlines)
        let components = fallback.components(separatedBy: invalidCharacters).filter { !$0.isEmpty }
        let result = components.joined(separator: "-")
        return result.isEmpty ? "datei" : result
    }
}
