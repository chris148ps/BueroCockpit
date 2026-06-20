import SwiftUI

struct SnapshotTaskDetailView: View {
    let task: SnapshotTask?
    let attachments: [SnapshotAttachmentIndex]

    var body: some View {
        Group {
            if let task {
                ScrollView {
                    VStack(alignment: .leading, spacing: 20) {
                        header(task)
                        section(title: "Kunde / Auftrag") {
                            keyValue("Kunde", task.customerName)
                            keyValue("Auftrag / Betreff", task.title)
                            keyValue("Status", task.displayStatus)
                        }
                        if task.notes?.isEmpty == false || task.shortText?.isEmpty == false {
                            section(title: "Beschreibung / Notiz") {
                                Text(task.notes?.isEmpty == false ? task.notes! : task.shortText!)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .fixedSize(horizontal: false, vertical: true)
                                    .textSelection(.enabled)
                            }
                        }
                        if hasDates(task) {
                            section(title: "Termine / Datum") {
                            keyValue("Fällig am", task.displayDueDate)
                            keyValue("Wiedervorlage", task.displayReminderDate)
                            keyValue("Erstellt am", task.displayCreatedAt)
                            keyValue("Aktualisiert am", task.displayUpdatedAt)
                            keyValue("Material bestellt am", task.displayMaterialOrderedAt)
                        }
                        }
                        if !task.displayCategoryNames.isEmpty {
                            section(title: "Kategorien") {
                                FlowChips(items: task.displayCategoryNames)
                            }
                        }
                        if !attachments.isEmpty {
                            section(title: "Anhänge") {
                                VStack(alignment: .leading, spacing: 8) {
                                    ForEach(attachments) { attachment in
                                        VStack(alignment: .leading, spacing: 4) {
                                            Text(attachment.fileName)
                                                .font(.headline)
                                                .fixedSize(horizontal: false, vertical: true)
                                            if attachment.isImportant {
                                                Label("Wichtiger Anhang", systemImage: "exclamationmark.circle")
                                                    .font(.caption)
                                                    .foregroundStyle(.secondary)
                                            }
                                        }
                                        .padding(.vertical, 4)
                                    }
                                }
                            }
                        }
                    }
                    .padding(24)
                }
                .navigationTitle(task.title)
            } else {
                SnapshotEmptyStateView(
                    title: "Keine Aufgabe gewählt",
                    message: "Wähle links eine Kategorie und in der mittleren Spalte eine Aufgabe.",
                    systemImage: "doc.text.magnifyingglass"
                )
            }
        }
    }

    private func header(_ task: SnapshotTask) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(task.title)
                .font(.largeTitle.bold())
                .foregroundStyle(.primary)
                .fixedSize(horizontal: false, vertical: true)
            if let customerName = task.customerName, !customerName.isEmpty {
                Text(customerName)
                    .font(.title3)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    @ViewBuilder
    private func section(title: String, @ViewBuilder content: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(title)
                .font(.headline)
                .foregroundStyle(.primary)
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .background(Color.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.10), lineWidth: 1)
        )
    }

    @ViewBuilder
    private func keyValue(_ label: String, _ value: String?) -> some View {
        if let value = SnapshotDisplayFormatter.displayText(value) {
            HStack(alignment: .top) {
                Text(label)
                    .foregroundStyle(.secondary)
                    .frame(width: 170, alignment: .leading)
                Text(value)
                    .foregroundStyle(.primary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private func hasDates(_ task: SnapshotTask) -> Bool {
        [
            task.displayDueDate,
            task.displayReminderDate,
            task.displayCreatedAt,
            task.displayUpdatedAt,
            task.displayMaterialOrderedAt
        ].contains(where: { $0 != nil })
    }
}

private struct FlowChips: View {
    let items: [String]

    var body: some View {
        FlowLayout(items: items) { item in
            Text(item)
                .font(.subheadline)
                .foregroundStyle(.primary)
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.accentColor.opacity(0.15), in: Capsule())
        }
    }
}

private struct FlowLayout<Data: RandomAccessCollection, Content: View>: View where Data.Element: Hashable {
    let items: Data
    let content: (Data.Element) -> Content

    var body: some View {
        LazyVGrid(
            columns: [GridItem(.adaptive(minimum: 120), spacing: 8, alignment: .leading)],
            alignment: .leading,
            spacing: 8
        ) {
            ForEach(Array(items), id: \.self, content: content)
        }
    }
}
