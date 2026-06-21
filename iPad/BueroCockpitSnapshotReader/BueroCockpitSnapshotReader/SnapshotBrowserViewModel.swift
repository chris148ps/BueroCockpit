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

    var hasSavedSnapshotLocation: Bool {
        accessStore.hasSavedLocation
    }

    var loadingDescription: String {
        loadingTitle == "Snapshot wird aktualisiert …"
            ? "Bitte warten. Die App lädt den gespeicherten Snapshot-Ort neu."
            : "Bitte warten. Die App liest die lokale Snapshot-Kopie ein."
    }

    func restoreAtLaunch() {
        guard !didAttemptStartupLoad else {
            return
        }

        didAttemptStartupLoad = true
        guard accessStore.hasSavedLocation else {
            SnapshotPerformanceLog.event("Start auto-load skipped: no saved location")
            setupRequired = true
            loadState = .idle
            return
        }

        loadCachedSnapshotAtLaunch()
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
                    let bookmark = try accessStore.makeBookmark(for: sourceURL)
                    let document = try reader.readSnapshot(from: sourceURL)
                    accessStore.save(bookmark: bookmark, fileName: sourceURL.lastPathComponent)
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
                    noticeMessage = "Der ausgewählte Snapshot konnte nicht geladen werden: \(message)"
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
        guard accessStore.hasSavedLocation else {
            setupRequired = true
            setupMessage = "Bitte wähle zuerst einen Snapshot aus."
            return
        }

        loadSavedSnapshot()
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

    private func loadCachedSnapshotAtLaunch() {
        SnapshotPerformanceLog.event("Start local cache load started")
        loadingTitle = "Snapshot wird geladen …"
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            let outcome = await Task.detached(priority: .utility) {
                do {
                    let cachedURL = try accessStore.cachedSnapshotURL()
                    SnapshotPerformanceLog.event("Start local cache located")
                    let document = try reader.readCachedSnapshot(from: cachedURL)
                    return StartCacheLoadOutcome.loaded(Self.prepare(document: document))
                } catch {
                    return StartCacheLoadOutcome.failure(Self.displayMessage(for: error))
                }
            }.value

            switch outcome {
            case .loaded(let prepared):
                setupRequired = false
                setupMessage = nil
                noticeMessage = nil
                apply(prepared: prepared)
                SnapshotPerformanceLog.event("Start local cache load finished")
            case .failure(let message):
                setupRequired = false
                present(
                    errorMessage: "Die lokale Snapshot-Kopie konnte nicht geladen werden: \(message)",
                    keepSetupState: true
                )
                SnapshotPerformanceLog.event("Start local cache load failed")
            }
        }
    }

    private func loadSavedSnapshot() {
        SnapshotPerformanceLog.event("Manual bookmark refresh started")
        searchText = ""
        loadingTitle = "Snapshot wird aktualisiert …"
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            let outcome = await Task.detached(priority: .userInitiated) {
                Self.loadSavedSnapshot(reader: reader, accessStore: accessStore)
            }.value

            switch outcome {
            case .loaded(let document, let notice, let requiresSetup, let message):
                noticeMessage = notice
                setupRequired = requiresSetup
                setupMessage = message
                apply(prepared: document)
                SnapshotPerformanceLog.event("Manual bookmark refresh finished")
            case .failure(let message, let requiresSetup):
                noticeMessage = nil
                setupRequired = requiresSetup
                setupMessage = requiresSetup ? message : nil
                if document != nil {
                    noticeMessage = message
                    loadState = .ready
                } else {
                    present(errorMessage: message, keepSetupState: true)
                }
                SnapshotPerformanceLog.event("Manual bookmark refresh failed")
            }
        }
    }

    nonisolated private static func loadSavedSnapshot(
        reader: SnapshotReader,
        accessStore: SnapshotAccessStore
    ) -> SavedSnapshotLoadOutcome {
        do {
            let location = try accessStore.resolveSavedLocation()
            if location.isStale {
                return cachedOutcome(
                    reader: reader,
                    accessStore: accessStore,
                    failureMessage: SnapshotAccessError.staleBookmark.localizedDescription,
                    requiresSetup: true
                )
            }

            do {
                return .loaded(
                    prepare(document: try reader.readSnapshot(from: location.url)),
                    notice: nil,
                    requiresSetup: false,
                    setupMessage: nil
                )
            } catch {
                return cachedOutcome(
                    reader: reader,
                    accessStore: accessStore,
                    failureMessage: "Gespeicherter Snapshot konnte nicht aktualisiert werden. Es wird die letzte lokale Kopie angezeigt.",
                    requiresSetup: false
                )
            }
        } catch {
            return cachedOutcome(
                reader: reader,
                accessStore: accessStore,
                failureMessage: displayMessage(for: error),
                requiresSetup: true
            )
        }
    }

    nonisolated private static func cachedOutcome(
        reader: SnapshotReader,
        accessStore: SnapshotAccessStore,
        failureMessage: String,
        requiresSetup: Bool
    ) -> SavedSnapshotLoadOutcome {
        do {
            let cachedURL = try accessStore.cachedSnapshotURL()
            let document = try reader.readCachedSnapshot(from: cachedURL)
            return .loaded(
                prepare(document: document),
                notice: "Gespeicherter Snapshot konnte nicht aktualisiert werden. Es wird die letzte lokale Kopie angezeigt.",
                requiresSetup: requiresSetup,
                setupMessage: requiresSetup ? failureMessage : nil
            )
        } catch {
            return .failure(failureMessage, requiresSetup: requiresSetup)
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

private enum SavedSnapshotLoadOutcome: Sendable {
    case loaded(
        PreparedSnapshot,
        notice: String?,
        requiresSetup: Bool,
        setupMessage: String?
    )
    case failure(String, requiresSetup: Bool)
}

private enum StartCacheLoadOutcome: Sendable {
    case loaded(PreparedSnapshot)
    case failure(String)
}

private struct PreparedSnapshot: Sendable {
    let document: SnapshotDocument
    let categories: [SnapshotCategoryGroup]
    let taskCountByCategoryID: [String: Int]
}
