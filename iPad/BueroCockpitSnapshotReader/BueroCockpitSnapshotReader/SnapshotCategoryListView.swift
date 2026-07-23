import SwiftUI

struct SnapshotCategoryListView: View {
    let categories: [SnapshotCategoryGroup]
    let selectedCategoryID: String
    let allTaskCount: Int
    let taskCountForCategory: (String) -> Int
    let onSelectAll: () -> Void
    let onSelectCategory: (String) -> Void

    @State private var expandedCategoryIDs = Set<String>()

    var body: some View {
        List {
            Section {
                categoryButton(
                    title: "Alle Aufgaben",
                    count: allTaskCount,
                    isSelected:
                        selectedCategoryID
                        == SnapshotBrowserViewModel.allTasksCategoryID,
                    systemImage: "tray.full",
                    action: onSelectAll
                )
            }

            Section("Kategorien") {
                ForEach(rootNodes) { node in
                    CategoryTreeRow(
                        node: node,
                        selectedCategoryID: selectedCategoryID,
                        expandedCategoryIDs: $expandedCategoryIDs,
                        taskCountForCategory: taskCountForCategory,
                        onSelectCategory: onSelectCategory
                    )
                }
            }
        }
        .navigationTitle("Kategorien")
        .listStyle(.sidebar)
        .onAppear {
            expandPathToSelectedCategory()
        }
        .onChange(of: selectedCategoryID) { _, _ in
            expandPathToSelectedCategory()
        }
    }

    private var rootNodes: [SnapshotCategoryNode] {
        let hiddenNames = Set([
            "übersicht",
            "schreibtisch"
        ])

        let visibleCategories = categories.filter { category in
            !hiddenNames.contains(
                category.displayName
                    .trimmingCharacters(in: .whitespacesAndNewlines)
                    .lowercased()
            )
        }

        return SnapshotCategoryTreeBuilder.buildTree(
            from: visibleCategories
        )
    }

    private func expandPathToSelectedCategory() {
        guard selectedCategoryID
                != SnapshotBrowserViewModel.allTasksCategoryID
        else {
            return
        }

        let categoriesByID = Dictionary(
            uniqueKeysWithValues: categories.map { ($0.id, $0) }
        )

        var currentID = selectedCategoryID
        var visited = Set<String>()

        while let category = categoriesByID[currentID],
              let parentID = category.parentID,
              visited.insert(parentID).inserted {
            expandedCategoryIDs.insert(parentID)
            currentID = parentID
        }
    }

    private func categoryButton(
        title: String,
        count: Int,
        isSelected: Bool,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
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
        .listRowBackground(
            isSelected
                ? Color.accentColor.opacity(0.22)
                : Color.primary.opacity(0.04)
        )
    }
}

private struct CategoryTreeRow: View {
    let node: SnapshotCategoryNode
    let selectedCategoryID: String
    @Binding var expandedCategoryIDs: Set<String>
    let taskCountForCategory: (String) -> Int
    let onSelectCategory: (String) -> Void

    var body: some View {
        if node.children.isEmpty {
            categoryButton(systemImage: "folder")
        } else {
            DisclosureGroup(
                isExpanded: expansionBinding
            ) {
                ForEach(node.children) { child in
                    CategoryTreeRow(
                        node: child,
                        selectedCategoryID: selectedCategoryID,
                        expandedCategoryIDs: $expandedCategoryIDs,
                        taskCountForCategory: taskCountForCategory,
                        onSelectCategory: onSelectCategory
                    )
                }
            } label: {
                categoryButton(systemImage: "folder.fill")
            }
        }
    }

    private var expansionBinding: Binding<Bool> {
        Binding(
            get: {
                expandedCategoryIDs.contains(node.id)
            },
            set: { isExpanded in
                if isExpanded {
                    expandedCategoryIDs.insert(node.id)
                } else {
                    expandedCategoryIDs.remove(node.id)
                }
            }
        )
    }

    private func categoryButton(systemImage: String) -> some View {
        Button {
            onSelectCategory(node.id)
        } label: {
            HStack(spacing: 12) {
                Image(systemName: systemImage)
                    .frame(width: 18)

                Text(node.title)
                    .font(.body.weight(.medium))
                    .foregroundStyle(.primary)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)
                    .fixedSize(horizontal: false, vertical: true)

                Spacer()

                Text("\(taskCountForCategory(node.id))")
                    .font(.callout.monospacedDigit())
                    .foregroundStyle(.secondary)
                    .frame(width: 34, alignment: .trailing)
            }
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .listRowBackground(
            selectedCategoryID == node.id
                ? Color.accentColor.opacity(0.22)
                : Color.primary.opacity(0.04)
        )
    }
}

private struct SnapshotCategoryNode: Identifiable {
    let category: SnapshotCategoryGroup
    let children: [SnapshotCategoryNode]

    var id: String {
        category.id
    }

    var title: String {
        category.displayName
    }
}

private enum SnapshotCategoryTreeBuilder {
    static func buildTree(
        from categories: [SnapshotCategoryGroup]
    ) -> [SnapshotCategoryNode] {
        let categoriesByID = Dictionary(
            uniqueKeysWithValues: categories.map { ($0.id, $0) }
        )

        let childrenByParentID = Dictionary(
            grouping: categories
        ) { category in
            validParentID(
                for: category,
                categoriesByID: categoriesByID
            )
        }

        let roots = childrenByParentID[nil] ?? []

        return sorted(roots).map { root in
            buildNode(
                for: root,
                childrenByParentID: childrenByParentID,
                visitedIDs: []
            )
        }
    }

    private static func buildNode(
        for category: SnapshotCategoryGroup,
        childrenByParentID: [String?: [SnapshotCategoryGroup]],
        visitedIDs: Set<String>
    ) -> SnapshotCategoryNode {
        guard !visitedIDs.contains(category.id) else {
            return SnapshotCategoryNode(
                category: category,
                children: []
            )
        }

        var nextVisitedIDs = visitedIDs
        nextVisitedIDs.insert(category.id)

        let children = sorted(
            childrenByParentID[category.id] ?? []
        ).map { child in
            buildNode(
                for: child,
                childrenByParentID: childrenByParentID,
                visitedIDs: nextVisitedIDs
            )
        }

        return SnapshotCategoryNode(
            category: category,
            children: children
        )
    }

    private static func validParentID(
        for category: SnapshotCategoryGroup,
        categoriesByID: [String: SnapshotCategoryGroup]
    ) -> String? {
        guard let parentID = category.parentID,
              parentID != category.id,
              categoriesByID[parentID] != nil else {
            return nil
        }

        return parentID
    }

    private static func sorted(
        _ categories: [SnapshotCategoryGroup]
    ) -> [SnapshotCategoryGroup] {
        categories.sorted { lhs, rhs in
            if lhs.order != rhs.order {
                return lhs.order < rhs.order
            }

            return lhs.displayName.localizedCaseInsensitiveCompare(
                rhs.displayName
            ) == .orderedAscending
        }
    }
}
