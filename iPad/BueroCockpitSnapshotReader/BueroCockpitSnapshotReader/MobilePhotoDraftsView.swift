import SwiftUI
import UIKit
import PhotosUI

struct MobilePhotoDraftsView: View {
    let store: MobilePhotoDraftStore
    let availableTasks: [SnapshotTask]
    let onDraftsChanged: () -> Void
    let onDismiss: () -> Void

    @State private var collection = MobilePhotoDraftCollection()
    @State private var statusMessage: String?
    @State private var selectedPhotoItem: PhotosPickerItem?
    @State private var isImportingPhoto = false
    @State private var draftPendingRemoval: MobilePhotoDraft?
    @State private var draftPendingAssignment: MobilePhotoDraft?

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Text("Fotos können später vor einem Auftrag gesammelt oder direkt einem Auftrag zugeordnet werden.")
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)

                    Label("Foto-Entwürfe: \(collection.drafts.count)", systemImage: "camera")
                        .font(.callout.weight(.semibold))
                }

                Section {
                    PhotosPicker(
                        selection: $selectedPhotoItem,
                        matching: .images
                    ) {
                        Label("Foto auswählen", systemImage: "photo.on.rectangle")
                    }
                    .disabled(isImportingPhoto)

                    if isImportingPhoto {
                        ProgressView("Foto wird lokal gespeichert ...")
                    }
                }

                Section {
                    if collection.drafts.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            Label("Noch keine Foto-Entwürfe", systemImage: "photo.on.rectangle.angled")
                                .font(.headline)
                            Text("Der Foto-Modus speichert ausgewählte Bilder nur lokal auf diesem iPad. Es wird noch nichts übertragen oder einem Auftrag zugeordnet.")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        .padding(.vertical, 8)
                    } else {
                        if availableTasks.isEmpty {
                            Text("Es sind noch keine Snapshot-Aufträge geladen. Foto-Entwürfe bleiben lokal gespeichert; die Zuordnung ist möglich, sobald ein Snapshot mit Aufträgen vorhanden ist.")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }

                        ForEach(collection.drafts) { draft in
                            draftRow(draft)
                                .swipeActions(edge: .trailing) {
                                    Button(role: .destructive) {
                                        requestRemoval(of: draft)
                                    } label: {
                                        Label("Entfernen", systemImage: "trash")
                                    }
                                }
                        }
                    }
                } header: {
                    Text("Lokale Entwürfe")
                }

                Section {
                    Button {
                        createTestDraft()
                    } label: {
                        Label("Test-Fotoentwurf anlegen", systemImage: "plus")
                    }
                    .buttonStyle(.borderless)

                    if let statusMessage, !statusMessage.isEmpty {
                        Text(statusMessage)
                            .font(.footnote)
                            .foregroundStyle(.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
            .navigationTitle("Foto-Modus")
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Fertig", action: onDismiss)
                }
            }
            .onAppear(perform: loadDrafts)
            .onChange(of: selectedPhotoItem) { _, item in
                guard let item else { return }
                importPhoto(item)
            }
            .confirmationDialog(
                "Foto-Entwurf entfernen?",
                isPresented: removalConfirmationPresented,
                titleVisibility: .visible
            ) {
                if let draft = draftPendingRemoval {
                    Button("Entfernen", role: .destructive) {
                        removeDraft(draft)
                    }
                }
                Button("Abbrechen", role: .cancel) {}
            } message: {
                if let draft = draftPendingRemoval {
                    Text("\(draftTitle(for: draft)) und die zugehörigen lokalen Bilddateien werden von diesem iPad entfernt.")
                } else {
                    Text("Der lokale Entwurf und die zugehörigen lokalen Bilddateien werden von diesem iPad entfernt.")
                }
            }
            .sheet(item: $draftPendingAssignment) { draft in
                MobilePhotoDraftTaskPickerView(
                    draftTitle: draftTitle(for: draft),
                    tasks: availableTasks,
                    selectedTaskID: draft.linkedTaskId,
                    onSelect: { task in
                        assignDraft(draft, to: task)
                    },
                    onClear: clearAssignmentAction(for: draft),
                    onDismiss: {
                        draftPendingAssignment = nil
                    }
                )
            }
        }
    }

    private var removalConfirmationPresented: Binding<Bool> {
        Binding(
            get: { draftPendingRemoval != nil },
            set: { isPresented in
                if !isPresented {
                    draftPendingRemoval = nil
                }
            }
        )
    }

    private func draftRow(_ draft: MobilePhotoDraft) -> some View {
        HStack(alignment: .top, spacing: 12) {
            thumbnailView(for: draft)

            VStack(alignment: .leading, spacing: 6) {
                Text(draftTitle(for: draft))
                    .font(.headline)
                    .fixedSize(horizontal: false, vertical: true)

                Text(draftMetadata(for: draft))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)

                if let note = draft.note, !note.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    Text(note)
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                assignmentView(for: draft)

                Button {
                    draftPendingAssignment = draft
                } label: {
                    Label(draft.linkedTaskId == nil ? "Auftrag zuordnen" : "Auftrag ändern", systemImage: "link")
                }
                .buttonStyle(.borderless)
                .disabled(availableTasks.isEmpty)
                .accessibilityHint(availableTasks.isEmpty ? "Es sind keine Snapshot-Aufträge geladen." : "")
            }

            Spacer(minLength: 8)

            Button(role: .destructive) {
                requestRemoval(of: draft)
            } label: {
                Label("Entfernen", systemImage: "trash")
                    .labelStyle(.iconOnly)
            }
            .buttonStyle(.borderless)
            .accessibilityLabel("Foto-Entwurf entfernen")
        }
        .padding(.vertical, 4)
    }

    @ViewBuilder
    private func assignmentView(for draft: MobilePhotoDraft) -> some View {
        if let linkedTaskId = draft.linkedTaskId?.trimmingCharacters(in: .whitespacesAndNewlines), !linkedTaskId.isEmpty {
            if let task = task(for: linkedTaskId) {
                VStack(alignment: .leading, spacing: 2) {
                    Label("Zugeordnet", systemImage: "checkmark.circle")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.secondary)
                    Text(taskDisplayTitle(task))
                        .font(.callout.weight(.semibold))
                        .fixedSize(horizontal: false, vertical: true)
                    if let detail = taskDisplayDetail(task) {
                        Text(detail)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            } else {
                Label("Zugeordnet: Auftrag \(linkedTaskId)", systemImage: "checkmark.circle")
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        } else {
            Label("Noch keinem Auftrag zugeordnet", systemImage: "link.badge.plus")
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    @ViewBuilder
    private func thumbnailView(for draft: MobilePhotoDraft) -> some View {
        if let image = thumbnailImage(for: draft) {
            Image(uiImage: image)
                .resizable()
                .scaledToFill()
                .frame(width: 64, height: 64)
                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                .accessibilityHidden(true)
        } else {
            Image(systemName: "photo")
                .font(.title2)
                .foregroundStyle(.secondary)
                .frame(width: 64, height: 64)
                .background(.thinMaterial)
                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                .accessibilityHidden(true)
        }
    }

    private func thumbnailImage(for draft: MobilePhotoDraft) -> UIImage? {
        if let thumbnailPath = draft.thumbnailPath,
           let image = UIImage(contentsOfFile: thumbnailPath) {
            return image
        }

        if let imagePath = draft.localImagePath ?? draft.originalLocalPath {
            return UIImage(contentsOfFile: imagePath)
        }

        return nil
    }

    private func loadDrafts() {
        collection = store.load()
    }

    private func importPhoto(_ item: PhotosPickerItem) {
        Task {
            await importPhotoAsync(item)
        }
    }

    @MainActor
    private func importPhotoAsync(_ item: PhotosPickerItem) async {
        isImportingPhoto = true
        statusMessage = nil
        defer {
            isImportingPhoto = false
            selectedPhotoItem = nil
        }

        do {
            guard let data = try await item.loadTransferable(type: Data.self), !data.isEmpty else {
                statusMessage = "Das ausgewählte Foto konnte nicht gelesen werden."
                return
            }

            collection = try store.importPhotoDraft(
                imageData: data,
                originalFilename: nil,
                sourceDevice: UIDevice.current.name,
                source: .photoLibrary
            )
            statusMessage = "Foto lokal als Entwurf gespeichert."
            onDraftsChanged()
        } catch {
            statusMessage = "Foto konnte nicht gespeichert werden: \(error.localizedDescription)"
        }
    }

    private func createTestDraft() {
        do {
            collection = try store.createDummyDraft(sourceDevice: UIDevice.current.name, source: .iPadCamera)
            statusMessage = "Test-Fotoentwurf lokal angelegt."
            onDraftsChanged()
        } catch {
            statusMessage = "Test-Fotoentwurf konnte nicht gespeichert werden: \(error.localizedDescription)"
        }
    }

    private func requestRemoval(of draft: MobilePhotoDraft) {
        draftPendingRemoval = draft
    }

    private func removeDraft(_ draft: MobilePhotoDraft) {
        do {
            collection = try store.removeDraft(id: draft.id)
            statusMessage = "Foto-Entwurf entfernt."
            onDraftsChanged()
        } catch {
            collection = store.load()
            statusMessage = "Foto-Entwurf konnte nicht entfernt werden: \(error.localizedDescription)"
            onDraftsChanged()
        }
    }

    private func assignDraft(_ draft: MobilePhotoDraft, to task: SnapshotTask) {
        do {
            collection = try store.updateLinkedTask(for: draft.id, linkedTaskId: task.id)
            statusMessage = "Foto-Entwurf lokal zugeordnet."
            draftPendingAssignment = nil
            onDraftsChanged()
        } catch {
            collection = store.load()
            statusMessage = "Zuordnung konnte nicht gespeichert werden: \(error.localizedDescription)"
            draftPendingAssignment = nil
            onDraftsChanged()
        }
    }

    private func clearAssignedTask(for draft: MobilePhotoDraft) {
        do {
            collection = try store.updateLinkedTask(for: draft.id, linkedTaskId: nil)
            statusMessage = "Zuordnung lokal entfernt."
            draftPendingAssignment = nil
            onDraftsChanged()
        } catch {
            collection = store.load()
            statusMessage = "Zuordnung konnte nicht entfernt werden: \(error.localizedDescription)"
            draftPendingAssignment = nil
            onDraftsChanged()
        }
    }

    private func clearAssignmentAction(for draft: MobilePhotoDraft) -> (() -> Void)? {
        guard draft.linkedTaskId?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty == false else {
            return nil
        }

        return {
            clearAssignedTask(for: draft)
        }
    }

    private func draftTitle(for draft: MobilePhotoDraft) -> String {
        if let originalFilename = draft.originalFilename?.trimmingCharacters(in: .whitespacesAndNewlines), !originalFilename.isEmpty {
            return originalFilename
        }
        return "Freier Foto-Entwurf"
    }

    private func draftMetadata(for draft: MobilePhotoDraft) -> String {
        [
            sourceText(for: draft.source),
            statusText(for: draft.status),
            draft.updatedAt.formatted(date: .abbreviated, time: .shortened)
        ].joined(separator: " · ")
    }

    private func sourceText(for source: MobilePhotoDraftSource) -> String {
        switch source {
        case .iPadCamera:
            return "iPad-Kamera"
        case .photoLibrary:
            return "Fotoauswahl"
        case .iPhoneImport:
            return "iPhone-Import"
        case .shareExtension:
            return "ShareExtension"
        case .fileImport:
            return "Dateiimport"
        }
    }

    private func statusText(for status: MobilePhotoDraftStatus) -> String {
        switch status {
        case .draft:
            return "Entwurf"
        case .assigned:
            return "Zugeordnet"
        case .pendingUpload:
            return "Upload vorbereitet"
        case .uploaded:
            return "Übertragen"
        case .failed:
            return "Fehler"
        case .conflict:
            return "Konflikt"
        }
    }

    private func task(for taskID: String) -> SnapshotTask? {
        availableTasks.first { $0.id == taskID }
    }

    private func taskDisplayTitle(_ task: SnapshotTask) -> String {
        task.displayPrimaryTitle
    }

    private func taskDisplayDetail(_ task: SnapshotTask) -> String? {
        SnapshotDisplayFormatter.joinedMetadata([
            task.displaySecondaryTitle,
            task.displayStatus,
            task.displayCreatedAt.map { "Erstellt \($0)" },
            task.id
        ])
    }

}

private struct MobilePhotoDraftTaskPickerView: View {
    let draftTitle: String
    let tasks: [SnapshotTask]
    let selectedTaskID: String?
    let onSelect: (SnapshotTask) -> Void
    let onClear: (() -> Void)?
    let onDismiss: () -> Void

    @State private var searchText = ""

    private var filteredTasks: [SnapshotTask] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else {
            return tasks
        }
        return tasks.filter { $0.searchableText.localizedCaseInsensitiveContains(query) }
    }

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Text(draftTitle)
                        .font(.headline)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("Die Auswahl wird nur lokal im Foto-Entwurf gespeichert.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                Section {
                    if tasks.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            Label("Keine Snapshot-Aufträge", systemImage: "tray")
                                .font(.headline)
                            Text("Lade oder importiere zuerst einen Snapshot mit Aufträgen. Der Foto-Entwurf bleibt unverändert lokal gespeichert.")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        .padding(.vertical, 8)
                    } else if filteredTasks.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            Label("Keine Treffer", systemImage: "magnifyingglass")
                                .font(.headline)
                            Text("Für diese Suche wurde kein Auftrag im lokalen Snapshot gefunden.")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        .padding(.vertical, 8)
                    } else {
                        ForEach(filteredTasks) { task in
                            Button {
                                onSelect(task)
                            } label: {
                                HStack(alignment: .top, spacing: 10) {
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text(task.displayPrimaryTitle)
                                            .font(.headline)
                                            .foregroundStyle(.primary)
                                            .fixedSize(horizontal: false, vertical: true)
                                        if let detail = taskDetail(task) {
                                            Text(detail)
                                                .font(.caption)
                                                .foregroundStyle(.secondary)
                                                .fixedSize(horizontal: false, vertical: true)
                                        }
                                    }

                                    Spacer(minLength: 8)

                                    if task.id == selectedTaskID {
                                        Image(systemName: "checkmark")
                                            .foregroundStyle(.tint)
                                            .accessibilityLabel("Aktuell zugeordnet")
                                    }
                                }
                                .contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                        }
                    }
                } header: {
                    Text("Auftrag auswählen")
                }

                if let onClear {
                    Section {
                        Button(role: .destructive, action: onClear) {
                            Label("Zuordnung entfernen", systemImage: "link.badge.minus")
                        }
                    }
                }
            }
            .navigationTitle("Auftrag zuordnen")
            .searchable(text: $searchText, prompt: "Aufträge suchen")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Schließen", action: onDismiss)
                }
            }
        }
    }

    private func taskDetail(_ task: SnapshotTask) -> String? {
        SnapshotDisplayFormatter.joinedMetadata([
            task.displaySecondaryTitle,
            task.displayStatus,
            task.displayCategoryNames.first,
            task.displayCreatedAt.map { "Erstellt \($0)" },
            task.id
        ])
    }
}
