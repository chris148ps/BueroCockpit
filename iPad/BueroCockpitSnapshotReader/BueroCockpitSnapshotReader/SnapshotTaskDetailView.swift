import SwiftUI

struct SnapshotTaskDetailView: View {
    let task: SnapshotTask?
    let attachments: [SnapshotAttachmentIndex]
    let metadata: SnapshotMetadata?

    var body: some View {
        Group {
            if let task {
                ScrollView {
                    VStack(alignment: .leading, spacing: 20) {
                        header(task)
                        section(title: "Details") {
                            keyValue("Kunde", task.customerName)
                            keyValue("Status", task.status)
                            keyValue("Fällig am", task.displayDueDate)
                            keyValue("Erinnert am", task.displayReminderDate)
                            keyValue("Erstellt am", task.displayCreatedAt)
                            keyValue("Aktualisiert am", task.displayUpdatedAt)
                            keyValue("Material bestellt am", task.displayMaterialOrderedAt)
                        }
                        if let shortText = task.shortText, !shortText.isEmpty {
                            section(title: "Kurztext") {
                                Text(shortText)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                            }
                        }
                        if let notes = task.notes, !notes.isEmpty {
                            section(title: "Beschreibung") {
                                Text(notes)
                                    .frame(maxWidth: .infinity, alignment: .leading)
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
                                            Text(attachment.relativePath)
                                                .font(.footnote)
                                                .foregroundStyle(.secondary)
                                            HStack {
                                                Text(attachment.fileExists ? "Vorhanden" : "Fehlt")
                                                Spacer()
                                                if attachment.isImportant {
                                                    Text("Wichtig")
                                                }
                                            }
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                        }
                                        .padding(.vertical, 4)
                                    }
                                }
                            }
                        }
                        if let metadata {
                            section(title: "Snapshot") {
                                keyValue("App", metadata.appName)
                                keyValue("Version", metadata.displayAppVersion)
                                keyValue("Build", metadata.displayBuildIdentifier)
                                keyValue("Gerät", metadata.deviceName)
                                keyValue("Quelle", metadata.source)
                                keyValue("Format", metadata.formatVersion.map(String.init))
                                keyValue("Exportiert am", metadata.displayExportedAt)
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
            if let customerName = task.customerName, !customerName.isEmpty {
                Text(customerName)
                    .font(.title3)
                    .foregroundStyle(.secondary)
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
        if let value, !value.isEmpty {
            HStack(alignment: .top) {
                Text(label)
                    .foregroundStyle(.secondary)
                    .frame(width: 170, alignment: .leading)
                Text(value)
                    .foregroundStyle(.primary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
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
