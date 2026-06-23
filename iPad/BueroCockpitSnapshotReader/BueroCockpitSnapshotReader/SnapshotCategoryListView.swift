import SwiftUI

struct SnapshotCategoryListView: View {
    let categories: [SnapshotCategoryGroup]
    let selectedCategoryID: String
    let allTaskCount: Int
    let categoryCount: Int
    let loadedFileName: String?
    let snapshotDate: String?
    let taskCountForCategory: (String) -> Int
    let onImportSnapshot: () -> Void
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
            Section("Geladener Snapshot") {
                infoRow(label: "Datei", value: loadedFileName)
                infoRow(label: "Zeitpunkt", value: snapshotDate)
                infoRow(label: "Kategorien", value: "\(categoryCount)")
                infoRow(label: "Aufgaben", value: "\(allTaskCount)")

                Button(action: onImportSnapshot) {
                    Label("Live-Datei erneut importieren", systemImage: "square.and.arrow.down")
                }
            }
        }
        .navigationTitle("Kategorien")
        .listStyle(.sidebar)
    }

    @ViewBuilder
    private func infoRow(label: String, value: String?) -> some View {
        if let value, !value.isEmpty {
            VStack(alignment: .leading, spacing: 2) {
                Text(label)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(value)
                    .font(.callout)
                    .lineLimit(nil)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    @ViewBuilder
    private func row(title: String, count: Int, isSelected: Bool, systemImage: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Image(systemName: systemImage)
                    .frame(width: 18)
                Text(title)
                    .font(.body.weight(.medium))
                    .foregroundStyle(.primary)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer()
                Text("\(count)")
                    .font(.callout.monospacedDigit())
                    .foregroundStyle(.secondary)
                    .frame(width: 34, alignment: .trailing)
            }
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .listRowBackground(isSelected ? Color.accentColor.opacity(0.22) : Color.primary.opacity(0.04))
    }
}
