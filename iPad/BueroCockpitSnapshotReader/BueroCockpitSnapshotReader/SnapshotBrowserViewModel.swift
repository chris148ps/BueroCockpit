import Foundation
import UIKit

enum LocalNetworkDesktopConnectionState: Equatable, Sendable {
    case checking
    case connected
    case disconnected

    var title: String {
        switch self {
        case .checking:
            return "Desktop wird geprüft"
        case .connected:
            return "Desktop verbunden"
        case .disconnected:
            return "Desktop nicht verbunden"
        }
    }
}

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
    @Published private(set) var localNetworkDesktopConnectionState: LocalNetworkDesktopConnectionState = .disconnected
    @Published private(set) var discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop] = []
    @Published private(set) var syncSettings: SnapshotSyncSettings
    @Published private(set) var isSyncing = false
    @Published private(set) var loadingTitle = "Snapshot wird geladen …"
    @Published private(set) var mobileInboxEntries: [MobileInboxPendingEntry] = []
    @Published private(set) var pendingMobileChangeCount = 0
    @Published private(set) var openMobilePhotoDraftCount = 0
    @Published private(set) var manualSyncPhase: LocalNetworkManualSyncPhase?
    @Published private(set) var manualSyncProgress = 0.0
    @Published private(set) var manualSyncSummary: LocalNetworkManualSyncSummary?
    @Published private(set) var manualSyncErrorMessage: String?
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
    private let mobileChangeQueueStore: MobileChangeQueueStore
    private let mobilePhotoDraftStore: MobilePhotoDraftStore
    private let localNetworkManualSyncClient: LocalNetworkManualSyncClient
    private var didAttemptStartupLoad = false
    private let localNetworkDesktopTestPort = 53941
    private let localNetworkDesktopAutoCheckIntervalNanoseconds: UInt64 = 30_000_000_000
    private var localNetworkDesktopAutoCheckTask: Task<Void, Never>?
    private var localNetworkDesktopAutoCheckAddress: String?
    private var localNetworkDesktopAutoCheckPort: Int?
    private var isLocalNetworkDesktopServiceCheckRunning = false
    private var isLocalNetworkDesktopMainMonitoringActive = false
    private var isLocalNetworkDesktopSettingsMonitoringActive = false
    private var isLocalNetworkDesktopDiscoveryActive = false
    private let localNetworkDesktopDiscovery = LocalNetworkDesktopDiscovery()

    init(
        reader: SnapshotReader = SnapshotReader(),
        accessStore: SnapshotAccessStore = SnapshotAccessStore(),
        syncSettingsStore: SnapshotSyncSettingsStore = SnapshotSyncSettingsStore(),
        mobileInboxReader: MobileInboxReader = MobileInboxReader(),
        mobileChangeQueueStore: MobileChangeQueueStore = MobileChangeQueueStore(),
        mobilePhotoDraftStore: MobilePhotoDraftStore = MobilePhotoDraftStore(),
        localNetworkManualSyncClient: LocalNetworkManualSyncClient = LocalNetworkManualSyncClient()
    ) {
        self.reader = reader
        self.accessStore = accessStore
        self.syncSettingsStore = syncSettingsStore
        self.mobileInboxReader = mobileInboxReader
        self.mobileChangeQueueStore = mobileChangeQueueStore
        self.mobilePhotoDraftStore = mobilePhotoDraftStore
        self.localNetworkManualSyncClient = localNetworkManualSyncClient
        syncSettings = syncSettingsStore.load()
        setupRequired = false
        loadState = .idle
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
                name: "Mobile Eingänge",
                categoryIDs: [Self.mobilePendingCategoryID],
                order: Int.min
            )
        ] + groupedCategoryCache
    }

    var mobileInspectionCategoryNames: [String] {
        groupedCategoryCache
            .filter { Self.isSelectableMobileInspectionCategory($0) }
            .map(\.name)
            .filter { !Self.isLegacyMobileApprovalCategory($0) }
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
            return "Mobile Eingänge"
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

    var localNetworkDesktopName: String? {
        syncSettings.localNetworkDesktop.desktopName
    }

    var otherDiscoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop] {
        discoveredLocalNetworkDesktops.filter { desktop in
            !Self.matchesLocalNetworkDesktop(desktop, remembered: syncSettings.localNetworkDesktop, fallbackPort: localNetworkDesktopTestPort)
        }
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

    var canStartManualLocalNetworkSync: Bool {
        !isSyncing &&
            !localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
            syncSettings.localNetworkDesktop.ipadDeviceId?.isEmpty == false &&
            syncSettings.localNetworkDesktop.sharedSecret?.isEmpty == false
    }

    var manualLocalNetworkSyncTargetText: String {
        let name = localNetworkDesktopName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return name.isEmpty ? "BüroCockpit-Desktop" : name
    }

    var lastSuccessfulManualSyncText: String? {
        guard let date = syncSettings.localNetworkDesktop.lastSuccessfulSyncAt else { return nil }
        return Self.formatSyncDate(date)
    }

    func startManualLocalNetworkSync() {
        guard !isSyncing else { return }
        let desktop = syncSettings.localNetworkDesktop
        guard let address = desktop.desktopAddress?.trimmingCharacters(in: .whitespacesAndNewlines), !address.isEmpty else {
            manualSyncErrorMessage = LocalNetworkManualSyncError.desktopMissing.localizedDescription
            return
        }
        guard let deviceId = desktop.ipadDeviceId, !deviceId.isEmpty,
              let trustKey = desktop.sharedSecret, !trustKey.isEmpty else {
            manualSyncErrorMessage = LocalNetworkManualSyncError.pairingMissing.localizedDescription
            return
        }

        isSyncing = true
        manualSyncSummary = nil
        manualSyncErrorMessage = nil
        manualSyncPhase = .searchingDesktop
        manualSyncProgress = LocalNetworkManualSyncPhase.searchingDesktop.progress
        let client = localNetworkManualSyncClient
        let port = desktop.desktopPort ?? localNetworkDesktopTestPort

        Task {
            defer { isSyncing = false }
            do {
                let summary = try await client.synchronize(
                    address: address,
                    port: port,
                    deviceId: deviceId,
                    trustKey: trustKey
                ) { [weak self] phase in
                    await MainActor.run {
                        self?.manualSyncPhase = phase
                        self?.manualSyncProgress = phase.progress
                    }
                }
                manualSyncSummary = summary
                manualSyncPhase = .completed
                manualSyncProgress = 1
                if summary.failedObjects == 0 {
                    var settings = syncSettings
                    settings.localNetworkDesktop.lastSuccessfulSyncAt = Date()
                    settings.localNetworkDesktop.lastTransferredObjectCount = summary.transferredObjects
                    settings.localNetworkDesktop.lastTransferredPhotoCount = summary.transferredPhotos
                    settings.localNetworkDesktop.lastSkippedObjectCount = summary.skippedObjects
                    settings.localNetworkDesktop.lastFailedObjectCount = 0
                    syncSettings = settings
                    syncSettingsStore.save(settings)
                    syncStatusMessage = summary.transferredObjects == 0 && summary.skippedObjects == 0
                        ? "Synchronisation abgeschlossen: keine neuen mobilen Eingänge."
                        : "Synchronisation abgeschlossen"
                } else {
                    manualSyncErrorMessage = summary.messages.last ?? "Mindestens ein mobiler Eingang konnte nicht übertragen werden."
                    syncStatusMessage = "Synchronisation mit Fehlern beendet"
                }
            } catch is CancellationError {
                manualSyncErrorMessage = "Synchronisation abgebrochen. Lokale Daten bleiben erhalten."
                syncStatusMessage = "Synchronisation abgebrochen"
            } catch {
                manualSyncErrorMessage = error.localizedDescription
                syncStatusMessage = "Synchronisation fehlgeschlagen"
            }
        }
    }

    private var isLocalNetworkDesktopRemembered: Bool {
        Self.isRememberedLocalNetworkDesktop(syncSettings.localNetworkDesktop)
    }

    private var isLocalNetworkDesktopMonitoringActive: Bool {
        isLocalNetworkDesktopMainMonitoringActive || isLocalNetworkDesktopSettingsMonitoringActive
    }

    var loadingDescription: String {
        if loadingTitle == "Google-Drive-Aktualisierung läuft …" {
            return "Bitte warten. Die App lädt die Datei einmalig und prüft sie vor der lokalen Übernahme."
        }
        if loadingTitle == "iCloud-Datei wird gelesen …" {
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
        loadMobileChangeQueue()
        loadMobilePhotoDrafts()
        updateLocalNetworkDesktopConnectionStateForStoredSettings()
        setupRequired = false
        setupMessage = nil
        if syncStatusMessage == nil {
            syncStatusMessage = "Desktop-Sync-Dienst in BüroCockpit starten. Desktop wird automatisch gesucht. Manuelle IP in den Einstellungen möglich."
        }
        loadState = document == nil ? .idle : loadState
        SnapshotPerformanceLog.event(
            accessStore.hasLocalSnapshot
                ? "Startup snapshot autoload skipped: local snapshot remains available for manual reload"
                : "Startup snapshot autoload skipped: no imported live file"
        )
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
        guard accessStore.hasLocalSnapshot else {
            setupRequired = false
            setupMessage = nil
            syncStatusMessage = "Desktop-Sync-Dienst in BüroCockpit starten. Desktop wird automatisch gesucht. Manuelle IP in den Einstellungen möglich."
            loadState = .idle
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
            localNetworkDesktopConnectionState = .disconnected
            return
        }

        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        loadingTitle = "Prüfung läuft …"
        syncStatusMessage = "Prüfung läuft …"
        localNetworkDesktopConnectionState = .checking

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
                    saveSuccessfulLocalNetworkDesktopCheck(address: normalizedAddress, port: localNetworkDesktopTestPort)
                    await checkLocalNetworkDesktopChangeStatus(address: normalizedAddress, port: localNetworkDesktopTestPort)
                    localNetworkDesktopConnectionState = .connected
                    syncStatusMessage = isLocalNetworkDesktopRemembered
                        ? "Lokaler Desktop vorgemerkt"
                        : "Desktop-Sync-Dienst erreichbar"
                    print("Desktop-Statusprüfung erfolgreich")
                } else {
                    localNetworkDesktopConnectionState = .disconnected
                    syncStatusMessage = "Desktop-Sync-Dienst nicht erreichbar: unerwartete Antwort"
                    print("Desktop-Statusprüfung fehlgeschlagen: unerwartete Antwort")
                }
            } catch {
                localNetworkDesktopConnectionState = .disconnected
                syncStatusMessage = "Desktop-Sync-Dienst nicht erreichbar: \(Self.shortLocalNetworkDesktopError(error))"
                print("Desktop-Statusprüfung fehlgeschlagen: \(Self.shortLocalNetworkDesktopError(error))")
            }
        }
    }

    func startMainLocalNetworkDesktopMonitoring() {
        isLocalNetworkDesktopMainMonitoringActive = true
        ensureLocalNetworkMonitoringStarted(reason: "Hauptansicht")
    }

    func stopMainLocalNetworkDesktopMonitoring() {
        isLocalNetworkDesktopMainMonitoringActive = false
        stopLocalNetworkDesktopAutoCheck()
        stopLocalNetworkDesktopDiscovery()
    }

    func startSettingsLocalNetworkDesktopMonitoring(address: String) {
        isLocalNetworkDesktopSettingsMonitoringActive = true
        ensureLocalNetworkMonitoringStarted(reason: "Einstellungen", addressOverride: address)
    }

    func stopSettingsLocalNetworkDesktopMonitoring() {
        isLocalNetworkDesktopSettingsMonitoringActive = false
        stopLocalNetworkDesktopAutoCheck()
        stopLocalNetworkDesktopDiscovery()
    }

    private func ensureLocalNetworkMonitoringStarted(reason: String, addressOverride: String? = nil) {
        print("\(reason) startet Desktop-Statusmonitoring")
        loadStoredLocalNetworkDesktopSettings()

        let storedAddress = localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines)
        if storedAddress.isEmpty {
            print("Vorgemerkter Desktop geladen: keiner")
        } else {
            let name = localNetworkDesktopName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            let displayName = name.isEmpty ? "ohne Namen" : name
            print("Vorgemerkter Desktop geladen: \(displayName) \(storedAddress):\(localNetworkDesktopPort)")
        }

        let requestedAddress = addressOverride?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let address = requestedAddress.isEmpty ? storedAddress : requestedAddress
        startLocalNetworkDesktopAutoCheck(address: address)
        startLocalNetworkDesktopDiscovery()
    }

    func startLocalNetworkDesktopAutoCheck(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !normalizedAddress.isEmpty else {
            localNetworkDesktopAutoCheckTask?.cancel()
            localNetworkDesktopAutoCheckTask = nil
            localNetworkDesktopAutoCheckAddress = nil
            localNetworkDesktopAutoCheckPort = nil
            localNetworkDesktopAutoCheckMessage = "Desktop-Adresse fehlt"
            localNetworkDesktopConnectionState = .disconnected
            return
        }

        let port = localNetworkDesktopPort
        guard shouldAutoCheckLocalNetworkDesktop(address: normalizedAddress, port: port) else {
            localNetworkDesktopAutoCheckMessage = "Anderer Desktop gefunden. Wechsel nur über Benutzeraktion."
            return
        }

        if localNetworkDesktopAutoCheckTask != nil,
           localNetworkDesktopAutoCheckAddress == normalizedAddress,
           localNetworkDesktopAutoCheckPort == port {
            return
        }

        localNetworkDesktopAutoCheckTask?.cancel()
        localNetworkDesktopAutoCheckAddress = normalizedAddress
        localNetworkDesktopAutoCheckPort = port
        localNetworkDesktopAutoCheckMessage = "Prüfung läuft …"
        localNetworkDesktopConnectionState = .checking
        print("connectionState gesetzt: \(LocalNetworkDesktopConnectionState.checking.title)")
        let checkInterval = localNetworkDesktopAutoCheckIntervalNanoseconds
        localNetworkDesktopAutoCheckTask = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                guard let self else { return }
                await self.runLocalNetworkDesktopAutoCheck(address: normalizedAddress, port: port)
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
        guard !isLocalNetworkDesktopMonitoringActive else {
            return
        }
        localNetworkDesktopAutoCheckTask?.cancel()
        localNetworkDesktopAutoCheckTask = nil
        localNetworkDesktopAutoCheckAddress = nil
        localNetworkDesktopAutoCheckPort = nil
        localNetworkDesktopAutoCheckMessage = nil
    }

    func startLocalNetworkDesktopDiscovery() {
        guard !isLocalNetworkDesktopDiscoveryActive else { return }
        isLocalNetworkDesktopDiscoveryActive = true
        localNetworkDesktopDiscovery.start { [weak self] desktops in
            guard let self else { return }
            discoveredLocalNetworkDesktops = desktops
            if desktops.isEmpty, isLocalNetworkDesktopRemembered {
                localNetworkDesktopAutoCheckMessage = "Automatische Suche: kein weiterer Desktop gefunden."
                syncStatusMessage = "Lokaler Desktop vorgemerkt"
            }
            adoptRememberedLocalNetworkDesktopIfFound(in: desktops)
            autoCheckDiscoveredDesktopIfNeeded(desktops)
        }
    }

    func stopLocalNetworkDesktopDiscovery() {
        guard !isLocalNetworkDesktopMonitoringActive else {
            return
        }
        isLocalNetworkDesktopDiscoveryActive = false
        localNetworkDesktopDiscovery.stop()
        discoveredLocalNetworkDesktops = []
    }

    func useDiscoveredLocalNetworkDesktop(_ desktop: LocalNetworkDiscoveredDesktop) {
        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        loadingTitle = "Prüfung läuft …"
        syncStatusMessage = "Anderer BüroCockpit-Desktop wird geprüft …"
        localNetworkDesktopConnectionState = .checking

        Task {
            defer {
                isLocalNetworkDesktopServiceCheckRunning = false
            }

            do {
                let response = try await Self.fetchLocalNetworkDesktopStatus(
                    address: desktop.address,
                    port: desktop.port
                )
                guard response.isExpectedLocalNetworkTestStatus else {
                    localNetworkDesktopConnectionState = .disconnected
                    syncStatusMessage = "Anderer Desktop nicht übernommen: unerwartete Antwort"
                    return
                }

                rememberLocalNetworkDesktop(desktop)
                await checkLocalNetworkDesktopChangeStatus(address: desktop.address, port: desktop.port)
                localNetworkDesktopConnectionState = .connected
                syncStatusMessage = "Desktop vorgemerkt"
                startLocalNetworkDesktopAutoCheck(address: desktop.address)
                await rememberCurrentIpadAtDesktop(address: desktop.address, port: desktop.port)
            } catch {
                localNetworkDesktopConnectionState = .disconnected
                syncStatusMessage = "Anderer Desktop nicht übernommen: \(Self.shortLocalNetworkDesktopError(error))"
            }
        }
    }

    func markLocalNetworkDesktopAsPreferred(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !normalizedAddress.isEmpty else {
            syncStatusMessage = "Bitte Desktop-Adresse oder IP eintragen."
            return
        }

        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        loadingTitle = "Prüfung läuft …"
        syncStatusMessage = "Desktop wird geprüft …"
        localNetworkDesktopConnectionState = .checking

        let deviceId = ensureLocalNetworkIpadIdentity()
        let deviceName = Self.currentIpadDeviceName()
        let trustKey = ensureLocalNetworkTrustKey()

        Task {
            defer {
                isLocalNetworkDesktopServiceCheckRunning = false
            }

            do {
                let statusResponse = try await Self.fetchLocalNetworkDesktopStatus(
                    address: normalizedAddress,
                    port: localNetworkDesktopTestPort
                )
                guard statusResponse.isExpectedLocalNetworkTestStatus else {
                    localNetworkDesktopConnectionState = .disconnected
                    syncStatusMessage = "Desktop nicht übernommen: unerwartete Antwort"
                    return
                }

                var settings = syncSettings
                settings.localNetworkDesktop.desktopAddress = normalizedAddress
                settings.localNetworkDesktop.desktopPort = localNetworkDesktopTestPort
                settings.localNetworkDesktop.localDesktopStatus = "Lokaler Desktop vorgemerkt"
                settings.localNetworkDesktop.lastSuccessfulCheckAt = Date()
                settings.localNetworkDesktop.ipadDeviceId = deviceId
                settings.localNetworkDesktop.ipadDeviceName = deviceName
                settings.localNetworkDesktop.ipadPlatform = "iPadOS"
                syncSettings = settings
                syncSettingsStore.save(settings)
                await checkLocalNetworkDesktopChangeStatus(address: normalizedAddress, port: localNetworkDesktopTestPort)
                localNetworkDesktopConnectionState = .connected
                syncStatusMessage = "Lokaler Desktop vorgemerkt"
                startLocalNetworkDesktopAutoCheck(address: normalizedAddress)

                let response = try await Self.rememberIpadAtDesktop(
                    address: normalizedAddress,
                    port: localNetworkDesktopTestPort,
                    deviceId: deviceId,
                    deviceName: deviceName,
                    appVersion: Self.currentAppVersion(),
                    sharedSecret: trustKey
                )
                syncStatusMessage = response.isExpectedRememberResponse
                    ? "Desktop vorgemerkt, iPad am Desktop registriert."
                    : "Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich."
            } catch {
                localNetworkDesktopConnectionState = .disconnected
                syncStatusMessage = "Desktop nicht übernommen: \(Self.shortLocalNetworkDesktopError(error))"
            }
        }
    }

    private func rememberLocalNetworkDesktop(_ desktop: LocalNetworkDiscoveredDesktop) {
        let deviceId = ensureLocalNetworkIpadIdentity()
        let deviceName = Self.currentIpadDeviceName()
        var settings = syncSettings
        settings.localNetworkDesktop.desktopAddress = desktop.address
        settings.localNetworkDesktop.desktopPort = desktop.port
        settings.localNetworkDesktop.desktopDeviceId = desktop.deviceId
        settings.localNetworkDesktop.desktopName = desktop.name
        settings.localNetworkDesktop.desktopPlatform = nil
        settings.localNetworkDesktop.lastSuccessfulCheckAt = Date()
        settings.localNetworkDesktop.localDesktopStatus = "Lokaler Desktop vorgemerkt"
        settings.localNetworkDesktop.ipadDeviceId = deviceId
        settings.localNetworkDesktop.ipadDeviceName = deviceName
        settings.localNetworkDesktop.ipadPlatform = "iPadOS"
        syncSettings = settings
        syncSettingsStore.save(settings)
    }

    private func rememberCurrentIpadAtDesktop(address: String, port: Int) async {
        let deviceId = ensureLocalNetworkIpadIdentity()
        let deviceName = Self.currentIpadDeviceName()
        let trustKey = ensureLocalNetworkTrustKey()
        do {
            let response = try await Self.rememberIpadAtDesktop(
                address: address,
                port: port,
                deviceId: deviceId,
                deviceName: deviceName,
                appVersion: Self.currentAppVersion(),
                sharedSecret: trustKey
            )
            syncStatusMessage = response.isExpectedRememberResponse
                ? "Desktop vorgemerkt, iPad am Desktop registriert."
                : "Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich."
        } catch {
            syncStatusMessage = "Desktop lokal vorgemerkt. Registrierung am Desktop noch nicht möglich."
        }
    }

    func resetLocalNetworkDesktopPreferenceIfAddressChanged(address: String) {
        let normalizedAddress = address.trimmingCharacters(in: .whitespacesAndNewlines)
        guard normalizedAddress != (syncSettings.localNetworkDesktop.desktopAddress ?? "") else {
            return
        }
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

    func loadMobileChangeQueue() {
        let store = mobileChangeQueueStore
        Task {
            let queue = await Task.detached(priority: .utility) {
                store.load()
            }.value

            pendingMobileChangeCount = queue.pendingChangeCount
        }
    }

    func loadMobilePhotoDrafts() {
        let store = mobilePhotoDraftStore
        Task {
            let collection = await Task.detached(priority: .utility) {
                store.load()
            }.value

            openMobilePhotoDraftCount = collection.openDraftCount
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
        localNetworkDesktopConnectionState = .disconnected
    }

    func dismissSetup() {
        if document != nil {
            setupRequired = false
        }
    }

    private func loadLocalSnapshot(isLaunch: Bool) {
        SnapshotPerformanceLog.event(isLaunch ? "Start local load started" : "Manual local reload started")
        searchText = ""
        loadingTitle = isLaunch ? "Lokale Lesedaten werden geladen …" : "Lokale Lesedaten werden neu geladen …"
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
                let failureMessage = "Lokale Lesedaten konnten nicht geladen werden. \(message)"
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
        loadingTitle = "Google-Drive-Aktualisierung läuft …"
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
                SnapshotPerformanceLog.event(isLaunch ? "Launch Google Drive load finished" : "Manual Google Drive load finished")
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
        loadingTitle = "iCloud-Datei wird gelesen …"
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
            categoryNames: visibleMobileCategoryNames(from: entry),
            dueDate: nil,
            reminderDate: nil,
            createdAt: entry.createdAt,
            updatedAt: nil,
            materialOrderedAt: nil,
            status: displayMobileInboxStatus(entry.status),
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

    nonisolated private static func visibleMobileCategoryNames(from entry: MobileInboxPendingEntry) -> [String] {
        let category = entry.category.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !category.isEmpty, !isLegacyMobileApprovalCategory(category) else {
            return []
        }

        return [category]
    }

    nonisolated private static func displayMobileInboxStatus(_ status: String) -> String {
        switch status.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "", "new", "pending", "approved", "released", "freigegeben":
            return "Neu"
        case "imported", "processed", "done", "verarbeitet", "uebernommen", "übernommen":
            return "Übernommen"
        case "error", "failed", "fehlerhaft":
            return "Fehler"
        default:
            return status
        }
    }

    nonisolated private static func isLegacyMobileApprovalCategory(_ value: String) -> Bool {
        let legacyName = ["Wartet", "auf", "Freigabe"].joined(separator: " ")
        return value.trimmingCharacters(in: .whitespacesAndNewlines)
            .localizedCaseInsensitiveCompare(legacyName) == .orderedSame
    }

    nonisolated private static func isSelectableMobileInspectionCategory(_ category: SnapshotCategoryGroup) -> Bool {
        let trimmedName = category.name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedName.isEmpty,
              category.categoryIDs.count == 1,
              !isLegacyMobileApprovalCategory(trimmedName),
              trimmedName.caseInsensitiveCompare("Archiv") != .orderedSame,
              trimmedName.caseInsensitiveCompare("Mobile Eingänge") != .orderedSame else {
            return false
        }

        if trimmedName.contains(" / ") {
            return true
        }

        return isRootEndCategory(trimmedName)
    }

    nonisolated private static func isRootEndCategory(_ name: String) -> Bool {
        let rootEndCategories = [
            "Schreibtisch",
            "Offene Aufgaben",
            "Wartet auf Kunde"
        ]
        return rootEndCategories.contains { candidate in
            name.caseInsensitiveCompare(candidate) == .orderedSame
        }
    }

    nonisolated private static func groupedCategories(from categories: [SnapshotCategory]) -> [SnapshotCategoryGroup] {
        let visibleCategories = categories.enumerated().compactMap { index, category -> (SnapshotCategory, String, Int)? in
            let trimmedName = category.name.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmedName.isEmpty,
                  !isSystemSettingsCategory(id: category.id, name: trimmedName),
                  !isLegacyMobileApprovalCategory(trimmedName),
                  trimmedName.caseInsensitiveCompare("Archiv") != .orderedSame else {
                return nil
            }

            return (category, trimmedName, category.order ?? index)
        }

        let byID = Dictionary(uniqueKeysWithValues: visibleCategories.map { ($0.0.id, $0) })
        let byParent = Dictionary(grouping: visibleCategories) { item in
            item.0.parentId ?? ""
        }
        var result: [SnapshotCategoryGroup] = []

        func descendantIDs(for categoryID: String) -> [String] {
            var ids: [String] = []
            for child in byParent[categoryID] ?? [] {
                ids.append(child.0.id)
                ids.append(contentsOf: descendantIDs(for: child.0.id))
            }
            return ids
        }

        func addBranch(_ item: (SnapshotCategory, String, Int), path: String) {
            let children = (byParent[item.0.id] ?? [])
                .sorted {
                    if $0.2 != $1.2 {
                        return $0.2 < $1.2
                    }

                    return $0.1.localizedCaseInsensitiveCompare($1.1) == .orderedAscending
                }
            result.append(SnapshotCategoryGroup(
                id: item.0.id,
                name: path,
                categoryIDs: [item.0.id] + descendantIDs(for: item.0.id),
                order: item.2
            ))

            for child in children {
                addBranch(child, path: "\(path) / \(child.1)")
            }
        }

        let roots = visibleCategories
            .filter { item in
                guard let parentId = item.0.parentId, !parentId.isEmpty else {
                    return true
                }

                return byID[parentId] == nil
            }
            .sorted {
                if $0.2 != $1.2 {
                    return $0.2 < $1.2
                }

                return $0.1.localizedCaseInsensitiveCompare($1.1) == .orderedAscending
            }

        for root in roots {
            addBranch(root, path: root.1)
        }

        return result
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

    nonisolated private static func fetchLocalNetworkDesktopChangeStatus(
        address: String,
        port: Int
    ) async throws -> LocalNetworkDesktopChangeStatusResponse {
        guard !address.contains("/"),
              !address.contains(":"),
              !address.contains("?") else {
            throw SnapshotSyncError.invalidHTTPResponse
        }

        var components = URLComponents()
        components.scheme = "http"
        components.host = address
        components.port = port
        components.path = "/local-sync/changes/status"

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
        return try JSONDecoder().decode(LocalNetworkDesktopChangeStatusResponse.self, from: data)
    }

    nonisolated private static func rememberIpadAtDesktop(
        address: String,
        port: Int,
        deviceId: String,
        deviceName: String,
        appVersion: String?,
        sharedSecret: String
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
            lastSeenUtc: Date(),
            sharedSecret: sharedSecret
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

    private func ensureLocalNetworkTrustKey() -> String {
        if let existing = syncSettings.localNetworkDesktop.sharedSecret?.trimmingCharacters(in: .whitespacesAndNewlines),
           !existing.isEmpty {
            return existing
        }

        var generator = SystemRandomNumberGenerator()
        let bytes = (0..<32).map { _ in UInt8.random(in: UInt8.min...UInt8.max, using: &generator) }
        let trustKey = Data(bytes).base64EncodedString()
        var settings = syncSettings
        settings.localNetworkDesktop.sharedSecret = trustKey
        settings.localNetworkDesktop.trustKey = nil
        syncSettings = settings
        syncSettingsStore.save(settings)
        return trustKey
    }

    private static func currentIpadDeviceName() -> String {
        let name = UIDevice.current.name.trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? "iPad" : name
    }

    private static func currentAppVersion() -> String? {
        Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String
    }

    private func runLocalNetworkDesktopAutoCheck(address: String, port: Int) async {
        guard !isLocalNetworkDesktopServiceCheckRunning else { return }
        isLocalNetworkDesktopServiceCheckRunning = true
        localNetworkDesktopAutoCheckMessage = "Prüfung läuft …"
        localNetworkDesktopConnectionState = .checking
        print("Statusprüfung gestartet: http://\(address):\(port)/local-sync/status")
        print("connectionState gesetzt: \(LocalNetworkDesktopConnectionState.checking.title)")

        defer {
            isLocalNetworkDesktopServiceCheckRunning = false
        }

        do {
            let response = try await Self.fetchLocalNetworkDesktopStatus(
                address: address,
                port: port
            )
            if response.isExpectedLocalNetworkTestStatus {
                saveSuccessfulLocalNetworkDesktopCheck(address: address, port: port)
                await checkLocalNetworkDesktopChangeStatus(address: address, port: port)
                localNetworkDesktopConnectionState = .connected
                syncStatusMessage = isLocalNetworkDesktopRemembered
                    ? "Lokaler Desktop vorgemerkt"
                    : "Desktop-Sync-Dienst erreichbar"
                localNetworkDesktopAutoCheckMessage = isLocalNetworkDesktopRemembered
                    ? "Vorgemerkter Desktop erreichbar"
                    : "Desktop-Sync-Dienst erreichbar"
                print("/local-sync/status erfolgreich")
                print("connectionState gesetzt: \(LocalNetworkDesktopConnectionState.connected.title)")
            } else {
                localNetworkDesktopConnectionState = .disconnected
                if isLocalNetworkDesktopRemembered {
                    syncStatusMessage = "Lokaler Desktop vorgemerkt"
                    localNetworkDesktopAutoCheckMessage = "Automatische Prüfung: unerwartete Antwort"
                } else {
                    syncStatusMessage = "Desktop-Sync-Dienst nicht erreichbar: unerwartete Antwort"
                }
                print("/local-sync/status Fehler: unerwartete Antwort")
                print("connectionState gesetzt: \(LocalNetworkDesktopConnectionState.disconnected.title)")
            }
        } catch {
            localNetworkDesktopConnectionState = .disconnected
            if isLocalNetworkDesktopRemembered {
                syncStatusMessage = "Lokaler Desktop vorgemerkt"
                localNetworkDesktopAutoCheckMessage = "Automatische Prüfung: Desktop aktuell nicht erreichbar."
            } else {
                syncStatusMessage = "Desktop-Sync-Dienst nicht erreichbar: \(Self.shortLocalNetworkDesktopError(error))"
            }
            adoptRememberedLocalNetworkDesktopIfFound(in: discoveredLocalNetworkDesktops)
            print("/local-sync/status Fehler: \(Self.shortLocalNetworkDesktopError(error))")
            print("connectionState gesetzt: \(LocalNetworkDesktopConnectionState.disconnected.title)")
        }
    }

    private func autoCheckDiscoveredDesktopIfNeeded(_ desktops: [LocalNetworkDiscoveredDesktop]) {
        guard isLocalNetworkDesktopMainMonitoringActive else { return }
        guard !isLocalNetworkDesktopRemembered else { return }
        guard localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        guard let desktop = desktops.first else { return }
        saveDiscoveredLocalNetworkDesktop(desktop, status: "Desktop im lokalen Netzwerk gefunden")
        startLocalNetworkDesktopAutoCheck(address: desktop.address)
    }

    private func updateLocalNetworkDesktopConnectionStateForStoredSettings() {
        localNetworkDesktopConnectionState = localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            ? .disconnected
            : .checking
    }

    private func shouldAutoCheckLocalNetworkDesktop(address: String, port: Int) -> Bool {
        let remembered = syncSettings.localNetworkDesktop
        guard Self.isRememberedLocalNetworkDesktop(remembered) else {
            return true
        }
        return Self.matchesLocalNetworkDesktopEndpoint(
            address: address,
            port: port,
            remembered: remembered,
            fallbackPort: localNetworkDesktopTestPort
        )
    }

    private func loadStoredLocalNetworkDesktopSettings() {
        let storedSettings = syncSettingsStore.load()
        guard storedSettings.localNetworkDesktop != syncSettings.localNetworkDesktop else {
            return
        }

        var settings = syncSettings
        settings.localNetworkDesktop = storedSettings.localNetworkDesktop
        syncSettings = settings
        updateLocalNetworkDesktopConnectionStateForStoredSettings()
    }

    private func checkLocalNetworkDesktopChangeStatus(address: String, port: Int) async {
        do {
            let response = try await Self.fetchLocalNetworkDesktopChangeStatus(
                address: address,
                port: port
            )
            guard response.isExpectedLocalNetworkTestStatus else {
                return
            }
            var settings = syncSettings
            settings.localNetworkDesktop.lastChangeVersion = response.changeVersion
            settings.localNetworkDesktop.lastChangeStatusCheckAt = Date()
            syncSettings = settings
            syncSettingsStore.save(settings)
            localNetworkDesktopAutoCheckMessage = "Änderungsprüfung vorbereitet"
        } catch {
            localNetworkDesktopAutoCheckMessage = "Verbunden; Änderungsprüfung noch nicht verfügbar"
        }
    }

    private func saveSuccessfulLocalNetworkDesktopCheck(address: String, port: Int) {
        var settings = syncSettings
        let previousStatus = settings.localNetworkDesktop.localDesktopStatus
        if Self.isRememberedLocalNetworkDesktop(settings.localNetworkDesktop),
           !Self.matchesLocalNetworkDesktopEndpoint(
               address: address,
               port: port,
               remembered: settings.localNetworkDesktop,
               fallbackPort: localNetworkDesktopTestPort
           ) {
            return
        }
        settings.localNetworkDesktop.desktopAddress = address
        settings.localNetworkDesktop.desktopPort = port
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
            Self.matchesLocalNetworkDesktop(
                desktop,
                rememberedDeviceId: rememberedDeviceId,
                rememberedName: rememberedName,
                rememberedAddress: rememberedAddress,
                rememberedPort: remembered.desktopPort ?? localNetworkDesktopTestPort
            )
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
        // Eine erfolgreiche Erreichbarkeitspruefung ist noch keine bewusste Auswahl.
        // Erst "Diesen Desktop verwenden" oder die explizite Bonjour-Auswahl merkt ihn vor.
        return status == "lokaler Desktop vorgemerkt" ||
            status == "Lokaler Desktop vorgemerkt" ||
            status == "Desktop im lokalen Netzwerk gefunden"
    }

    nonisolated private static func matchesLocalNetworkDesktop(
        _ desktop: LocalNetworkDiscoveredDesktop,
        remembered: LocalNetworkDesktopPairing,
        fallbackPort: Int
    ) -> Bool {
        let rememberedDeviceId = remembered.desktopDeviceId?.trimmingCharacters(in: .whitespacesAndNewlines)
        let rememberedName = remembered.desktopName?.trimmingCharacters(in: .whitespacesAndNewlines)
        let rememberedAddress = remembered.desktopAddress?.trimmingCharacters(in: .whitespacesAndNewlines)
        return matchesLocalNetworkDesktop(
            desktop,
            rememberedDeviceId: rememberedDeviceId,
            rememberedName: rememberedName,
            rememberedAddress: rememberedAddress,
            rememberedPort: remembered.desktopPort ?? fallbackPort
        )
    }

    nonisolated private static func matchesLocalNetworkDesktopEndpoint(
        address: String,
        port: Int,
        remembered: LocalNetworkDesktopPairing,
        fallbackPort: Int
    ) -> Bool {
        let rememberedAddress = remembered.desktopAddress?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        guard !rememberedAddress.isEmpty else {
            return false
        }
        return rememberedAddress.caseInsensitiveCompare(address) == .orderedSame &&
            (remembered.desktopPort ?? fallbackPort) == port
    }

    nonisolated private static func matchesLocalNetworkDesktop(
        _ desktop: LocalNetworkDiscoveredDesktop,
        rememberedDeviceId: String?,
        rememberedName: String?,
        rememberedAddress: String?,
        rememberedPort: Int
    ) -> Bool {
        if let rememberedDeviceId, !rememberedDeviceId.isEmpty {
            return desktop.deviceId == rememberedDeviceId
        }
        if let rememberedName, !rememberedName.isEmpty {
            return desktop.name == rememberedName
        }
        if let hostName = desktop.hostName?.trimmingCharacters(in: .whitespacesAndNewlines),
           let rememberedAddress,
           !rememberedAddress.isEmpty,
           hostName.caseInsensitiveCompare(rememberedAddress) == .orderedSame {
            return desktop.port == rememberedPort
        }
        if let rememberedAddress, !rememberedAddress.isEmpty {
            return desktop.address == rememberedAddress && desktop.port == rememberedPort
        }
        return false
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

private struct LocalNetworkDesktopChangeStatusResponse: Decodable, Sendable {
    let app: String
    let status: String
    let mode: String
    let changeVersion: String?
    let lastChangedUtc: String?
    let syncActive: Bool

    var isExpectedLocalNetworkTestStatus: Bool {
        app == "BueroCockpit" &&
        status == "ok" &&
        mode == "local-network-test" &&
        syncActive == false
    }
}

private struct LocalNetworkDeviceRememberPayload: Encodable, Sendable {
    let deviceId: String
    let deviceName: String
    let platform: String
    let appVersion: String?
    let lastSeenUtc: Date
    let sharedSecret: String
}

private struct LocalNetworkDesktopRememberResponse: Decodable, Sendable {
    let app: String
    let status: String
    let mode: String
    let pairingStatus: String?
    let message: String?

    var isExpectedRememberResponse: Bool {
        app == "BueroCockpit" &&
        status == "ok" &&
        mode == "local-network-test"
    }
}
