import SwiftUI
import UniformTypeIdentifiers

struct SnapshotRootView: View {
    @StateObject private var viewModel = SnapshotBrowserViewModel()
    @State private var isPresentingFolderPicker = false
    @State private var isPresentingMetadataPicker = false
    @State private var isPresentingPackagePicker = false

    var body: some View {
        Group {
            switch viewModel.loadState {
            case .ready:
                browserView
            case .idle:
                SnapshotStartView(
                    statusTitle: "Snapshot-Datei importieren",
                    statusMessage: "Für OneDrive bitte die Snapshot-Datei importieren. Die Paketdatei latest.bcsnapshot enthält die JSON-Daten in einer Datei.",
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
                    statusMessage: "Bitte warten. Die App liest gerade die lokalen Snapshot-Dateien ein.",
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
                    statusMessage: message,
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
                    statusMessage: message,
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
            isPresented: $isPresentingPackagePicker,
            allowedContentTypes: packageImportContentTypes,
            allowsMultipleSelection: false
        ) { result in
            switch result {
            case .success(let urls):
                guard let url = urls.first else {
                    return
                }
                loadPackageSnapshot(from: url)
            case .failure(let error):
                viewModel.present(errorMessage: error.localizedDescription)
            }
        }
        .fileImporter(
            isPresented: $isPresentingFolderPicker,
            allowedContentTypes: [.folder],
            allowsMultipleSelection: false
        ) { result in
            switch result {
            case .success(let urls):
                guard let url = urls.first else {
                    return
                }
                viewModel.loadSnapshot(from: url)
            case .failure(let error):
                viewModel.present(errorMessage: error.localizedDescription)
            }
        }
        .fileImporter(
            isPresented: $isPresentingMetadataPicker,
            allowedContentTypes: [.json],
            allowsMultipleSelection: false
        ) { result in
            switch result {
            case .success(let urls):
                guard let url = urls.first else {
                    return
                }
                viewModel.loadSnapshot(from: url)
            case .failure(let error):
                viewModel.present(errorMessage: error.localizedDescription)
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

    private var packageImportContentTypes: [UTType] {
        var contentTypes: [UTType] = [.item]
        if let packageType = UTType(filenameExtension: "bcsnapshot") {
            contentTypes.insert(packageType, at: 0)
        }
        return contentTypes
    }

    private func openFolderPicker() {
        isPresentingFolderPicker = true
    }

    private func openMetadataPicker() {
        isPresentingMetadataPicker = true
    }

    private func openPackagePicker() {
        isPresentingPackagePicker = true
    }

    private func loadPackageSnapshot(from sourceURL: URL) {
        guard sourceURL.pathExtension.caseInsensitiveCompare("bcsnapshot") == .orderedSame else {
            viewModel.present(errorMessage: SnapshotReaderError.invalidPackageSelection.localizedDescription)
            return
        }

        viewModel.loadSnapshot(from: sourceURL)
    }

    private var titleForSelectedCategory: String {
        if viewModel.selectedCategoryID == SnapshotBrowserViewModel.allTasksCategoryID {
            return "Alle Aufgaben"
        }

        return viewModel.categories.first(where: { $0.id == viewModel.selectedCategoryID })?.name ?? "Aufgaben"
    }
}
