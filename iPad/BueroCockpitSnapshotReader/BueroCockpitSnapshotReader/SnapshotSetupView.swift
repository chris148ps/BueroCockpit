import SwiftUI

enum SnapshotSetupStep: Int, CaseIterable, Identifiable {
    case snapshotLocation

    var id: Int { rawValue }
}

struct SnapshotSetupView: View {
    let message: String?
    let statusMessage: String?
    let onSelectSnapshot: () -> Void
    let onSelectFolder: () -> Void

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 24) {
                    Image(systemName: "ipad.and.arrow.forward")
                        .font(.system(size: 52, weight: .medium))
                        .foregroundStyle(.blue)

                    Text("BüroCockpit einrichten")
                        .font(.largeTitle.bold())

                    Text("Wählen Sie Sync/live.bclive aus. Die Datei wird lokal in die App kopiert und dann gelesen.")
                        .font(.title3)
                        .multilineTextAlignment(.center)

                    Text("Die App liest ausschließlich die lokale Kopie. Für neuere Daten importieren Sie Sync/live.bclive erneut.")
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                        .frame(maxWidth: 560)

                    if let message, !message.isEmpty {
                        Label(message, systemImage: "exclamationmark.triangle")
                            .font(.callout)
                            .foregroundStyle(.orange)
                            .multilineTextAlignment(.leading)
                            .frame(maxWidth: 560, alignment: .leading)
                    }

                    if let statusMessage, !statusMessage.isEmpty {
                        Label(statusMessage, systemImage: "folder")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                    }

                    Button("Live-Datei importieren", action: onSelectSnapshot)
                        .buttonStyle(.borderedProminent)
                        .controlSize(.large)

                    Button("Live-Ordner auswählen", action: onSelectFolder)
                        .buttonStyle(.bordered)
                        .controlSize(.large)
                }
                .padding(40)
                .frame(maxWidth: .infinity)
            }
            .navigationTitle("Einrichtung")
        }
    }
}

struct SnapshotSettingsView: View {
    let fileName: String?
    let snapshotDate: String?
    let categoryCount: Int
    let taskCount: Int
    let statusMessage: String?
    let onRefresh: () -> Void
    let onChooseLocation: () -> Void
    let onChooseFolder: () -> Void
    let onReset: () -> Void
    let onDismiss: () -> Void

    var body: some View {
        NavigationStack {
            Form {
                Section("Snapshot") {
                    valueRow("Datei", fileName)
                    valueRow("Zeitpunkt", snapshotDate)
                    valueRow("Kategorien", String(categoryCount))
                    valueRow("Aufgaben", String(taskCount))

                    if let statusMessage, !statusMessage.isEmpty {
                        Label(statusMessage, systemImage: "info.circle")
                            .foregroundStyle(.orange)
                    }
                }

                Section("Aktionen") {
                    Button("Daten neu laden", action: onRefresh)
                    Button("Live-Datei erneut importieren", action: onChooseLocation)
                    Button("Live-Ordner auswählen", action: onChooseFolder)
                    Button("Einrichtung zurücksetzen", role: .destructive, action: onReset)
                }

                Section("Ausblick") {
                    Text("Diese Einrichtung kann später um Betrieb, Benutzerrolle, Offline-Verhalten und Rückgabeordner erweitert werden.")
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle("Einrichtung")
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Fertig", action: onDismiss)
                }
            }
        }
    }

    @ViewBuilder
    private func valueRow(_ label: String, _ value: String?) -> some View {
        if let value, !value.isEmpty {
            LabeledContent(label, value: value)
        }
    }
}
