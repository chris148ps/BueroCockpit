import SwiftUI
import QuickLook
import PDFKit

struct SnapshotTaskDetailView: View {
    let task: SnapshotTask?
    let attachments: [SnapshotAttachmentIndex]
    let attachmentLoader: (SnapshotAttachmentIndex) async throws -> URL
    @State private var previewItem: SnapshotPreviewItem?
    @State private var attachmentNotice: String?
    @State private var loadingAttachmentID: String?
    @State private var unavailableAttachmentIDs: Set<String> = []

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
                                VStack(alignment: .leading, spacing: 10) {
                                    ForEach(attachments) { attachment in
                                        Button {
                                            openAttachment(attachment)
                                        } label: {
                                            attachmentRow(
                                                attachment,
                                                previewUnavailable: unavailableAttachmentIDs.contains(attachment.id)
                                            )
                                        }
                                        .buttonStyle(.plain)
                                        .disabled(loadingAttachmentID != nil)
                                    }
                                }
                            }
                        }
                    }
                    .padding(24)
                }
            } else {
                SnapshotEmptyStateView(
                    title: "Keine Aufgabe gewählt",
                    message: "Wähle links eine Kategorie und in der mittleren Spalte eine Aufgabe.",
                    systemImage: "doc.text.magnifyingglass"
                )
            }
        }
        .sheet(item: $previewItem) { item in
            switch item.kind {
            case .pdf:
                SnapshotPDFPreview(url: item.url)
            case .quickLook:
                SnapshotQuickLookPreview(url: item.url)
            }
        }
        .alert("Anhang kann nicht geöffnet werden", isPresented: Binding(
            get: { attachmentNotice != nil },
            set: { isPresented in
                if !isPresented {
                    attachmentNotice = nil
                }
            }
        )) {
            Button("OK", role: .cancel) {
                attachmentNotice = nil
            }
        } message: {
            Text(attachmentNotice ?? "")
        }
        .overlay {
            if loadingAttachmentID != nil {
                VStack(spacing: 12) {
                    ProgressView()
                    Text("Anhang wird vorbereitet …")
                        .font(.headline)
                    Text("Bei großen Dateien kann dies einen Moment dauern.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                .padding(24)
                .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 16, style: .continuous))
                .shadow(radius: 12)
            }
        }
    }

    private func header(_ task: SnapshotTask) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(task.displayPrimaryTitle)
                .font(.largeTitle.bold())
                .foregroundStyle(.primary)
                .fixedSize(horizontal: false, vertical: true)
            if let secondaryTitle = task.displaySecondaryTitle {
                Text(secondaryTitle)
                    .font(.title3.weight(.semibold))
                    .foregroundStyle(.primary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if let metadata = task.displayDetailMetadata {
                Text(metadata)
                    .font(.subheadline)
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

    private func attachmentRow(_ attachment: SnapshotAttachmentIndex, previewUnavailable: Bool) -> some View {
        HStack(alignment: .center, spacing: 12) {
            ZStack {
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(Color.accentColor.opacity(0.12))
                Image(systemName: attachment.systemImageName)
                    .font(.title3)
                    .foregroundStyle(Color.accentColor)
            }
            .frame(width: 44, height: 44)

            VStack(alignment: .leading, spacing: 4) {
                Text(attachment.displayTitle)
                    .font(.headline)
                    .foregroundStyle(.primary)
                    .fixedSize(horizontal: false, vertical: true)

                Text(attachmentMetadata(attachment, previewUnavailable: previewUnavailable))
                .font(.caption)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

                if attachment.isImportant {
                    Label("Wichtiger Anhang", systemImage: "exclamationmark.circle")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            if loadingAttachmentID == attachment.id {
                ProgressView()
            } else {
                Image(systemName: attachment.canPreview && !previewUnavailable ? "chevron.right" : "exclamationmark.circle")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(.secondary)
            }
        }
        .padding(10)
        .background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.08), lineWidth: 1)
        )
    }

    private func openAttachment(_ attachment: SnapshotAttachmentIndex) {
        guard attachment.canPreview else {
            attachmentNotice = attachment.existsInSnapshot
                ? "Die Datei ist im Snapshot enthalten, kann aber nicht geöffnet werden."
                : attachment.fileExists
                ? "Die Datei ist im BüroCockpit-Datenordner vorhanden, aber nicht in diesem Snapshot-Paket enthalten."
                : attachment.exportHint ?? "Die Datei ist im Snapshot-Index vermerkt, wurde aber nicht gefunden."
            return
        }

        loadingAttachmentID = attachment.id
        Task {
            defer { loadingAttachmentID = nil }
            do {
                let localURL = try await attachmentLoader(attachment)
                if localURL.pathExtension.localizedCaseInsensitiveCompare("pdf") == .orderedSame {
                    let canOpenPDF = await Task.detached(priority: .userInitiated) {
                        PDFDocument(url: localURL) != nil
                    }.value
                    guard canOpenPDF else {
                        throw SnapshotAttachmentError.previewUnavailable
                    }
                    previewItem = SnapshotPreviewItem(url: localURL, kind: .pdf)
                } else {
                    guard QLPreviewController.canPreview(localURL as NSURL) else {
                        throw SnapshotAttachmentError.previewUnavailable
                    }
                    previewItem = SnapshotPreviewItem(url: localURL, kind: .quickLook)
                }
                unavailableAttachmentIDs.remove(attachment.id)
            } catch {
                if case SnapshotAttachmentError.previewUnavailable = error {
                    unavailableAttachmentIDs.insert(attachment.id)
                }
                attachmentNotice = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
            }
        }
    }

    private func attachmentMetadata(_ attachment: SnapshotAttachmentIndex, previewUnavailable: Bool) -> String {
        [
            attachment.displayFileType,
            attachment.displaySize,
            previewUnavailable ? "Vorschau nicht verfügbar" : attachment.availabilityText
        ]
        .compactMap { $0 }
        .joined(separator: " · ")
    }
}

private struct SnapshotPreviewItem: Identifiable {
    enum Kind {
        case pdf
        case quickLook
    }

    let url: URL
    let kind: Kind
    var id: String { "\(kind)-\(url.path)" }
}

private struct SnapshotPDFPreview: UIViewRepresentable {
    let url: URL

    func makeUIView(context: Context) -> PDFView {
        let view = PDFView()
        view.autoScales = true
        view.displayMode = .singlePageContinuous
        view.displayDirection = .vertical
        view.document = PDFDocument(url: url)
        return view
    }

    func updateUIView(_ uiView: PDFView, context: Context) {
        if uiView.document?.documentURL != url {
            uiView.document = PDFDocument(url: url)
        }
    }
}

private struct SnapshotQuickLookPreview: UIViewControllerRepresentable {
    let url: URL

    func makeCoordinator() -> Coordinator {
        Coordinator(url: url)
    }

    func makeUIViewController(context: Context) -> QLPreviewController {
        let controller = QLPreviewController()
        controller.dataSource = context.coordinator
        return controller
    }

    func updateUIViewController(_ uiViewController: QLPreviewController, context: Context) {
        context.coordinator.url = url
        uiViewController.reloadData()
    }

    final class Coordinator: NSObject, QLPreviewControllerDataSource {
        var url: URL

        init(url: URL) {
            self.url = url
        }

        func numberOfPreviewItems(in controller: QLPreviewController) -> Int {
            1
        }

        func previewController(_ controller: QLPreviewController, previewItemAt index: Int) -> QLPreviewItem {
            url as NSURL
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
