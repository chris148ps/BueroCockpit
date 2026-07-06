import SwiftUI
import UIKit
import PhotosUI

struct MobilePhotoDraftsView: View {
    let store: MobilePhotoDraftStore
    let onDraftsChanged: () -> Void
    let onDismiss: () -> Void

    @State private var collection = MobilePhotoDraftCollection()
    @State private var statusMessage: String?
    @State private var selectedPhotoItem: PhotosPickerItem?
    @State private var isImportingPhoto = false

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
                        ForEach(collection.drafts) { draft in
                            draftRow(draft)
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
        }
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
            }
        }
        .padding(.vertical, 4)
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

    private func draftTitle(for draft: MobilePhotoDraft) -> String {
        if let linkedTaskId = draft.linkedTaskId?.trimmingCharacters(in: .whitespacesAndNewlines), !linkedTaskId.isEmpty {
            return "Entwurf für Auftrag \(linkedTaskId)"
        }
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

}
