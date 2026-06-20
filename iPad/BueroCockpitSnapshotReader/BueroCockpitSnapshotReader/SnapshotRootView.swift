import SwiftUI

struct SnapshotRootView: View {
    private enum SnapshotImportMode {
        case folder
        case metadata
        case package
    }

    @StateObject private var viewModel = SnapshotBrowserViewModel()
    @State private var isPackageImporterPresented = false
    @State private var activeImportMode: SnapshotImportMode?
    @State private var importStatusMessage: String?

    var body: some View {
        Group {
            switch viewModel.loadState {
            case .ready:
                browserView
            case .idle:
                SnapshotStartView(
                    statusTitle: "Snapshot-Datei importieren",
                    statusMessage: combinedStatusMessage(
                        base: "Für OneDrive bitte die Snapshot-Datei importieren. Die Paketdatei latest.bcsnapshot enthält die JSON-Daten in einer Datei."
                    ),
                    primaryButtonTitle: "Snapshot-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Snapshot-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .loading:
                SnapshotStartView(
                    statusTitle: "Snapshot wird geladen …",
                    statusMessage: combinedStatusMessage(
                        base: "Bitte warten. Die App liest gerade die lokalen Snapshot-Dateien ein."
                    ),
                    primaryButtonTitle: "Snapshot-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Snapshot-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .empty(let message):
                SnapshotStartView(
                    statusTitle: "Keine Snapshot-Daten gefunden",
                    statusMessage: combinedStatusMessage(base: message),
                    primaryButtonTitle: "Snapshot-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Snapshot-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            case .failure(let message):
                SnapshotStartView(
                    statusTitle: "Snapshot konnte nicht gelesen werden",
                    statusMessage: combinedStatusMessage(base: message),
                    primaryButtonTitle: "Snapshot-Datei importieren",
                    primaryAction: openPackagePicker,
                    secondaryButtonTitle: "Snapshot-Ordner auswählen",
                    secondaryAction: openFolderPicker,
                    tertiaryButtonTitle: "metadata.json auswählen",
                    tertiaryAction: openMetadataPicker
                )
            }
        }
        .fileImporter(
            isPresented: $isPackageImporterPresented,
            allowedContentTypes: [.folder, .json, .item],
            allowsMultipleSelection: false
        ) { result in
            defer {
                activeImportMode = nil
            }

            switch result {
            case .success(let urls):
                guard let url = urls.first else {
                    return
                }
                handleImportedURL(url)
            case .failure(let error):
                handleImporterFailure(error)
            }
        }
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
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button("Neuen Snapshot importieren") {
                    openPackagePicker()
                }

                Menu("Weitere Importoptionen") {
                    Button("Snapshot-Ordner auswählen") {
                        openFolderPicker()
                    }

                    Button("metadata.json auswählen") {
                        openMetadataPicker()
                    }
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
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message,
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
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
                    primaryButtonTitle: "Neuen Snapshot importieren",
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
                title: "Snapshot auswählen",
                message: "Wähle die Snapshot-Datei latest.bcsnapshot aus, um Kategorien und Aufgaben anzuzeigen.",
                systemImage: "folder",
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
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

            Text(task.title)
                .font(task.customerName?.isEmpty == false ? .subheadline.weight(.semibold) : .headline)
                .fixedSize(horizontal: false, vertical: true)

            if let shortText = SnapshotDisplayFormatter.displayText(task.shortText) {
                Text(shortText)
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            ViewThatFits(in: .horizontal) {
                HStack(spacing: 6) {
                    taskInfoChips(task)
                }
                VStack(alignment: .leading, spacing: 6) {
                    taskInfoChips(task)
                }
            }
        }
        .padding(.vertical, 6)
    }

    @ViewBuilder
    private func taskInfoChips(_ task: SnapshotTask) -> some View {
        if let status = task.displayStatus {
            infoChip(status, systemImage: "circle.fill")
        }
        ForEach(task.displayCategoryNames, id: \.self) { category in
            infoChip(category, systemImage: "folder")
        }
        if let dueDate = task.displayDueDate {
            infoChip("Fällig: \(dueDate)", systemImage: "calendar")
        } else if let createdAt = task.displayCreatedAt {
            infoChip("Erstellt: \(createdAt)", systemImage: "calendar")
        }
    }

    private func infoChip(_ text: String, systemImage: String) -> some View {
        Label(text, systemImage: systemImage)
            .font(.caption)
            .foregroundStyle(.secondary)
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
            .background(Color.secondary.opacity(0.12), in: Capsule())
            .fixedSize(horizontal: false, vertical: true)
    }

    @ViewBuilder
    private var detailView: some View {
        switch viewModel.loadState {
        case .ready:
            SnapshotTaskDetailView(
                task: viewModel.selectedTask,
                attachments: viewModel.selectedTask.map(viewModel.attachments(for:)) ?? []
            )
        case .idle:
            SnapshotEmptyStateView(
                title: "BüroCockpit",
                message: "Snapshot-Datei importieren. Die App arbeitet nur lesend.",
                systemImage: "tray.full",
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
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
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message,
                primaryButtonTitle: "Snapshot-Datei importieren",
                primaryAction: openPackagePicker,
                secondaryButtonTitle: "Snapshot-Ordner auswählen",
                secondaryAction: openFolderPicker,
                tertiaryButtonTitle: "metadata.json auswählen",
                tertiaryAction: openMetadataPicker
            )
        }
    }

    private func openFolderPicker() {
        activeImportMode = .folder
        importStatusMessage = "Snapshot-Ordnerauswahl wird geöffnet …"
        isPackageImporterPresented = true
    }

    private func openMetadataPicker() {
        activeImportMode = .metadata
        importStatusMessage = "metadata.json-Auswahl wird geöffnet …"
        isPackageImporterPresented = true
    }

    private func openPackagePicker() {
        activeImportMode = .package
        importStatusMessage = "Snapshot-Dateiauswahl wird geöffnet …"
        isPackageImporterPresented = true
    }

    private func handleImportedURL(_ sourceURL: URL) {
        switch activeImportMode {
        case .package:
            loadPackageSnapshot(from: sourceURL)
        case .folder, .metadata:
            viewModel.loadSnapshot(from: sourceURL)
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

        viewModel.loadSnapshot(from: sourceURL)
    }

    private func isSnapshotPackage(_ url: URL) -> Bool {
        let extensionName = url.pathExtension.lowercased()
        return extensionName == "bcsnapshot" || extensionName == "zip"
    }

    private func combinedStatusMessage(base: String) -> String? {
        guard let importStatusMessage, !importStatusMessage.isEmpty else {
            return base
        }

        return "\(base)\n\(importStatusMessage)"
    }

}
