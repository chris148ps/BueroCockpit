import Foundation

@MainActor
final class SnapshotBrowserViewModel: ObservableObject {
    static let allTasksCategoryID = "__all_tasks__"

    @Published private(set) var loadState: SnapshotLoadState = .idle
    @Published private(set) var document: SnapshotDocument?
    @Published private(set) var selectedFolderURL: URL?
    @Published private(set) var setupRequired = false
    @Published private(set) var setupMessage: String?
    @Published private(set) var noticeMessage: String?
    @Published private(set) var loadingTitle = "Snapshot wird geladen …"
    @Published private var groupedCategoryCache: [SnapshotCategoryGroup] = []
    @Published private var taskCountByCategoryID: [String: Int] = [:]
    @Published var selectedCategoryID: String = "__all_tasks__"
    @Published var selectedTaskID: String?
    @Published var searchText: String = "" {
        didSet {
            if filteredTasks.contains(where: { $0.id == selectedTaskID }) == false {
                selectedTaskID = filteredTasks.first?.id
            }
        }
    }

    private let reader: SnapshotReader
    private let accessStore: SnapshotAccessStore
    private var didAttemptStartupLoad = false

    init(
        reader: SnapshotReader = SnapshotReader(),
        accessStore: SnapshotAccessStore = SnapshotAccessStore()
    ) {
        self.reader = reader
        self.accessStore = accessStore
        setupRequired = !accessStore.isSetupCompleted
        loadState = accessStore.isSetupCompleted ? .loading : .idle
        SnapshotPerformanceLog.event("ViewModel init")
    }

    var metadata: SnapshotMetadata? {
        document?.metadata
    }

    var categories: [SnapshotCategoryGroup] {
        groupedCategoryCache
    }

    var tasks: [SnapshotTask] {
        document?.tasks ?? []
    }

    var attachments: [SnapshotAttachmentIndex] {
        document?.attachments ?? []
    }

    var filteredTasks: [SnapshotTask] {
        let categoryTasks: [SnapshotTask]
        if selectedCategoryID == Self.allTasksCategoryID {
            categoryTasks = tasks
        } else if let selectedGroup = categories.first(where: { $0.id == selectedCategoryID }) {
            let selectedCategoryIDs = Set(selectedGroup.categoryIDs)
            categoryTasks = tasks.filter { task in
                task.categoryIds.contains(where: { selectedCategoryIDs.contains($0) })
            }
        } else {
            categoryTasks = tasks
        }

        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else {
            return categoryTasks
        }

        return categoryTasks.filter { task in
            task.searchableText.localizedCaseInsensitiveContains(query)
        }
    }

    var selectedTask: SnapshotTask? {
        guard let selectedTaskID else {
            return filteredTasks.first
        }

        return filteredTasks.first(where: { $0.id == selectedTaskID }) ?? filteredTasks.first
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
        let directMatches = attachments.filter { $0.taskId == task.id }
        guard !task.attachmentRefs.isEmpty else {
            return directMatches.sorted { $0.sourceIndex < $1.sourceIndex }
        }

        let attachmentLookup = Dictionary(grouping: attachments, by: \.id)
        let refMatches = task.attachmentRefs.compactMap { attachmentLookup[$0]?.first }
        let missingDirectMatches = directMatches.filter { directMatch in
            !refMatches.contains(where: { $0.id == directMatch.id })
        }
        return (refMatches + missingDirectMatches).sorted { $0.sourceIndex < $1.sourceIndex }
    }

    func prepareAttachment(_ attachment: SnapshotAttachmentIndex) async throws -> URL {
        guard let sourceURL = document?.sourceURL else {
            throw SnapshotAttachmentError.missingFromSnapshot
        }

        let reader = self.reader
        return try await Task.detached(priority: .userInitiated) {
            try reader.prepareAttachment(attachment, from: sourceURL)
        }.value
    }

    func taskCount(in categoryID: String) -> Int {
        if categoryID == Self.allTasksCategoryID {
            return tasks.count
        }
        return taskCountByCategoryID[categoryID] ?? 0
    }

    var selectedCategoryTitle: String {
        if selectedCategoryID == Self.allTasksCategoryID {
            return "Alle Aufgaben"
        }

        return categories.first(where: { $0.id == selectedCategoryID })?.name ?? "Aufgaben"
    }

    var loadedFileName: String? {
        accessStore.savedFileName ?? document?.sourceURL.lastPathComponent
    }

    var hasLocalSnapshot: Bool {
        accessStore.hasLocalSnapshot
    }

    var loadingDescription: String {
        loadingTitle == "Lokale Live-Datei wird neu geladen …"
            ? "Bitte warten. Die App liest die bereits lokal gespeicherte Datei erneut."
            : "Bitte warten. Die ausgewählte Datei wird lokal kopiert und gelesen."
    }

    func restoreAtLaunch() {
        guard !didAttemptStartupLoad else {
            return
        }

        didAttemptStartupLoad = true
        guard accessStore.hasLocalSnapshot else {
            SnapshotPerformanceLog.event("Start local load skipped: no imported file")
            setupRequired = true
            loadState = .idle
            return
        }

        loadLocalSnapshot(isLaunch: true)
    }

    func importSnapshot(from sourceURL: URL) {
        SnapshotPerformanceLog.event("Import processing started")
        let hadLoadedDocument = document != nil
        searchText = ""
        noticeMessage = nil
        loadingTitle = "Snapshot wird geladen …"
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            do {
                let document = try await Task.detached(priority: .userInitiated) {
                    let document = try reader.readSnapshot(from: sourceURL)
                    if ["bclive", "bcsnapshot", "zip"].contains(document.sourceURL.pathExtension.lowercased()) {
                        accessStore.saveLocalSnapshot(fileName: document.sourceURL.lastPathComponent)
                    }
                    return Self.prepare(document: document)
                }.value

                setupRequired = false
                setupMessage = nil
                apply(prepared: document)
                SnapshotPerformanceLog.event("Import processing finished")
            } catch {
                let message = Self.displayMessage(for: error)
                if hadLoadedDocument {
                    setupRequired = false
                    setupMessage = nil
                    noticeMessage = "Import fehlgeschlagen. Die bisher angezeigten Daten wurden nicht ersetzt. \(message)"
                    loadState = .ready
                } else {
                    setupRequired = true
                    setupMessage = message
                    present(error: error)
                }
                SnapshotPerformanceLog.event("Import processing failed")
            }
        }
    }

    func refreshSnapshot() {
        guard accessStore.hasLocalSnapshot else {
            setupRequired = true
            setupMessage = "Bitte Sync/live.bclive erneut importieren."
            return
        }

        loadLocalSnapshot(isLaunch: false)
    }

    func resetSetup() {
        accessStore.reset()
        document = nil
        groupedCategoryCache = []
        taskCountByCategoryID = [:]
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil
        searchText = ""
        noticeMessage = nil
        setupMessage = nil
        setupRequired = true
        loadState = .idle
    }

    func dismissSetup() {
        if document != nil {
            setupRequired = false
        }
    }

    private func loadLocalSnapshot(isLaunch: Bool) {
        SnapshotPerformanceLog.event(isLaunch ? "Start local file load started" : "Manual local file reload started")
        searchText = ""
        loadingTitle = isLaunch ? "Lokale Live-Datei wird geladen …" : "Lokale Live-Datei wird neu geladen …"
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            let outcome = await Task.detached(priority: .userInitiated) {
                do {
                    let cachedURL = try accessStore.cachedSnapshotURL()
                    let document = try reader.readCachedSnapshot(from: cachedURL)
                    return LocalSnapshotLoadOutcome.loaded(Self.prepare(document: document))
                } catch {
                    return LocalSnapshotLoadOutcome.failure(Self.displayMessage(for: error))
                }
            }.value

            switch outcome {
            case .loaded(let prepared):
                noticeMessage = nil
                setupRequired = false
                setupMessage = nil
                apply(prepared: prepared)
                SnapshotPerformanceLog.event(isLaunch ? "Start local file load finished" : "Manual local file reload finished")
            case .failure(let message):
                let failureMessage = "Die lokale Live-Datei konnte nicht gelesen werden. Bitte Sync/live.bclive erneut importieren. \(message)"
                if document != nil {
                    noticeMessage = failureMessage
                    setupRequired = false
                    setupMessage = nil
                    loadState = .ready
                } else {
                    setupRequired = true
                    setupMessage = failureMessage
                    present(errorMessage: failureMessage)
                }
                SnapshotPerformanceLog.event(isLaunch ? "Start local file load failed" : "Manual local file reload failed")
            }
        }
    }

    nonisolated private static func displayMessage(for error: Error) -> String {
        if let localizedError = error as? LocalizedError,
           let description = localizedError.errorDescription {
            return description
        }

        return error.localizedDescription
    }

    private func apply(prepared: PreparedSnapshot) {
        document = prepared.document
        groupedCategoryCache = prepared.categories
        taskCountByCategoryID = prepared.taskCountByCategoryID
        selectedFolderURL = prepared.document.sourceURL

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
        SnapshotPerformanceLog.event("ViewModel state updated")
    }

    private func present(error: Error) {
        document = nil
        groupedCategoryCache = []
        taskCountByCategoryID = [:]
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil
        searchText = ""

        if let readerError = error as? SnapshotReaderError {
            loadState = .failure(readerError.localizedDescription)
        } else {
            loadState = .failure(error.localizedDescription)
        }
    }

    func present(errorMessage: String, keepSetupState: Bool = false) {
        document = nil
        groupedCategoryCache = []
        taskCountByCategoryID = [:]
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil
        searchText = ""
        if !keepSetupState {
            setupRequired = true
        }
        loadState = .failure(errorMessage)
    }

    nonisolated private static func prepare(document: SnapshotDocument) -> PreparedSnapshot {
        let groups = groupedCategories(from: document.categories)
        var taskCounts: [String: Int] = [:]

        for group in groups {
            let categoryIDs = Set(group.categoryIDs)
            taskCounts[group.id] = document.tasks.reduce(into: 0) { count, task in
                if task.categoryIds.contains(where: categoryIDs.contains) {
                    count += 1
                }
            }
        }

        SnapshotPerformanceLog.event("Category grouping finished")

        return PreparedSnapshot(
            document: document,
            categories: groups,
            taskCountByCategoryID: taskCounts
        )
    }

    nonisolated private static func groupedCategories(from categories: [SnapshotCategory]) -> [SnapshotCategoryGroup] {
        struct Bucket {
            var name: String
            var categoryIDs: [String]
            var order: Int
        }

        var buckets: [String: Bucket] = [:]
        var orderedKeys: [String] = []

        for (index, category) in categories.enumerated() {
            let trimmedName = category.name.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmedName.isEmpty else {
                continue
            }

            let key = trimmedName.lowercased()
            if var bucket = buckets[key] {
                if !bucket.categoryIDs.contains(category.id) {
                    bucket.categoryIDs.append(category.id)
                }
                bucket.order = min(bucket.order, category.order ?? index)
                buckets[key] = bucket
            } else {
                buckets[key] = Bucket(
                    name: trimmedName,
                    categoryIDs: [category.id],
                    order: category.order ?? index
                )
                orderedKeys.append(key)
            }
        }

        return orderedKeys.compactMap { key in
            guard let bucket = buckets[key] else {
                return nil
            }

            return SnapshotCategoryGroup(
                id: key,
                name: bucket.name,
                categoryIDs: bucket.categoryIDs,
                order: bucket.order
            )
        }
        .sorted {
            if $0.order != $1.order {
                return $0.order < $1.order
            }

            return $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending
        }
    }
}

private enum LocalSnapshotLoadOutcome: Sendable {
    case loaded(PreparedSnapshot)
    case failure(String)
}

private struct PreparedSnapshot: Sendable {
    let document: SnapshotDocument
    let categories: [SnapshotCategoryGroup]
    let taskCountByCategoryID: [String: Int]
}
