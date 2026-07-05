import SwiftUI
import UIKit

struct MobilePhotoDraftsView: View {
    let store: MobilePhotoDraftStore
    let onDraftsChanged: () -> Void
    let onDismiss: () -> Void

    @State private var collection = MobilePhotoDraftCollection()
    @State private var statusMessage: String?

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
                    if collection.drafts.isEmpty {
                        VStack(alignment: .leading, spacing: 8) {
                            Label("Noch keine Foto-Entwürfe", systemImage: "photo.on.rectangle.angled")
                                .font(.headline)
                            Text("Der Foto-Modus speichert aktuell nur lokale Entwürfe. Es wird noch kein Foto aufgenommen, importiert oder übertragen.")
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
        }
    }

    private func draftRow(_ draft: MobilePhotoDraft) -> some View {
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
        .padding(.vertical, 4)
    }

    private func loadDrafts() {
        collection = store.load()
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
