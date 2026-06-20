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
                Button("Snapshot-Datei importieren") {
                    openPackagePicker()
                }

                Button("Snapshot-Ordner auswählen") {
                    openFolderPicker()
                }

                Button("metadata.json auswählen") {
                    openMetadataPicker()
                }
            }
        }
    }

    private var sidebarView: some View {
        SnapshotCategoryListView(
            categories: viewModel.categories,
            selectedCategoryID: viewModel.selectedCategoryID,
            allTaskCount: viewModel.taskCount(in: SnapshotBrowserViewModel.allTasksCategoryID),
            taskCountForCategory: { categoryID in
                viewModel.taskCount(in: categoryID)
            },
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
            List(viewModel.filteredTasks, selection: Binding(
                get: { viewModel.selectedTaskID },
                set: { viewModel.selectTask($0) }
            )) { task in
                VStack(alignment: .leading, spacing: 4) {
                    Text(task.title)
                        .font(.headline)
                    if let customerName = task.customerName, !customerName.isEmpty {
                        Text(customerName)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                    if let shortText = task.shortText, !shortText.isEmpty {
                        Text(shortText)
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                            .lineLimit(2)
                    }
                }
                .padding(.vertical, 4)
            }
            .navigationTitle(titleForSelectedCategory)
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

    @ViewBuilder
    private var detailView: some View {
        switch viewModel.loadState {
        case .ready:
            SnapshotTaskDetailView(
                task: viewModel.selectedTask,
                attachments: viewModel.selectedTask.map(viewModel.attachments(for:)) ?? [],
                metadata: viewModel.metadata
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

    private var titleForSelectedCategory: String {
        if viewModel.selectedCategoryID == SnapshotBrowserViewModel.allTasksCategoryID {
            return "Alle Aufgaben"
        }

        return viewModel.categories.first(where: { $0.id == viewModel.selectedCategoryID })?.name ?? "Aufgaben"
    }
}
