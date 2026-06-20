import SwiftUI

struct SnapshotCategoryListView: View {
    let categories: [SnapshotCategory]
    let selectedCategoryID: String
    let allTaskCount: Int
    let taskCountForCategory: (String) -> Int
    let onSelectAll: () -> Void
    let onSelectCategory: (String) -> Void

    var body: some View {
        List {
            Section {
                row(
                    title: "Alle Aufgaben",
                    count: allTaskCount,
                    isSelected: selectedCategoryID == SnapshotBrowserViewModel.allTasksCategoryID,
                    systemImage: "tray.full"
                ) {
                    onSelectAll()
                }
            }

            Section("Kategorien") {
                ForEach(categories) { category in
                    row(
                        title: category.name,
                        count: taskCountForCategory(category.id),
                        isSelected: selectedCategoryID == category.id,
                        systemImage: "folder"
                    ) {
                        onSelectCategory(category.id)
                    }
                }
            }
        }
        .navigationTitle("Kategorien")
        .listStyle(.sidebar)
    }

    @ViewBuilder
    private func row(title: String, count: Int, isSelected: Bool, systemImage: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Image(systemName: systemImage)
                    .frame(width: 18)
                Text(title)
                    .lineLimit(1)
                Spacer()
                Text("\(count)")
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .listRowBackground(isSelected ? Color.accentColor.opacity(0.18) : Color.clear)
    }
}
