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
    }

    var metadata: SnapshotMetadata? {
        document?.metadata
    }

    var categories: [SnapshotCategoryGroup] {
        groupedCategories(from: document?.categories ?? [])
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

        guard let selectedGroup = categories.first(where: { $0.id == categoryID }) else {
            return 0
        }

        let selectedCategoryIDs = Set(selectedGroup.categoryIDs)
        return tasks.filter { task in
            task.categoryIds.contains(where: { selectedCategoryIDs.contains($0) })
        }.count
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

    func restoreAtLaunch() {
        guard !didAttemptStartupLoad else {
            return
        }

        didAttemptStartupLoad = true
        guard accessStore.hasSavedLocation else {
            setupRequired = true
            loadState = .idle
            return
        }

        loadSavedSnapshot()
    }

    func importSnapshot(from sourceURL: URL) {
        let hadLoadedDocument = document != nil
        searchText = ""
        noticeMessage = nil
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            do {
                let document = try await Task.detached(priority: .userInitiated) {
                    let bookmark = try accessStore.makeBookmark(for: sourceURL)
                    let document = try reader.readSnapshot(from: sourceURL)
                    accessStore.save(bookmark: bookmark, fileName: sourceURL.lastPathComponent)
                    return document
                }.value

                setupRequired = false
                setupMessage = nil
                apply(document: document)
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

    private func loadSavedSnapshot() {
        searchText = ""
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
                apply(document: document)
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
                    try reader.readSnapshot(from: location.url),
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
                document,
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

    private func apply(document: SnapshotDocument) {
        self.document = document
        selectedFolderURL = document.sourceURL

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

    private func present(error: Error) {
        document = nil
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
        selectedFolderURL = nil
        selectedCategoryID = Self.allTasksCategoryID
        selectedTaskID = nil
        searchText = ""
        if !keepSetupState {
            setupRequired = true
        }
        loadState = .failure(errorMessage)
    }

    private func groupedCategories(from categories: [SnapshotCategory]) -> [SnapshotCategoryGroup] {
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
        SnapshotDocument,
        notice: String?,
        requiresSetup: Bool,
        setupMessage: String?
    )
    case failure(String, requiresSetup: Bool)
}
