import Foundation

struct MobileInspectionPhoto: Codable, Equatable, Sendable {
    let id: String
    let originalPath: String
    let previewPath: String
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
}

struct MobileInspectionPhotoInput: Identifiable, Equatable, Sendable {
    let id: String
    let fileName: String
    let data: Data
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
}

struct MobileInspectionSaveResult: Equatable, Sendable {
    let entryID: String
    let entryURL: URL
}
