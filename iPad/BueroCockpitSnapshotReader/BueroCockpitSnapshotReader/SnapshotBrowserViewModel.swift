import Foundation

@MainActor
final class SnapshotBrowserViewModel: ObservableObject {
    static let allTasksCategoryID = "__all_tasks__"

    @Published private(set) var loadState: SnapshotLoadState = .idle
    @Published private(set) var document: SnapshotDocument?
    @Published private(set) var selectedFolderURL: URL?
    @Published var selectedCategoryID: String = "__all_tasks__"
    @Published var selectedTaskID: String?

    private let reader: SnapshotReader

    init(reader: SnapshotReader = SnapshotReader()) {
        self.reader = reader
    }

    var metadata: SnapshotMetadata? {
        document?.metadata
    }

    var categories: [SnapshotCategory] {
        document?.categories ?? []
    }

    var tasks: [SnapshotTask] {
        document?.tasks ?? []
    }

    var attachments: [SnapshotAttachmentIndex] {
        document?.attachments ?? []
    }

    var filteredTasks: [SnapshotTask] {
        guard selectedCategoryID != Self.allTasksCategoryID else {
            return tasks
        }

        return tasks.filter { task in
            task.categoryIds.contains(selectedCategoryID)
        }
    }

    var selectedTask: SnapshotTask? {
        guard let selectedTaskID else {
            return filteredTasks.first
        }

        return filteredTasks.first(where: { $0.id == selectedTaskID }) ?? filteredTasks.first
    }

    func loadSnapshot(from sourceURL: URL) {
        loadState = .loading
        Task { [reader] in
            do {
                let document = try reader.readSnapshot(from: sourceURL)
                await apply(document: document)
            } catch {
                await present(error: error)
            }
        }
    }

    func selectAllTasks() {
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = filteredTasks.first?.id
    }

    func selectCategory(_ categoryID: String) {
        selectedCategoryID = categoryID
        selectedTaskID = filteredTasks.first?.id
    }

    func selectTask(_ taskID: String?) {
        selectedTaskID = taskID
    }

    func attachments(for task: SnapshotTask) -> [SnapshotAttachmentIndex] {
        guard !task.attachmentRefs.isEmpty else {
            return []
        }

        let attachmentLookup = Dictionary(uniqueKeysWithValues: attachments.map { ($0.id, $0) })
        return task.attachmentRefs.compactMap { attachmentLookup[$0] }
    }

    func taskCount(in categoryID: String) -> Int {
        if categoryID == Self.allTasksCategoryID {
            return tasks.count
        }

        return tasks.filter { $0.categoryIds.contains(categoryID) }.count
    }

    private func apply(document: SnapshotDocument) async {
        self.document = document
        selectedFolderURL = document.sourceURL

        if categories.isEmpty || tasks.isEmpty {
            loadState = .empty("Im Snapshot wurden keine Kategorien oder Aufgaben gefunden.")
            selectedCategoryID = Self.allTasksCategoryID
            selectedTaskID = nil
            return
        }

        if selectedCategoryID != Self.allTasksCategoryID,
           !categories.contains(where: { $0.id == selectedCategoryID }) {
            selectedCategoryID = Self.allTasksCategoryID
        }

        if selectedCategoryID == Self.allTasksCategoryID {
            selectedTaskID = tasks.first?.id
        } else if filteredTasks.first(where: { $0.id == selectedTaskID }) == nil {
            selectedTaskID = filteredTasks.first?.id
        }

        loadState = .ready
    }

    private func present(error: Error) async {
        document = nil
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil

        if let readerError = error as? SnapshotReaderError {
            loadState = .failure(readerError.localizedDescription)
        } else {
            loadState = .failure(error.localizedDescription)
        }
    }

    func present(errorMessage: String) {
        document = nil
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil
        loadState = .failure(errorMessage)
    }
}
