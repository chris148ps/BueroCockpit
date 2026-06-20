import SwiftUI
import UniformTypeIdentifiers

struct SnapshotRootView: View {
    @StateObject private var viewModel = SnapshotBrowserViewModel()
    @State private var isPresentingFolderPicker = false

    var body: some View {
        Group {
            switch viewModel.loadState {
            case .ready:
                browserView
            case .idle:
                SnapshotStartView(
                    statusTitle: "Snapshot-Ordner auswählen",
                    statusMessage: "Wähle einen Ordner mit Sync/snapshots/, um Kategorien und Aufgaben anzuzeigen.",
                    action: openFolderPicker
                )
            case .loading:
                SnapshotStartView(
                    statusTitle: "Snapshot wird geladen …",
                    statusMessage: "Bitte warten. Die App liest gerade die lokalen Snapshot-Dateien ein.",
                    action: openFolderPicker
                )
            case .empty(let message):
                SnapshotStartView(
                    statusTitle: "Keine Snapshot-Daten gefunden",
                    statusMessage: message,
                    action: openFolderPicker
                )
            case .failure(let message):
                SnapshotStartView(
                    statusTitle: "Snapshot konnte nicht gelesen werden",
                    statusMessage: message,
                    action: openFolderPicker
                )
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
            ToolbarItem(placement: .topBarTrailing) {
                Button("Snapshot-Ordner auswählen") {
                    openFolderPicker()
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
                systemImage: "tray"
            ) {
                openFolderPicker()
            }
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message
            ) {
                openFolderPicker()
            }
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
                message: "Wähle den Ordner mit Sync/snapshots/ aus, um Kategorien und Aufgaben anzuzeigen.",
                systemImage: "folder"
            ) {
                openFolderPicker()
            }
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
                message: "Snapshot-Ordner auswählen. Die App arbeitet nur lesend.",
                systemImage: "tray.full"
            ) {
                openFolderPicker()
            }
        case .loading:
            ProgressView("Snapshot wird geladen …")
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .empty(let message):
            SnapshotEmptyStateView(
                title: "Keine Daten gefunden",
                message: message,
                systemImage: "tray"
            ) {
                openFolderPicker()
            }
        case .failure(let message):
            SnapshotErrorView(
                title: "Snapshot konnte nicht gelesen werden",
                message: message
            ) {
                openFolderPicker()
            }
        }
    }

    private func openFolderPicker() {
        isPresentingFolderPicker = true
    }

    private var titleForSelectedCategory: String {
        if viewModel.selectedCategoryID == SnapshotBrowserViewModel.allTasksCategoryID {
            return "Alle Aufgaben"
        }

        return viewModel.categories.first(where: { $0.id == viewModel.selectedCategoryID })?.name ?? "Aufgaben"
    }
}
