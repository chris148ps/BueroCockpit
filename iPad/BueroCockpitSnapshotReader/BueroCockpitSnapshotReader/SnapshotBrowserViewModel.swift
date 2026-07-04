import Foundation
import UIKit

@MainActor
final class SnapshotBrowserViewModel: ObservableObject {
    nonisolated static let allTasksCategoryID = "__all_tasks__"
    nonisolated static let mobilePendingCategoryID = "__mobile_pending__"

    @Published private(set) var loadState: SnapshotLoadState = .idle
    @Published private(set) var document: SnapshotDocument?
    @Published private(set) var selectedFolderURL: URL?
    @Published private(set) var setupRequired = false
    @Published private(set) var setupMessage: String?
    @Published private(set) var noticeMessage: String?
    @Published private(set) var syncStatusMessage: String?
    @Published private(set) var localNetworkDesktopAutoCheckMessage: String?
    @Published private(set) var discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop] = []
    @Published private(set) var syncSettings: SnapshotSyncSettings
    @Published private(set) var isSyncing = false
    @Published private(set) var loadingTitle = "Snapshot wird geladen …"
    @Published private(set) var mobileInboxEntries: [MobileInboxPendingEntry] = []
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
    private let syncSettingsStore: SnapshotSyncSettingsStore
    private let mobileInboxReader: MobileInboxReader
    private var didAttemptStartupLoad = false
    private let localNetworkDesktopTestPort = 53941
    private let localNetworkDesktopInitialAutoCheckDelayNanoseconds: UInt64 = 3_000_000_000
    private let localNetworkDesktopAutoCheckIntervalNanoseconds: UInt64 = 30_000_000_000
    private var localNetworkDesktopAutoCheckTask: Task<Void, Never>?
    private var isLocalNetworkDesktopServiceCheckRunning = false
    private let localNetworkDesktopDiscovery = LocalNetworkDesktopDiscovery()

    init(
        reader: SnapshotReader = SnapshotReader(),
        accessStore: SnapshotAccessStore = SnapshotAccessStore(),
        syncSettingsStore: SnapshotSyncSettingsStore = SnapshotSyncSettingsStore(),
        mobileInboxReader: MobileInboxReader = MobileInboxReader()
    ) {
        self.reader = reader
        self.accessStore = accessStore
        self.syncSettingsStore = syncSettingsStore
        self.mobileInboxReader = mobileInboxReader
        syncSettings = syncSettingsStore.load()
        setupRequired = !accessStore.isSetupCompleted
        loadState = accessStore.isSetupCompleted ? .loading : .idle
        SnapshotPerformanceLog.event("ViewModel init")
    }

    deinit {
        localNetworkDesktopAutoCheckTask?.cancel()
        localNetworkDesktopDiscovery.stop()
    }

    var metadata: SnapshotMetadata? {
        document?.metadata
    }

    var categories: [SnapshotCategoryGroup] {
        if mobileInboxEntries.isEmpty {
            return groupedCategoryCache
        }

        return [
            SnapshotCategoryGroup(
                id: Self.mobilePendingCategoryID,
                name: "Wartet auf Freigabe",
                categoryIDs: [Self.mobilePendingCategoryID],
                order: Int.min
            )
        ] + groupedCategoryCache
    }

    var tasks: [SnapshotTask] {
        document?.tasks ?? []
    }

    var attachments: [SnapshotAttachmentIndex] {
        document?.attachments ?? []
    }

    var filteredTasks: [SnapshotTask] {
        if selectedCategoryID == Self.mobilePendingCategoryID {
            let mobileTasks = mobileInboxEntries.enumerated().map { index, entry in
                Self.snapshotTask(from: entry, sourceIndex: index)
            }
            let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !query.isEmpty else {
                return mobileTasks
            }
            return mobileTasks.filter { $0.searchableText.localizedCaseInsensitiveContains(query) }
        }

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

    func mobileInboxEntry(forTaskID taskID: String) -> MobileInboxPendingEntry? {
        guard taskID.hasPrefix("mobile-inbox-") else {
            return nil
        }

        let entryID = String(taskID.dropFirst("mobile-inbox-".count))
        return mobileInboxEntries.first { $0.id == entryID }
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
        if categoryID == Self.mobilePendingCategoryID {
            return mobileInboxEntries.count
        }
        if categoryID == Self.allTasksCategoryID {
            return tasks.count
        }
        return taskCountByCategoryID[categoryID] ?? 0
    }

    var selectedCategoryTitle: String {
        if selectedCategoryID == Self.allTasksCategoryID {
            return "Alle Aufgaben"
        }
        if selectedCategoryID == Self.mobilePendingCategoryID {
            return "Wartet auf Freigabe"
        }

        return categories.first(where: { $0.id == selectedCategoryID })?.name ?? "Aufgaben"
    }

    var loadedFileName: String? {
        accessStore.savedFileName ?? document?.sourceURL.lastPathComponent
    }

    var hasLocalSnapshot: Bool {
        accessStore.hasLocalSnapshot
    }

    var hasICloudSnapshotSource: Bool {
        accessStore.hasICloudSourceBookmark
    }

    var isICloudDriveActive: Bool {
        syncSettings.providerType == .iCloudDrive
    }

    var syncLastUpdatedText: String? {
        let date: Date?
        switch syncSettings.providerType {
        case .manualFile:
            date = syncSettings.lastImportDate
        case .iCloudDrive, .googleDriveDirect:
            date = syncSettings.lastSuccessfulSync ?? syncSettings.lastImportDate
        case .webDavNas, .dropbox, .microsoftGraphOneDrive:
            date = syncSettings.lastSuccessfulSync ?? syncSettings.lastImportDate
        }
        guard let date else {
            return nil
        }

        return "Zuletzt aktualisiert: \(Self.formatSyncDate(date))"
    }

    var localNetworkDesktopAddress: String {
        syncSettings.localNetworkDesktop.desktopAddress ?? ""
    }

    var localNetworkDesktopPort: Int {
        syncSettings.localNetworkDesktop.desktopPort ?? localNetworkDesktopTestPort
    }

    var localNetworkDesktopLastSuccessfulCheckText: String? {
        guard let date = syncSettings.localNetworkDesktop.lastSuccessfulCheckAt else {
            return nil
        }
        return Self.formatSyncDate(date)
    }

    var localNetworkDesktopStatus: String? {
        syncSettings.localNetworkDesktop.localDesktopStatus
    }

    private var isLocalNetworkDesktopRemembered: Bool {
        Self.isRememberedLocalNetworkDesktop(syncSettings.localNetworkDesktop)
    }

    var loadingDescription: String {
        if loadingTitle == "Google Drive wird aktualisiert …" {
            return "Bitte warten. Die App lädt die Datei einmalig und prüft sie vor der lokalen Übernahme."
        }
        if loadingTitle == "iCloud Drive wird aktualisiert …" {
            return "Bitte warten. Die App liest die ausgewählte Datei und prüft sie vor der lokalen Übernahme."
        }
        return "Bitte warten. Die lokale Datei wird gelesen."
    }

    func restoreAtLaunch() {
        guard !didAttemptStartupLoad else {
            return
        }

        didAttemptStartupLoad = true
        loadMobileInboxEntries()
        if syncSettings.providerType == .googleDriveDirect,
           !syncSettings.googleDriveFileId.isEmpty {
            updateFromGoogleDrive(
                link: syncSettings.googleDriveLink,
                fileID: syncSettings.googleDriveFileId,
                saveConfigurationOnSuccess: false,
                isLaunch: true
            )
        } else if accessStore.hasLocalSnapshot {
            loadLocalSnapshot(isLaunch: true)
        } else {
            SnapshotPerformanceLog.event("Start local load skipped: no imported live file")
            setupRequired = true
            setupMessage = "Bitte live.bclive einmal importieren."
            loadState = .idle
        }
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
                var settings = syncSettings
                settings.providerType = .manualFile
                settings.lastImportDate = Date()
                settings.lastError = nil
                syncSettings = settings
                syncSettingsStore.save(settings)
                syncStatusMessage = "Lokale Datei geladen"
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
        loadMobileInboxEntries()
        if syncSettings.providerType == .iCloudDrive {
            refreshICloudSnapshot()
            return
        }

        if syncSettings.providerType == .googleDriveDirect,
           !syncSettings.googleDriveFileId.isEmpty {
            updateFromGoogleDrive(
                link: syncSettings.googleDriveLink,
                fileID: syncSettings.googleDriveFileId,
                saveConfigurationOnSuccess: false,
                isLaunch: false
            )
            return
        }

        guard accessStore.hasLocalSnapshot else {
            setupRequired = true
            setupMessage = "Bitte live.bclive einmal importieren."
            return
        }
        loadLocalSnapshot(isLaunch: false)
    }

    func testGoogleDriveConnection(link: String) {
        guard let fileID = GoogleDriveDirectProvider.fileID(from: link) else {
            syncStatusMessage = SnapshotSyncError.invalidGoogleDriveLink.localizedDescription
            return
        }
        updateFromGoogleDrive(
            link: link.trimmingCharacters(in: .whitespacesAndNewlines),
            fileID: fileID,
            saveConfigurationOnSuccess: true,
            isLaunch: false
        )
    }

    func testLocalNetworkDesktopService(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalizedAddress.isEmpty else {
            syncStatusMessage = "Bitte Desktop-Adresse oder IP eintragen."
            return
        }

        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        loadingTitle = "Prüfung läuft …"
        syncStatusMessage = "Prüfung läuft …"

        Task {
            defer {
                isLocalNetworkDesktopServiceCheckRunning = false
            }

            do {
                let response = try await Self.fetchLocalNetworkDesktopStatus(
                    address: normalizedAddress,
                    port: localNetworkDesktopTestPort
                )
                if response.isExpectedLocalNetworkTestStatus {
                    saveSuccessfulLocalNetworkDesktopCheck(address: normalizedAddress)
                    syncStatusMessage = isLocalNetworkDesktopRemembered
                        ? "Lokaler Desktop vorgemerkt"
                        : "Desktop-Testdienst erreichbar"
                } else {
                    syncStatusMessage = "Desktop-Testdienst nicht erreichbar: unerwartete Antwort"
                }
            } catch {
                syncStatusMessage = "Desktop-Testdienst nicht erreichbar: \(Self.shortLocalNetworkDesktopError(error))"
            }
        }
    }

    func startLocalNetworkDesktopAutoCheck(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        localNetworkDesktopAutoCheckTask?.cancel()

        guard !normalizedAddress.isEmpty else {
            localNetworkDesktopAutoCheckMessage = "Desktop-Adresse fehlt"
            return
        }

        localNetworkDesktopAutoCheckMessage = "Erste Prüfung in ca. 3 Sekunden"
        let initialDelay = localNetworkDesktopInitialAutoCheckDelayNanoseconds
        let checkInterval = localNetworkDesktopAutoCheckIntervalNanoseconds
        localNetworkDesktopAutoCheckTask = Task { @MainActor [weak self] in
            do {
                try await Task.sleep(nanoseconds: initialDelay)
            } catch {
                return
            }

            while !Task.isCancelled {
                guard let self else { return }
                await self.runLocalNetworkDesktopAutoCheck(address: normalizedAddress)
                guard !Task.isCancelled else { return }
                self.localNetworkDesktopAutoCheckMessage = "Nächste Prüfung in ca. 30 Sekunden"

                do {
                    try await Task.sleep(nanoseconds: checkInterval)
                } catch {
                    return
                }
            }
        }
    }

    func stopLocalNetworkDesktopAutoCheck() {
        localNetworkDesktopAutoCheckTask?.cancel()
        localNetworkDesktopAutoCheckTask = nil
        localNetworkDesktopAutoCheckMessage = nil
    }

    func startLocalNetworkDesktopDiscovery() {
        localNetworkDesktopDiscovery.start { [weak self] desktops in
            guard let self else { return }
            discoveredLocalNetworkDesktops = desktops
            if desktops.isEmpty, isLocalNetworkDesktopRemembered {
                localNetworkDesktopAutoCheckMessage = "Automatische Suche hat aktuell keinen Desktop gefunden."
                syncStatusMessage = "Lokaler Desktop vorgemerkt"
            }
            adoptRememberedLocalNetworkDesktopIfFound(in: desktops)
        }
    }

    func stopLocalNetworkDesktopDiscovery() {
        localNetworkDesktopDiscovery.stop()
        discoveredLocalNetworkDesktops = []
    }

    func useDiscoveredLocalNetworkDesktop(_ desktop: LocalNetworkDiscoveredDesktop) {
        saveDiscoveredLocalNetworkDesktop(desktop, status: "Desktop im lokalen Netzwerk gefunden")
        syncStatusMessage = "Desktop im lokalen Netzwerk gefunden"
    }

    func markLocalNetworkDesktopAsPreferred(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalizedAddress.isEmpty else {
            syncStatusMessage = "Bitte Desktop-Adresse oder IP eintragen."
            return
        }

        let deviceId = ensureLocalNetworkIpadIdentity()
        let deviceName = Self.currentIpadDeviceName()
        var settings = syncSettings
        settings.localNetworkDesktop.desktopAddress = normalizedAddress
        settings.localNetworkDesktop.desktopPort = localNetworkDesktopTestPort
        settings.localNetworkDesktop.localDesktopStatus = "Lokaler Desktop vorgemerkt"
        settings.localNetworkDesktop.lastSuccessfulCheckAt = settings.localNetworkDesktop.lastSuccessfulCheckAt ?? Date()
        settings.localNetworkDesktop.ipadDeviceId = deviceId
        settings.localNetworkDesktop.ipadDeviceName = deviceName
        settings.localNetworkDesktop.ipadPlatform = "iPadOS"
        syncSettings = settings
        syncSettingsStore.save(settings)
        syncStatusMessage = "Lokaler Desktop vorgemerkt"

        Task {
            do {
                let response = try await Self.rememberIpadAtDesktop(
                    address: normalizedAddress,
                    port: localNetworkDesktopTestPort,
                    deviceId: deviceId,
                    deviceName: deviceName,
                    appVersion: Self.currentAppVersion()
                )
                syncStatusMessage = response.isExpectedRememberResponse
                    ? "Desktop vorgemerkt, iPad am Desktop registriert."
                    : "Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich."
            } catch {
                syncStatusMessage = "Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich."
            }
        }
    }

    func resetLocalNetworkDesktopPreferenceIfAddressChanged(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard normalizedAddress != (syncSettings.localNetworkDesktop.desktopAddress ?? "") else {
            return
        }
        guard syncSettings.localNetworkDesktop.lastSuccessfulCheckAt != nil ||
            syncSettings.localNetworkDesktop.localDesktopStatus != nil else {
            return
        }

        var settings = syncSettings
        settings.localNetworkDesktop.desktopAddress = normalizedAddress.isEmpty ? nil : normalizedAddress
        settings.localNetworkDesktop.desktopPort = localNetworkDesktopTestPort
        settings.localNetworkDesktop.lastSuccessfulCheckAt = nil
        settings.localNetworkDesktop.localDesktopStatus = nil
        syncSettings = settings
        syncSettingsStore.save(settings)
        syncStatusMessage = normalizedAddress.isEmpty ? "Desktop-Adresse fehlt" : "Bereit zur Prüfung"
    }

    func importICloudSnapshot(from sourceURL: URL) {
        updateFromICloudDrive(
            sourceURL: sourceURL,
            saveBookmarkOnSuccess: true,
            successMessage: "iCloud-Datei geladen",
            keepCurrentView: false
        )
    }

    @discardableResult
    func refreshICloudSnapshot(keepCurrentView: Bool = false) -> Bool {
        do {
            let sourceURL = try accessStore.resolveICloudSourceURL()
            updateFromICloudDrive(
                sourceURL: sourceURL,
                saveBookmarkOnSuccess: false,
                successMessage: "Aus iCloud Drive aktualisiert",
                keepCurrentView: keepCurrentView
            )
            return true
        } catch {
            let message = Self.displayMessage(for: error)
            syncStatusMessage = message
            noticeMessage = message
            accessStore.clearICloudSourceBookmark()
            if document == nil {
                setupRequired = true
                setupMessage = message
                loadState = .idle
            }
            return false
        }
    }

    func requestICloudSourceSelection() {
        let message = "Bitte iCloud-Datei erneut auswählen"
        syncStatusMessage = message
        if document != nil {
            noticeMessage = message
        } else {
            setupMessage = message
        }
    }

    func clearNotice() {
        noticeMessage = nil
    }

    func loadMobileInboxEntries(selectCategory: Bool = false) {
        let reader = mobileInboxReader
        Task {
            let result = await Task.detached(priority: .userInitiated) {
                do {
                    return try reader.loadPendingEntries()
                } catch {
                    return []
                }
            }.value

            mobileInboxEntries = result
            if selectCategory, !result.isEmpty {
                selectedCategoryID = Self.mobilePendingCategoryID
                selectedTaskID = filteredTasks.first?.id
            } else if selectedCategoryID == Self.mobilePendingCategoryID, result.isEmpty {
                selectedCategoryID = Self.allTasksCategoryID
                selectedTaskID = filteredTasks.first?.id
            } else if selectedCategoryID == Self.mobilePendingCategoryID {
                selectedTaskID = filteredTasks.first?.id
            }
        }
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
        syncStatusMessage = nil
        setupMessage = nil
        syncSettingsStore.reset()
        syncSettings = SnapshotSyncSettings()
        setupRequired = true
        loadState = .idle
    }

    func dismissSetup() {
        if document != nil {
            setupRequired = false
        }
    }

    private func loadLocalSnapshot(isLaunch: Bool) {
        SnapshotPerformanceLog.event(isLaunch ? "Start local load started" : "Manual local reload started")
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
                syncStatusMessage = syncSettings.providerType == .manualFile ? "Lokale Datei geladen" : syncStatusMessage
                setupRequired = false
                setupMessage = nil
                apply(prepared: prepared)
                SnapshotPerformanceLog.event(isLaunch ? "Start local load finished" : "Manual local reload finished")
            case .failure(let message):
                let failureMessage = "Bitte live.bclive einmal importieren. \(message)"
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
                SnapshotPerformanceLog.event(isLaunch ? "Start local load failed" : "Manual local reload failed")
            }
        }
    }

    private func updateFromGoogleDrive(
        link: String,
        fileID: String,
        saveConfigurationOnSuccess: Bool,
        isLaunch: Bool
    ) {
        guard !isSyncing else { return }
        isSyncing = true
        searchText = ""
        noticeMessage = nil
        syncStatusMessage = "Google Drive wird geprüft …"
        loadingTitle = "Google Drive wird aktualisiert …"
        loadState = .loading

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            let outcome: GoogleSnapshotLoadOutcome
            do {
                let provider = GoogleDriveDirectProvider(fileID: fileID)
                let downloadedURL = try await provider.downloadLatestSnapshot()
                defer { try? FileManager.default.removeItem(at: downloadedURL) }
                let prepared = try await Task.detached(priority: .userInitiated) {
                    _ = try reader.readCachedSnapshot(from: downloadedURL)
                    let installedURL = try accessStore.installDownloadedLiveSnapshot(from: downloadedURL)
                    return Self.prepare(document: try reader.readCachedSnapshot(from: installedURL))
                }.value
                outcome = .online(prepared)
            } catch {
                let onlineError = Self.displayMessage(for: error)
                let fallback = await Task.detached(priority: .userInitiated) { () -> PreparedSnapshot? in
                    do {
                        let cachedURL = try accessStore.cachedSnapshotURL()
                        return try Self.prepare(document: reader.readCachedSnapshot(from: cachedURL))
                    } catch {
                        return nil
                    }
                }.value
                outcome = fallback.map { .fallback($0, onlineError: onlineError) } ?? .failure(onlineError)
            }

            isSyncing = false
            switch outcome {
            case .online(let prepared):
                var settings = syncSettings
                if saveConfigurationOnSuccess {
                    settings.providerType = .googleDriveDirect
                    settings.googleDriveLink = link
                    settings.googleDriveFileId = fileID
                }
                settings.lastSuccessfulSync = Date()
                settings.lastError = nil
                syncSettings = settings
                syncSettingsStore.save(settings)
                syncStatusMessage = saveConfigurationOnSuccess
                    ? "Google Drive Verbindung erfolgreich"
                    : "Online aktualisiert"
                setupRequired = false
                setupMessage = nil
                apply(prepared: prepared)
                SnapshotPerformanceLog.event(isLaunch ? "Start Google Drive load finished" : "Manual Google Drive load finished")
            case .fallback(let prepared, let onlineError):
                var settings = syncSettings
                settings.lastError = onlineError
                syncSettings = settings
                syncSettingsStore.save(settings)
                syncStatusMessage = "Online-Aktualisierung fehlgeschlagen. Lokale Kopie wird verwendet."
                noticeMessage = syncStatusMessage
                setupRequired = false
                setupMessage = nil
                apply(prepared: prepared)
            case .failure(let onlineError):
                var settings = syncSettings
                settings.lastError = onlineError
                syncSettings = settings
                syncSettingsStore.save(settings)
                let message = "Online-Aktualisierung fehlgeschlagen. Lokale Kopie wird verwendet. \(onlineError)"
                syncStatusMessage = message
                setupRequired = true
                setupMessage = message
                present(errorMessage: message, keepSetupState: true)
            }
        }
    }

    private func updateFromICloudDrive(
        sourceURL: URL,
        saveBookmarkOnSuccess: Bool,
        successMessage: String,
        keepCurrentView: Bool
    ) {
        guard !isSyncing else { return }
        isSyncing = true
        searchText = ""
        noticeMessage = nil
        syncStatusMessage = "Aktualisiere iCloud-Datei …"
        loadingTitle = "iCloud Drive wird aktualisiert …"
        if !keepCurrentView {
            loadState = .loading
        }

        let reader = self.reader
        let accessStore = self.accessStore
        Task {
            let outcome: ICloudSnapshotLoadOutcome
            do {
                let imported = try await Task.detached(priority: .userInitiated) {
                    let importResult = try accessStore.copySecurityScopedLiveSnapshotToTemporary(
                        from: sourceURL,
                        saveBookmark: saveBookmarkOnSuccess
                    )
                    let temporaryURL = importResult.temporaryURL
                    defer { try? FileManager.default.removeItem(at: temporaryURL) }
                    _ = try reader.readCachedSnapshot(from: temporaryURL)
                    let installedURL = try accessStore.installDownloadedLiveSnapshot(from: temporaryURL)
                    return (
                        prepared: Self.prepare(document: try reader.readCachedSnapshot(from: installedURL)),
                        bookmarkWarning: importResult.bookmarkWarning
                    )
                }.value

                outcome = .loaded(imported.prepared, bookmarkWarning: imported.bookmarkWarning)
            } catch {
                outcome = .failure(Self.displayMessage(for: error))
            }

            isSyncing = false
            switch outcome {
            case .loaded(let prepared, let bookmarkWarning):
                let now = Date()
                var settings = syncSettings
                settings.providerType = .iCloudDrive
                settings.lastSuccessfulSync = now
                settings.lastImportDate = now
                settings.lastError = bookmarkWarning
                syncSettings = settings
                syncSettingsStore.save(settings)
                var status = successMessage
                if let bookmarkWarning {
                    status += "\n\(bookmarkWarning)"
                }
                syncStatusMessage = status
                setupRequired = false
                setupMessage = nil
                apply(prepared: prepared)
            case .failure(let message):
                var settings = syncSettings
                settings.lastError = message
                syncSettings = settings
                syncSettingsStore.save(settings)
                let displayMessage = message == SnapshotAccessError.iCloudSourceUnavailable.localizedDescription
                    ? message
                    : "iCloud-Aktualisierung fehlgeschlagen. \(message)"
                syncStatusMessage = displayMessage
                if message == SnapshotAccessError.iCloudSourceUnavailable.localizedDescription {
                    accessStore.clearICloudSourceBookmark()
                }
                if document != nil {
                    noticeMessage = displayMessage
                    setupRequired = false
                    setupMessage = nil
                    loadState = .ready
                } else {
                    setupRequired = true
                    setupMessage = displayMessage
                    present(errorMessage: displayMessage, keepSetupState: true)
                }
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

    nonisolated private static func formatSyncDate(_ date: Date) -> String {
        date.formatted(date: .abbreviated, time: .shortened)
    }

    private func apply(prepared: PreparedSnapshot) {
        document = prepared.document
        groupedCategoryCache = prepared.categories
        taskCountByCategoryID = prepared.taskCountByCategoryID
        selectedFolderURL = prepared.document.sourceURL

        if selectedCategoryID != Self.allTasksCategoryID,
           selectedCategoryID != Self.mobilePendingCategoryID,
           !categories.contains(where: { $0.id == selectedCategoryID }) {
            selectedCategoryID = Self.allTasksCategoryID
        }

        if selectedCategoryID == Self.allTasksCategoryID {
            selectedTaskID = tasks.first?.id
        } else if selectedCategoryID == Self.mobilePendingCategoryID {
            selectedTaskID = filteredTasks.first?.id
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

    nonisolated private static func snapshotTask(from entry: MobileInboxPendingEntry, sourceIndex: Int) -> SnapshotTask {
        SnapshotTask(
            id: "mobile-inbox-\(entry.id)",
            title: entry.displayTitle,
            customerName: entry.customerName,
            customerEmail: entry.email,
            customerPhone: entry.phone,
            categoryIds: [Self.mobilePendingCategoryID],
            categoryNames: ["Wartet auf Freigabe", entry.category].filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty },
            dueDate: nil,
            reminderDate: nil,
            createdAt: entry.createdAt,
            updatedAt: nil,
            materialOrderedAt: nil,
            status: "wartet auf Freigabe",
            notes: ([
                entry.notes,
                entry.attachmentSummary,
                entry.attachmentIssueSummary
            ] as [String?]).compactMap { value in
                guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
                    return nil
                }
                return value
            }.joined(separator: "\n\n"),
            shortText: entry.attachmentIssueSummary ?? entry.attachmentSummary,
            attachmentRefs: [],
            sourceIndex: sourceIndex
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
            guard !trimmedName.isEmpty,
                  !isSystemSettingsCategory(id: category.id, name: trimmedName) else {
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

    nonisolated private static func isSystemSettingsCategory(id: String, name: String) -> Bool {
        id.caseInsensitiveCompare("__settings") == .orderedSame ||
        name.caseInsensitiveCompare("Einstellungen") == .orderedSame
    }

    nonisolated private static func fetchLocalNetworkDesktopStatus(
        address: String,
        port: Int
    ) async throws -> LocalNetworkDesktopStatusResponse {
        guard !address.contains("/"),
              !address.contains(":"),
              !address.contains("?") else {
            throw SnapshotSyncError.invalidHTTPResponse
        }

        var components = URLComponents()
        components.scheme = "http"
        components.host = address
        components.port = port
        components.path = "/local-sync/status"

        guard let url = components.url else {
            throw SnapshotSyncError.invalidHTTPResponse
        }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.timeoutInterval = 5

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw SnapshotSyncError.invalidHTTPResponse
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            throw SnapshotSyncError.httpStatus(httpResponse.statusCode)
        }
        return try JSONDecoder().decode(LocalNetworkDesktopStatusResponse.self, from: data)
    }

    nonisolated private static func rememberIpadAtDesktop(
        address: String,
        port: Int,
        deviceId: String,
        deviceName: String,
        appVersion: String?
    ) async throws -> LocalNetworkDesktopRememberResponse {
        guard !address.contains("/"),
              !address.contains(":"),
              !address.contains("?") else {
            throw SnapshotSyncError.invalidHTTPResponse
        }

        var components = URLComponents()
        components.scheme = "http"
        components.host = address
        components.port = port
        components.path = "/local-sync/devices/remember"

        guard let url = components.url else {
            throw SnapshotSyncError.invalidHTTPResponse
        }

        let payload = LocalNetworkDeviceRememberPayload(
            deviceId: deviceId,
            deviceName: deviceName,
            platform: "iPadOS",
            appVersion: appVersion,
            lastSeenUtc: Date()
        )
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.timeoutInterval = 5
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        request.httpBody = try encoder.encode(payload)

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw SnapshotSyncError.invalidHTTPResponse
        }
        guard (200...299).contains(httpResponse.statusCode) else {
            throw SnapshotSyncError.httpStatus(httpResponse.statusCode)
        }
        return try JSONDecoder().decode(LocalNetworkDesktopRememberResponse.self, from: data)
    }

    private func ensureLocalNetworkIpadIdentity() -> String {
        if let existing = syncSettings.localNetworkDesktop.ipadDeviceId?.trimmingCharacters(in: .whitespacesAndNewlines),
           !existing.isEmpty {
            return existing
        }

        let deviceId = "ipad-\(UUID().uuidString.lowercased())"
        var settings = syncSettings
        settings.localNetworkDesktop.ipadDeviceId = deviceId
        settings.localNetworkDesktop.ipadDeviceName = Self.currentIpadDeviceName()
        settings.localNetworkDesktop.ipadPlatform = "iPadOS"
        syncSettings = settings
        syncSettingsStore.save(settings)
        return deviceId
    }

    private static func currentIpadDeviceName() -> String {
        let name = UIDevice.current.name.trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? "iPad" : name
    }

    private static func currentAppVersion() -> String? {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String
    }

    private func runLocalNetworkDesktopAutoCheck(address: String) async {
        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        localNetworkDesktopAutoCheckMessage = "Prüfung läuft …"

        defer {
            isLocalNetworkDesktopServiceCheckRunning = false
        }

        do {
            let response = try await Self.fetchLocalNetworkDesktopStatus(
                address: address,
                port: localNetworkDesktopTestPort
            )
            if response.isExpectedLocalNetworkTestStatus {
                saveSuccessfulLocalNetworkDesktopCheck(address: address)
                syncStatusMessage = isLocalNetworkDesktopRemembered
                    ? "Lokaler Desktop vorgemerkt"
                    : "Desktop-Testdienst erreichbar"
                localNetworkDesktopAutoCheckMessage = isLocalNetworkDesktopRemembered
                    ? "Vorgemerkter Desktop erreichbar"
                    : "Desktop-Testdienst erreichbar"
            } else {
                if isLocalNetworkDesktopRemembered {
                    syncStatusMessage = "Lokaler Desktop vorgemerkt"
                    localNetworkDesktopAutoCheckMessage = "Automatische Prüfung: unerwartete Antwort"
                } else {
                    syncStatusMessage = "Desktop-Testdienst nicht erreichbar: unerwartete Antwort"
                }
            }
        } catch {
            if isLocalNetworkDesktopRemembered {
                syncStatusMessage = "Lokaler Desktop vorgemerkt"
                localNetworkDesktopAutoCheckMessage = "Automatische Suche hat aktuell keinen Desktop gefunden."
            } else {
                syncStatusMessage = "Desktop-Testdienst nicht erreichbar: \(Self.shortLocalNetworkDesktopError(error))"
            }
            adoptRememberedLocalNetworkDesktopIfFound(in: discoveredLocalNetworkDesktops)
        }
    }

    private func saveSuccessfulLocalNetworkDesktopCheck(address: String) {
        var settings = syncSettings
        let previousStatus = settings.localNetworkDesktop.localDesktopStatus
        settings.localNetworkDesktop.desktopAddress = address
        settings.localNetworkDesktop.desktopPort = localNetworkDesktopTestPort
        settings.localNetworkDesktop.lastSuccessfulCheckAt = Date()
        settings.localNetworkDesktop.localDesktopStatus = Self.isRememberedLocalNetworkDesktop(settings.localNetworkDesktop)
            ? previousStatus
            : nil
        syncSettings = settings
        syncSettingsStore.save(settings)
    }

    private func saveDiscoveredLocalNetworkDesktop(
        _ desktop: LocalNetworkDiscoveredDesktop,
        status: String
    ) {
        var settings = syncSettings
        settings.localNetworkDesktop.desktopAddress = desktop.address
        settings.localNetworkDesktop.desktopPort = desktop.port
        settings.localNetworkDesktop.lastSuccessfulCheckAt = Date()
        settings.localNetworkDesktop.localDesktopStatus = status
        settings.localNetworkDesktop.desktopDeviceId = desktop.deviceId
        settings.localNetworkDesktop.desktopName = desktop.name
        syncSettings = settings
        syncSettingsStore.save(settings)
    }

    private func adoptRememberedLocalNetworkDesktopIfFound(in desktops: [LocalNetworkDiscoveredDesktop]) {
        guard !desktops.isEmpty else { return }
        let remembered = syncSettings.localNetworkDesktop
        guard Self.isRememberedLocalNetworkDesktop(remembered) else {
            return
        }

        let rememberedDeviceId = remembered.desktopDeviceId?.trimmingCharacters(in: .whitespacesAndNewlines)
        let rememberedName = remembered.desktopName?.trimmingCharacters(in: .whitespacesAndNewlines)
        let rememberedAddress = remembered.desktopAddress?.trimmingCharacters(in: .whitespacesAndNewlines)
        let match = desktops.first { desktop in
            if let rememberedDeviceId, !rememberedDeviceId.isEmpty {
                return desktop.deviceId == rememberedDeviceId
            }
            if let rememberedName, !rememberedName.isEmpty {
                return desktop.name == rememberedName
            }
            if let rememberedAddress, !rememberedAddress.isEmpty {
                return desktop.address == rememberedAddress && desktop.port == (remembered.desktopPort ?? localNetworkDesktopTestPort)
            }
            return false
        }

        guard let match else { return }
        guard remembered.desktopAddress != match.address || remembered.desktopPort != match.port else { return }
        let status = remembered.localDesktopStatus == "Lokaler Desktop vorgemerkt" ||
            remembered.localDesktopStatus == "lokaler Desktop vorgemerkt"
            ? "Lokaler Desktop vorgemerkt"
            : "Desktop im lokalen Netzwerk gefunden"
        saveDiscoveredLocalNetworkDesktop(match, status: status)
        syncStatusMessage = status
    }

    nonisolated private static func isRememberedLocalNetworkDesktop(_ desktop: LocalNetworkDesktopPairing) -> Bool {
        let status = desktop.localDesktopStatus?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return desktop.lastSuccessfulCheckAt != nil ||
            status == "lokaler Desktop vorgemerkt" ||
            status == "Lokaler Desktop vorgemerkt" ||
            status == "Desktop im lokalen Netzwerk gefunden"
    }

    nonisolated private static func shortLocalNetworkDesktopError(_ error: Error) -> String {
        if let urlError = error as? URLError {
            switch urlError.code {
            case .cannotFindHost:
                return "Host nicht gefunden"
            case .cannotConnectToHost, .networkConnectionLost, .notConnectedToInternet:
                return "keine Verbindung"
            case .timedOut:
                return "Zeitüberschreitung"
            case .unsupportedURL, .badURL:
                return "ungültige Adresse"
            default:
                return urlError.localizedDescription
            }
        }

        if case SnapshotSyncError.httpStatus(let statusCode) = error {
            return "HTTP \(statusCode)"
        }

        if error is DecodingError {
            return "Antwort nicht lesbar"
        }

        return error.localizedDescription
    }
}

private enum LocalSnapshotLoadOutcome: Sendable {
    case loaded(PreparedSnapshot)
    case failure(String)
}

private enum GoogleSnapshotLoadOutcome: Sendable {
    case online(PreparedSnapshot)
    case fallback(PreparedSnapshot, onlineError: String)
    case failure(String)
}

private enum ICloudSnapshotLoadOutcome: Sendable {
    case loaded(PreparedSnapshot, bookmarkWarning: String?)
    case failure(String)
}

private struct PreparedSnapshot: Sendable {
    let document: SnapshotDocument
    let categories: [SnapshotCategoryGroup]
    let taskCountByCategoryID: [String: Int]
}

private struct LocalNetworkDesktopStatusResponse: Decodable, Sendable {
    let app: String
    let status: String
    let mode: String

    var isExpectedLocalNetworkTestStatus: Bool {
        app == "BueroCockpit" &&
        status == "ok" &&
        (mode == "local-network-test" || mode == "pairing-test")
    }
}

private struct LocalNetworkDeviceRememberPayload: Encodable, Sendable {
    let deviceId: String
    let deviceName: String
    let platform: String
    let appVersion: String?
    let lastSeenUtc: Date
}

private struct LocalNetworkDesktopRememberResponse: Decodable, Sendable {
    let app: String
    let status: String
    let mode: String
    let message: String?

    var isExpectedRememberResponse: Bool {
        app == "BueroCockpit" &&
        status == "ok" &&
        mode == "local-network-test"
    }
}
