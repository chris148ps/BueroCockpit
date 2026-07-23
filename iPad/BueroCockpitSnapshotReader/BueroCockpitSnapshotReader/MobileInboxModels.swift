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

struct MobileInspectionFile: Codable, Equatable, Sendable {
    let id: String
    let fileName: String
    let path: String
}

struct MobileInspectionRevisionValues: Codable, Equatable, Sendable {
    let notes: String
    let categoryId: String
    let workflowType: String
    let workflowStep: String
    let dueDate: String?
    let followUpDate: String?
    let followUpReason: String
    let technician: String
}

struct MobileInspectionCategoryOption: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
}

struct MobileInspectionTask: Codable, Equatable, Sendable {
    let id: String
    let schemaVersion: Int
    let createdAt: String
    let source: String
    let status: String
    let operation: String?
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
    let photos: [MobileInspectionPhoto]
    let sketches: [MobileInspectionSketch]?
    let files: [MobileInspectionFile]?
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

struct MobileInspectionFileInput: Identifiable, Equatable, Sendable {
    let id: String
    let fileName: String
    let data: Data
}

struct MobileInspectionDraft: Equatable, Sendable {
    var editingEntryID: String?
    var editingEntryDirectoryName: String?
    var originalCreatedAt: String?
    var desktopTaskId: String?
    var baseRevision: String?
    var confirmedRevision: String?
    var baseValues: MobileInspectionRevisionValues?
    var customerName: String = ""
    var address: String = ""
    var phone: String = ""
    var email: String = ""
    var title: String = ""
    var category: String = ""
    var categoryId: String = ""
    var workflowType: String = ""
    var workflowStep: String = ""
    var dueDate: String?
    var followUpDate: String?
    var followUpReason: String = ""
    var technician: String = ""
    var notes: String = ""
    var photos: [MobileInspectionPhotoInput] = []
    var sketches: [MobileInspectionSketchInput] = []
    var files: [MobileInspectionFileInput] = []

    var hasUserContent: Bool {
        let textValues = [
            desktopTaskId ?? "",
            customerName,
            address,
            phone,
            email,
            title,
            category,
            categoryId,
            workflowType,
            workflowStep,
            followUpReason,
            technician,
            notes
        ]
        return textValues.contains { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            || dueDate != nil
            || followUpDate != nil
            || !photos.isEmpty
            || !sketches.isEmpty
            || !files.isEmpty
    }

    var isEditingExistingEntry: Bool {
        editingEntryID?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
    }

    var isDesktopTaskUpdate: Bool {
        desktopTaskId?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
            && baseValues != nil
    }
}

struct MobileInspectionSaveResult: Equatable, Sendable {
    let entryID: String
    let entryURL: URL
}

struct MobileInboxPendingEntry: Identifiable, Equatable, Sendable {
    let id: String
    let customerName: String
    let address: String
    let phone: String
    let email: String
    let title: String
    let category: String
    let notes: String
    let createdAt: String
    let status: String
    let photoCount: Int
    let annotatedPhotoCount: Int
    let sketchCount: Int
    let fileCount: Int
    let attachmentIssueSummary: String?
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
        if fileCount == 1 {
            parts.append("1 Datei")
        } else if fileCount > 1 {
            parts.append("\(fileCount) Dateien")
        }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    var hasAttachmentIssue: Bool {
        attachmentIssueSummary?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false
    }

    var displayCreatedAt: String? {
        SnapshotDisplayFormatter.displayDate(createdAt)
    }
}
