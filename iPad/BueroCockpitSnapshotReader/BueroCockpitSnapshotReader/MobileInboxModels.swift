import Foundation

struct MobileInspectionPhoto: Codable, Equatable, Sendable {
    let id: String
    let originalPath: String
    let previewPath: String
    let annotatedPath: String?
    let annotatedPreviewPath: String?
}

struct MobileInspectionSketch: Codable, Equatable, Sendable {
    let id: String
    let path: String
    let drawingPath: String?
}

struct MobileInspectionTask: Codable, Equatable, Sendable {
    let id: String
    let schemaVersion: Int
    let createdAt: String
    let source: String
    let status: String
    let customerName: String
    let address: String
    let phone: String
    let email: String
    let title: String
    let category: String
    let notes: String
    let photos: [MobileInspectionPhoto]
    let sketches: [MobileInspectionSketch]?
}

struct MobileInspectionPhotoInput: Identifiable, Equatable, Sendable {
    let id: String
    let fileName: String
    let data: Data
    let previewData: Data?
    let annotatedData: Data?
    let annotatedPreviewData: Data?
}

struct MobileInspectionSketchInput: Identifiable, Equatable, Sendable {
    let id: String
    let fileName: String
    let data: Data
    let previewData: Data?
    let drawingData: Data?
}

struct MobileInspectionDraft: Equatable, Sendable {
    var customerName: String = ""
    var address: String = ""
    var phone: String = ""
    var email: String = ""
    var title: String = ""
    var category: String = ""
    var notes: String = ""
    var photos: [MobileInspectionPhotoInput] = []
    var sketches: [MobileInspectionSketchInput] = []
}

struct MobileInspectionSaveResult: Equatable, Sendable {
    let entryID: String
    let entryURL: URL
}

struct MobileInboxPendingEntry: Identifiable, Equatable, Sendable {
    let id: String
    let customerName: String
    let title: String
    let category: String
    let notes: String
    let createdAt: String
    let status: String
    let photoCount: Int
    let annotatedPhotoCount: Int
    let sketchCount: Int
    let entryURL: URL

    var displayTitle: String {
        let trimmed = title.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Neue mobile Besichtigung" : trimmed
    }

    var searchableText: String {
        [customerName, title, category, notes, createdAt, status].joined(separator: "\n")
    }

    var attachmentSummary: String? {
        var parts: [String] = []
        if photoCount == 1 {
            parts.append("1 Foto")
        } else if photoCount > 1 {
            parts.append("\(photoCount) Fotos")
        }
        if annotatedPhotoCount == 1 {
            parts.append("1 markierte Version")
        } else if annotatedPhotoCount > 1 {
            parts.append("\(annotatedPhotoCount) markierte Versionen")
        }
        if sketchCount == 1 {
            parts.append("1 Skizze")
        } else if sketchCount > 1 {
            parts.append("\(sketchCount) Skizzen")
        }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    var displayCreatedAt: String? {
        SnapshotDisplayFormatter.displayDate(createdAt)
    }
}
