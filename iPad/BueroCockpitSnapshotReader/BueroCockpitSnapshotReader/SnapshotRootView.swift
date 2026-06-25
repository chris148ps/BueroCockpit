import SwiftUI
import UniformTypeIdentifiers

struct SnapshotRootView: View {
    private enum PresentedSheet: Identifiable, Equatable {
        case setup
        case settings

        var id: String {
            switch self {
            case .setup: "setup"
            case .settings: "settings"
            }
        }
    }

    private enum SnapshotImportMode {
        case folder
        case metadata
        case package
    }

    @StateObject private var viewModel = SnapshotBrowserViewModel()
    @State private var isPackageImporterPresented = false
    @State private var activeImportMode: SnapshotImportMode?
    @State private var importStatusMessage: String?
    @State private var presentedSheet: PresentedSheet?
    @State private var importModeAfterSheetDismissal: SnapshotImportMode?

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
            }
        }
    }

    @ViewBuilder
    private var contentView: some View {
        if viewModel.document == nil && viewModel.loadState != .loading {
            setupView
        } else {
            switch viewModel.loadState {
            case .ready:
                browserView
            case .idle:
                SnapshotStartView(
                    statusTitle: "Live-Sync laden",
                    statusMessage: combinedStatusMessage(
                        base: "Wählen Sie Sync/live.bclive aus. Die Datei wird lokal in die App kopiert und dann gelesen."
                    ),
                    primaryButtonTitle: "Live-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Live-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .loading:
                SnapshotStartView(
                    statusTitle: viewModel.loadingTitle,
                    statusMessage: combinedStatusMessage(
                        base: viewModel.loadingDescription
                    ),
                    primaryButtonTitle: "Live-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Live-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .empty(let message):
                SnapshotStartView(
                    statusTitle: "Keine Snapshot-Daten gefunden",
                    statusMessage: combinedStatusMessage(base: message),
                    primaryButtonTitle: "Live-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Live-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .failure(let message):
                SnapshotStartView(
                    statusTitle: "Snapshot konnte nicht gelesen werden",
                    statusMessage: combinedStatusMessage(base: message),
                    primaryButtonTitle: viewModel.hasLocalSnapshot ? "Daten neu laden" : "Live-Datei importieren",
                    primaryAction: viewModel.hasLocalSnapshot ? viewModel.refreshSnapshot : openPackagePicker,
                    secondaryButtonTitle: "Live-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: nil,
                    tertiaryAction: nil
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
            message: viewModel.setupMessage,
            statusMessage: viewModel.syncStatusMessage ?? importStatusMessage,
            isWorking: viewModel.isSyncing || viewModel.loadState == .loading,
            onSelectSnapshot: {
                if presentedSheet != nil {
                    importModeAfterSheetDismissal = .package
                    presentedSheet = nil
                } else {
                    openPackagePicker()
                }
            },
            onReload: viewModel.refreshSnapshot,
            onTestGoogleDrive: viewModel.testGoogleDriveConnection,
            onDismiss: onDismiss
        )
    }

    private var browserView: some View {
        NavigationSplitView(
            sidebar: {
                sidebarView
            },
            content: {
                taskList
            },
            detail: {
                detailView
            }
        )
        .navigationSplitViewStyle(.balanced)
        .safeAreaInset(edge: .top) {
            if let notice = viewModel.noticeMessage {
                let isFailure = notice.localizedCaseInsensitiveContains("fehlgeschlagen")
                    || notice.localizedCaseInsensitiveContains("konnte nicht")
                Label(notice, systemImage: isFailure ? "exclamationmark.triangle" : "checkmark.circle")
                    .font(.callout)
                    .foregroundStyle(isFailure ? .orange : .green)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 16)
                    .padding(.vertical, 10)
                    .background(.thinMaterial)
            }
        }
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button("Daten neu laden") {
                    viewModel.refreshSnapshot()
                }

                Button("Live-Datei importieren") {
                    openPackagePicker()
                }

                Button("Live-Ordner auswählen") {
                    openFolderPicker()
                }

                Button("Sync-Quelle") {
                    presentedSheet = .settings
                }
            }
        }
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
                    title: viewModel.tasks.isEmpty ? "Keine Aufgaben im Snapshot" : "Keine Aufgaben gefunden",
                    message: viewModel.tasks.isEmpty
                        ? "Der geladene Snapshot enthält keine Aufgaben. Du kannst jederzeit einen neuen Snapshot importieren."
                        : "Für die aktuelle Kategorie und Suche wurden keine passenden Aufgaben gefunden.",
                    systemImage: viewModel.tasks.isEmpty ? "tray" : "magnifyingglass",
                    primaryButtonTitle: "Live-Datei erneut importieren",
                    primaryAction: openPackagePicker
                )
                .navigationTitle(viewModel.selectedCategoryTitle)
                .searchable(text: $viewModel.searchText, prompt: "Aufgaben durchsuchen")
            } else {
                List(viewModel.filteredTasks, selection: Binding(
                    get: { viewModel.selectedTaskID },
                    set: { viewModel.selectTask($0) }
                )) { task in
                    taskRow(task)
                        .tag(task.id)
                }
                .navigationTitle(viewModel.selectedCategoryTitle)
                .searchable(text: $viewModel.searchText, prompt: "Kunde, Auftrag, Beschreibung, Kategorie")
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
        case .folder, .metadata:
            viewModel.importSnapshot(from: sourceURL)
        case .none:
            viewModel.present(errorMessage: "Die Auswahl konnte nicht zugeordnet werden.")
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
        case .folder:
            return [.folder]
        case .metadata:
            return [.json]
        case .package, .none:
            return [
                UTType(filenameExtension: "bclive") ?? .data,
                UTType(filenameExtension: "bcsnapshot") ?? .data,
                .zip
            ]
        }
    }

}
