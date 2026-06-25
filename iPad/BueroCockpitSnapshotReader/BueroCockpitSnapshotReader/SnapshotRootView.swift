import SwiftUI
import UniformTypeIdentifiers

struct SnapshotRootView: View {
    private enum PresentedSheet: Identifiable, Equatable {
        case setup
        case settings
        case mobileInspection

        var id: String {
            switch self {
            case .setup: "setup"
            case .settings: "settings"
            case .mobileInspection: "mobileInspection"
            }
        }
    }

    private enum SnapshotImportMode {
        case folder
        case metadata
        case package
        case iCloudPackage
        case mobileInboxFolder
    }

    @StateObject private var viewModel = SnapshotBrowserViewModel()
    @State private var mobileInboxStore = MobileInboxStore()
    @State private var mobileInboxWriter = MobileInboxWriter()
    @State private var isPackageImporterPresented = false
    @State private var activeImportMode: SnapshotImportMode?
    @State private var importStatusMessage: String?
    @State private var presentedSheet: PresentedSheet?
    @State private var importModeAfterSheetDismissal: SnapshotImportMode?
    @State private var refreshNoticeMessage: String?
    @State private var mobileInboxFolderPath: String?
    @State private var mobileInboxMessage: String?

    var body: some View {
        Group {
            if viewModel.setupRequired && viewModel.document == nil && viewModel.loadState != .loading {
                setupView
            } else {
                contentView
            }
        }
        .task {
            viewModel.restoreAtLaunch()
        }
        .onChange(of: viewModel.setupRequired) { _, isRequired in
            if isRequired, viewModel.document != nil {
                presentedSheet = .setup
            }
        }
        .fileImporter(
            isPresented: $isPackageImporterPresented,
            allowedContentTypes: allowedImportContentTypes,
            allowsMultipleSelection: false
        ) { result in
            defer {
                activeImportMode = nil
                importStatusMessage = nil
            }

            switch result {
            case .success(let urls):
                guard let url = urls.first else {
                    return
                }
                SnapshotPerformanceLog.event("File selected")
                handleImportedURL(url)
            case .failure(let error):
                handleImporterFailure(error)
            }
        }
        .sheet(item: $presentedSheet, onDismiss: {
            if let importModeAfterSheetDismissal {
                self.importModeAfterSheetDismissal = nil
                openImporter(importModeAfterSheetDismissal)
            }
        }) { sheet in
            switch sheet {
            case .setup:
                setupView
            case .settings:
                syncSetupView(onDismiss: { presentedSheet = nil })
            case .mobileInspection:
                MobileInspectionFormView(
                    categoryNames: viewModel.categories.map(\.name),
                    writer: mobileInboxWriter,
                    onSaved: { result in
                        mobileInboxMessage = "Gespeichert in \(result.entryURL.lastPathComponent)"
                        refreshNoticeMessage = "Mobile Besichtigung gespeichert:\n\(result.entryURL.path)"
                        viewModel.loadMobileInboxEntries(selectCategory: true)
                    },
                    onNeedsFolderSelection: {
                        importModeAfterSheetDismissal = .mobileInboxFolder
                        presentedSheet = nil
                    },
                    onDismiss: { presentedSheet = nil }
                )
            }
        }
        .onChange(of: viewModel.noticeMessage) { _, message in
            guard let message, !message.isEmpty else { return }
            refreshNoticeMessage = message
        }
        .alert("Aktualisierung", isPresented: Binding(
            get: { refreshNoticeMessage != nil },
            set: { isPresented in
                if !isPresented {
                    refreshNoticeMessage = nil
                    viewModel.clearNotice()
                }
            }
        )) {
            Button("OK", role: .cancel) {
                refreshNoticeMessage = nil
                viewModel.clearNotice()
            }
        } message: {
            Text(refreshNoticeMessage ?? "")
        }
    }

    @ViewBuilder
    private var contentView: some View {
        if viewModel.document == nil {
            setupView
        } else {
            switch viewModel.loadState {
            case .ready:
                browserView
            case .idle:
                browserView
            case .loading:
                browserView
            case .empty(let message):
                SnapshotEmptyStateView(
                    title: "Keine Snapshot-Daten gefunden",
                    message: message,
                    systemImage: "tray",
                    primaryButtonTitle: "Sync-Einstellungen",
                    primaryAction: { presentedSheet = .settings }
                )
            case .failure(let message):
                SnapshotErrorView(
                    title: "Snapshot konnte nicht gelesen werden",
                    message: message,
                    primaryButtonTitle: "Sync-Einstellungen",
                    primaryAction: { presentedSheet = .settings }
                )
            }
        }
    }

    private var setupView: some View {
        syncSetupView(onDismiss: nil)
    }

    private func syncSetupView(onDismiss: (() -> Void)?) -> some View {
        SnapshotSetupView(
            currentProvider: viewModel.syncSettings.providerType,
            savedGoogleDriveLink: viewModel.syncSettings.googleDriveLink,
            hasLocalSnapshot: viewModel.hasLocalSnapshot,
            hasICloudSnapshotSource: viewModel.hasICloudSnapshotSource,
            lastUpdatedText: viewModel.syncLastUpdatedText,
            message: viewModel.setupMessage,
            statusMessage: viewModel.syncStatusMessage ?? importStatusMessage,
            mobileInboxFolderPath: mobileInboxFolderPath ?? mobileInboxStore.selectedFolderDisplayPath,
            mobileInboxMessage: mobileInboxMessage,
            isWorking: viewModel.isSyncing,
            onSelectSnapshot: {
                if presentedSheet != nil {
                    importModeAfterSheetDismissal = .package
                    presentedSheet = nil
                } else {
                    openPackagePicker()
                }
            },
            onSelectICloudSnapshot: {
                if presentedSheet != nil {
                    importModeAfterSheetDismissal = .iCloudPackage
                    presentedSheet = nil
                } else {
                    openICloudPicker()
                }
            },
            onRefreshICloudSnapshot: refreshOrSelectICloudSnapshot,
            onReload: viewModel.refreshSnapshot,
            onTestGoogleDrive: viewModel.testGoogleDriveConnection,
            onSelectMobileInboxFolder: {
                if presentedSheet != nil {
                    importModeAfterSheetDismissal = .mobileInboxFolder
                    presentedSheet = nil
                } else {
                    openMobileInboxFolderPicker()
                }
            },
            onDismiss: onDismiss
        )
    }

    private var browserView: some View {
        NavigationSplitView(
            sidebar: {
                sidebarView
            },
            content: {
                contentColumnView
            },
            detail: {
                detailView
            }
        )
        .navigationSplitViewStyle(.balanced)
    }

    private var contentColumnView: some View {
        VStack(spacing: 0) {
            mainHeaderView
            if shouldShowTaskSearch {
                taskSearchField
            }
            Divider()
            taskList
        }
    }

    private var mainHeaderView: some View {
        HStack(spacing: 12) {
            Text("BüroCockpit")
                .font(.headline)

            Spacer()

            Button {
                presentedSheet = .mobileInspection
            } label: {
                Image(systemName: "plus")
            }
            .buttonStyle(.borderless)
            .help("Neue mobile Besichtigung")

            Button {
                refreshCurrentSyncSource()
            } label: {
                if viewModel.isSyncing || viewModel.loadState == .loading {
                    ProgressView()
                        .controlSize(.small)
                } else {
                    Image(systemName: "arrow.clockwise")
                }
            }
            .disabled(viewModel.isSyncing || viewModel.loadState == .loading)
            .buttonStyle(.borderless)
            .help("Aktualisieren")

            Button {
                presentedSheet = .settings
            } label: {
                Image(systemName: "gearshape")
            }
            .buttonStyle(.borderless)
            .help("Sync-Einstellungen")
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 8)
        .frame(minHeight: 44)
    }

    private var taskSearchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(.secondary)
            TextField("Aufträge suchen", text: $viewModel.searchText)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
            if !viewModel.searchText.isEmpty {
                Button {
                    viewModel.searchText = ""
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(Color.secondary.opacity(0.12), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        .padding(.horizontal, 16)
        .padding(.bottom, 8)
    }

    private var shouldShowTaskSearch: Bool {
        if case .ready = viewModel.loadState {
            return true
        }
        return false
    }

    private var sidebarView: some View {
        SnapshotCategoryListView(
            categories: viewModel.categories,
            selectedCategoryID: viewModel.selectedCategoryID,
            allTaskCount: viewModel.taskCount(in: SnapshotBrowserViewModel.allTasksCategoryID),
            categoryCount: viewModel.document?.categories.count ?? 0,
            loadedFileName: viewModel.loadedFileName,
            snapshotDate: viewModel.metadata?.displayExportedAt,
            taskCountForCategory: { categoryID in
                viewModel.taskCount(in: categoryID)
            },
            onImportSnapshot: openPackagePicker,
            onSelectAll: {
                viewModel.selectAllTasks()
            },
            onSelectCategory: { categoryID in
                viewModel.selectCategory(categoryID)
            }
        )
    }

    @ViewBuilder
    private var taskList: some View {
        switch viewModel.loadState {
        case .loading:
            ProgressView("Snapshot wird geladen …")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .empty(let message):
            SnapshotEmptyStateView(
                title: "Keine Daten gefunden",
                message: message,
                systemImage: "tray",
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message,
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .ready:
            if viewModel.filteredTasks.isEmpty {
                SnapshotEmptyStateView(
                    title: viewModel.tasks.isEmpty ? "Keine Aufträge" : "Keine Treffer",
                    message: viewModel.tasks.isEmpty
                        ? "Keine Aufträge im aktuellen Snapshot."
                        : "Keine passenden Aufträge gefunden.",
                    systemImage: viewModel.tasks.isEmpty ? "tray" : "magnifyingglass",
                    primaryButtonTitle: "Live-Datei erneut importieren",
                    primaryAction: openPackagePicker
                )
                .navigationTitle(viewModel.selectedCategoryTitle)
            } else {
                List(viewModel.filteredTasks, selection: Binding(
                    get: { viewModel.selectedTaskID },
                    set: { viewModel.selectTask($0) }
                )) { task in
                    taskRow(task)
                        .tag(task.id)
                }
                .navigationTitle(viewModel.selectedCategoryTitle)
            }
        case .idle:
            SnapshotEmptyStateView(
                title: "Live-Sync auswählen",
                message: "Wählen Sie Sync/live.bclive aus. Die Datei wird lokal in die App kopiert und dann gelesen.",
                systemImage: "folder",
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        }
    }

    private func taskRow(_ task: SnapshotTask) -> some View {
        if task.categoryIds.contains(SnapshotBrowserViewModel.mobilePendingCategoryID) {
            return AnyView(mobileInboxTaskRow(task))
        }

        return AnyView(snapshotTaskRow(task))
    }

    private func mobileInboxTaskRow(_ task: SnapshotTask) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            if let customerName = SnapshotDisplayFormatter.displayText(task.customerName) {
                Text(customerName)
                    .font(.headline)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Text(SnapshotDisplayFormatter.displayText(task.title) ?? "Neue mobile Besichtigung")
                .font(.subheadline.weight(.semibold))
                .fixedSize(horizontal: false, vertical: true)

            if let category = task.displayCategoryNames.dropFirst().first {
                Text(category)
                    .font(.footnote)
                    .foregroundStyle(.secondary)
            }

            if let summary = task.shortText {
                Text(summary)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if let metadata = SnapshotDisplayFormatter.joinedMetadata([
                "wartet auf Freigabe",
                task.displayCreatedAt
            ]) {
                Text(metadata)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.vertical, 6)
    }

    private func snapshotTaskRow(_ task: SnapshotTask) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            if let customerName = SnapshotDisplayFormatter.displayText(task.customerName) {
                Text(customerName)
                    .font(.headline)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Text(task.displayPrimaryTitle)
                .font(SnapshotDisplayFormatter.displayText(task.customerName) != nil ? .subheadline.weight(.semibold) : .headline)
                .fixedSize(horizontal: false, vertical: true)

            if let shortText = SnapshotDisplayFormatter.displayText(task.shortText) {
                Text(shortText)
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if let metadata = task.displayListMetadata {
                Text(metadata)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.vertical, 6)
    }

    @ViewBuilder
    private var detailView: some View {
        switch viewModel.loadState {
        case .ready:
            SnapshotTaskDetailView(
                task: viewModel.selectedTask,
                attachments: viewModel.selectedTask.map(viewModel.attachments(for:)) ?? [],
                attachmentLoader: viewModel.prepareAttachment
            )
        case .idle:
            SnapshotEmptyStateView(
                title: "BüroCockpit",
                message: "Wählen Sie Sync/live.bclive aus. Die Datei wird lokal in die App kopiert und dann gelesen. Die App arbeitet nur lesend.",
                systemImage: "tray.full",
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .loading:
            ProgressView("Snapshot wird geladen …")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .empty(let message):
            SnapshotEmptyStateView(
                title: "Keine Daten gefunden",
                message: message,
                systemImage: "tray",
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message,
                primaryButtonTitle: "Live-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Live-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        }
    }

    private func openFolderPicker() {
        presentImporter(
            mode: .folder,
            statusMessage: "Live-Ordnerauswahl wird geöffnet …"
        )
    }

    private func openImporter(_ mode: SnapshotImportMode) {
        switch mode {
        case .folder:
            openFolderPicker()
        case .metadata:
            openMetadataPicker()
        case .package:
            openPackagePicker()
        case .iCloudPackage:
            openICloudPicker()
        case .mobileInboxFolder:
            openMobileInboxFolderPicker()
        }
    }

    private func openMetadataPicker() {
        presentImporter(
            mode: .metadata,
            statusMessage: "metadata.json-Auswahl wird geöffnet …"
        )
    }

    private func openPackagePicker() {
        presentImporter(
            mode: .package,
            statusMessage: "Live-Dateiimport wird geöffnet …"
        )
    }

    private func openICloudPicker() {
        presentImporter(
            mode: .iCloudPackage,
            statusMessage: "iCloud-Dateiauswahl wird geöffnet …"
        )
    }

    private func openMobileInboxFolderPicker() {
        presentImporter(
            mode: .mobileInboxFolder,
            statusMessage: "Mobile-Inbox-Ordnerauswahl wird geöffnet …"
        )
    }

    private func refreshOrSelectICloudSnapshot() {
        guard !viewModel.isSyncing, viewModel.loadState != .loading else {
            return
        }

        if viewModel.hasICloudSnapshotSource {
            if viewModel.refreshICloudSnapshot(keepCurrentView: presentedSheet != nil) {
                return
            }
            openICloudSelectionAfterMissingAccess()
            return
        }

        viewModel.requestICloudSourceSelection()
        openICloudSelectionAfterMissingAccess()
    }

    private func refreshCurrentSyncSource() {
        if viewModel.isICloudDriveActive {
            refreshOrSelectICloudSnapshot()
        } else {
            viewModel.refreshSnapshot()
        }
    }

    private func openICloudSelectionAfterMissingAccess() {
        if presentedSheet != nil {
            importModeAfterSheetDismissal = .iCloudPackage
            presentedSheet = nil
        } else {
            openICloudPicker()
        }
    }

    private func presentImporter(mode: SnapshotImportMode, statusMessage: String) {
        SnapshotPerformanceLog.event("Import button pressed")
        activeImportMode = mode
        importStatusMessage = statusMessage
        isPackageImporterPresented = true
        SnapshotPerformanceLog.event("fileImporter presented state set")
    }

    private func handleImportedURL(_ sourceURL: URL) {
        switch activeImportMode {
        case .package:
            loadPackageSnapshot(from: sourceURL)
        case .iCloudPackage:
            loadICloudSnapshot(from: sourceURL)
        case .folder, .metadata:
            viewModel.importSnapshot(from: sourceURL)
        case .mobileInboxFolder:
            saveMobileInboxFolder(sourceURL)
        case .none:
            viewModel.present(errorMessage: "Die Auswahl konnte nicht zugeordnet werden.")
        }
    }

    private func saveMobileInboxFolder(_ folderURL: URL) {
        do {
            try mobileInboxStore.saveSelectedFolder(folderURL)
            mobileInboxFolderPath = folderURL.path
            mobileInboxMessage = "Mobile-Inbox-Ordner gespeichert."
            refreshNoticeMessage = "Mobile-Inbox-Ordner gespeichert:\n\(folderURL.path)"
        } catch {
            viewModel.present(errorMessage: error.localizedDescription)
        }
    }

    private func handleImporterFailure(_ error: Error) {
        let nsError = error as NSError
        if nsError.domain == NSCocoaErrorDomain, nsError.code == NSUserCancelledError {
            return
        }

        viewModel.present(errorMessage: error.localizedDescription)
    }

    private func loadPackageSnapshot(from sourceURL: URL) {
        guard isSnapshotPackage(sourceURL) else {
            viewModel.present(errorMessage: SnapshotReaderError.invalidPackageSelection.localizedDescription)
            return
        }

        viewModel.importSnapshot(from: sourceURL)
    }

    private func loadICloudSnapshot(from sourceURL: URL) {
        guard sourceURL.pathExtension.caseInsensitiveCompare("bclive") == .orderedSame else {
            viewModel.present(errorMessage: SnapshotReaderError.invalidPackageSelection.localizedDescription)
            return
        }

        viewModel.importICloudSnapshot(from: sourceURL)
    }

    private func isSnapshotPackage(_ url: URL) -> Bool {
        let extensionName = url.pathExtension.lowercased()
        return extensionName == "bclive" || extensionName == "bcsnapshot" || extensionName == "zip"
    }

    private func combinedStatusMessage(base: String) -> String? {
        guard let importStatusMessage, !importStatusMessage.isEmpty else {
            return base
        }

        return "\(base)\n\(importStatusMessage)"
    }

    private var allowedImportContentTypes: [UTType] {
        switch activeImportMode {
        case .folder, .mobileInboxFolder:
            return [.folder]
        case .metadata:
            return [.json]
        case .package, .iCloudPackage, .none:
            return [
                UTType(filenameExtension: "bclive") ?? .data,
                UTType(filenameExtension: "bcsnapshot") ?? .data,
                .zip
            ]
        }
    }

}
