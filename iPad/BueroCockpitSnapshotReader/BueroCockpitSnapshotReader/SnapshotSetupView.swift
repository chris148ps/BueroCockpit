import SwiftUI

struct SnapshotSetupView: View {
    let currentProvider: SyncProviderType
    let savedGoogleDriveLink: String
    let hasLocalSnapshot: Bool
    let hasICloudSnapshotSource: Bool
    let iCloudLastUpdatedText: String?
    let message: String?
    let statusMessage: String?
    let isWorking: Bool
    let onSelectSnapshot: () -> Void
    let onSelectICloudSnapshot: () -> Void
    let onRefreshICloudSnapshot: () -> Void
    let onReload: () -> Void
    let onTestGoogleDrive: (String) -> Void
    let onDismiss: (() -> Void)?

    @State private var selectedProvider: SyncProviderType
    @State private var googleDriveLink: String

    init(
        currentProvider: SyncProviderType,
        savedGoogleDriveLink: String,
        hasLocalSnapshot: Bool,
        hasICloudSnapshotSource: Bool,
        iCloudLastUpdatedText: String?,
        message: String?,
        statusMessage: String?,
        isWorking: Bool,
        onSelectSnapshot: @escaping () -> Void,
        onSelectICloudSnapshot: @escaping () -> Void,
        onRefreshICloudSnapshot: @escaping () -> Void,
        onReload: @escaping () -> Void,
        onTestGoogleDrive: @escaping (String) -> Void,
        onDismiss: (() -> Void)? = nil
    ) {
        self.currentProvider = currentProvider
        self.savedGoogleDriveLink = savedGoogleDriveLink
        self.hasLocalSnapshot = hasLocalSnapshot
        self.hasICloudSnapshotSource = hasICloudSnapshotSource
        self.iCloudLastUpdatedText = iCloudLastUpdatedText
        self.message = message
        self.statusMessage = statusMessage
        self.isWorking = isWorking
        self.onSelectSnapshot = onSelectSnapshot
        self.onSelectICloudSnapshot = onSelectICloudSnapshot
        self.onRefreshICloudSnapshot = onRefreshICloudSnapshot
        self.onReload = onReload
        self.onTestGoogleDrive = onTestGoogleDrive
        self.onDismiss = onDismiss
        _selectedProvider = State(initialValue: currentProvider)
        _googleDriveLink = State(initialValue: savedGoogleDriveLink)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 24) {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Sync-Quelle auswählen")
                            .font(.largeTitle.bold())
                        Text("Wählen Sie aus, wie BüroCockpit die nur lesbare lokale Kopie current.bclive erhält.")
                            .font(.title3)
                            .foregroundStyle(.secondary)
                    }

                    LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 12)], spacing: 12) {
                        ForEach(SyncProviderType.allCases) { provider in
                            providerCard(provider)
                        }
                    }

                    GroupBox {
                        assistantContent
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Einrichtungsassistent", systemImage: "list.number")
                            .font(.headline)
                    }

                    if let message, !message.isEmpty {
                        Label(message, systemImage: "exclamationmark.triangle")
                            .font(.callout)
                            .foregroundStyle(.orange)
                    }

                    if let statusMessage, !statusMessage.isEmpty {
                        Label(statusMessage, systemImage: statusMessage.contains("fehlgeschlagen") ? "exclamationmark.triangle" : "checkmark.circle")
                            .font(.callout)
                            .foregroundStyle(statusMessage.contains("fehlgeschlagen") ? .orange : .green)
                    }
                }
                .padding(32)
                .frame(maxWidth: 900)
                .frame(maxWidth: .infinity)
            }
            .navigationTitle("Einrichtung")
            .toolbar {
                if let onDismiss {
                    ToolbarItem(placement: .confirmationAction) {
                        Button("Fertig", action: onDismiss)
                    }
                }
            }
        }
    }

    private func providerCard(_ provider: SyncProviderType) -> some View {
        Button {
            guard provider.isAvailable else { return }
            selectedProvider = provider
        } label: {
            HStack(spacing: 14) {
                Image(systemName: provider.systemImage)
                    .font(.title2)
                    .frame(width: 32)
                VStack(alignment: .leading, spacing: 4) {
                    Text(provider.displayName)
                        .font(.headline)
                    Text(provider.isAvailable ? "Jetzt einrichten" : "Demnächst verfügbar")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                if selectedProvider == provider {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundStyle(.blue)
                }
            }
            .padding(16)
            .frame(maxWidth: .infinity, minHeight: 78)
            .background(
                RoundedRectangle(cornerRadius: 12)
                    .fill(selectedProvider == provider ? Color.blue.opacity(0.12) : Color.secondary.opacity(0.08))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(selectedProvider == provider ? Color.blue : Color.clear, lineWidth: 1.5)
            )
        }
        .buttonStyle(.plain)
        .disabled(!provider.isAvailable)
        .opacity(provider.isAvailable ? 1 : 0.65)
    }

    @ViewBuilder
    private var assistantContent: some View {
        switch selectedProvider {
        case .manualFile:
            VStack(alignment: .leading, spacing: 16) {
                Text("1. Wählen Sie eine live.bclive-Datei aus. Die App kopiert sie lokal und verwendet diese lokale Kopie.")
                Button("Live-Datei importieren", action: onSelectSnapshot)
                    .buttonStyle(.borderedProminent)
                    .disabled(isWorking)
                Text("2. Nach erfolgreicher ZIP-Prüfung wird current.bclive ersetzt und lokal geladen.")
                    .foregroundStyle(.secondary)
                Button("Daten neu laden", action: onReload)
                    .buttonStyle(.bordered)
                    .disabled(isWorking)
                if !hasLocalSnapshot {
                    Text("Bitte live.bclive einmal importieren.")
                        .foregroundStyle(.secondary)
                }
            }
        case .iCloudDrive:
            VStack(alignment: .leading, spacing: 16) {
                Text("iCloud Drive")
                    .font(.headline)
                Text("Wählen Sie die Datei live.bclive aus iCloud Drive aus. Die App merkt sich die Datei und kann sie später erneut einlesen.")
                Button("iCloud-Datei auswählen", action: onSelectICloudSnapshot)
                    .buttonStyle(.borderedProminent)
                    .disabled(isWorking)
                Button("Aus iCloud Drive aktualisieren", action: onRefreshICloudSnapshot)
                    .buttonStyle(.bordered)
                    .disabled(isWorking)
                if hasICloudSnapshotSource {
                    Text("iCloud-Datei eingerichtet")
                        .foregroundStyle(.secondary)
                    if let iCloudLastUpdatedText {
                        Text(iCloudLastUpdatedText)
                            .foregroundStyle(.secondary)
                    }
                } else {
                    Text("Bitte zuerst live.bclive aus iCloud Drive auswählen.")
                        .foregroundStyle(.secondary)
                }
            }
        case .googleDriveDirect:
            VStack(alignment: .leading, spacing: 16) {
                Text("Für die interne Nutzung kann eine freigegebene Google-Drive-Datei geladen werden. Später soll für Verkaufsversionen OneDrive/Microsoft Graph verwendet werden.")
                Text("1. Fügen Sie den normalen Freigabelink der live.bclive-Datei ein.")
                    .foregroundStyle(.secondary)
                TextField("https://drive.google.com/file/d/…/view", text: $googleDriveLink)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .textFieldStyle(.roundedBorder)
                Text("2. Die Verbindung wird einmal getestet. Erst eine vollständig geprüfte ZIP-Datei ersetzt die lokale current.bclive.")
                    .foregroundStyle(.secondary)
                Button(isWorking ? "Verbindung wird getestet …" : "Verbindung testen") {
                    onTestGoogleDrive(googleDriveLink)
                }
                .buttonStyle(.borderedProminent)
                .disabled(isWorking || googleDriveLink.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        case .webDavNas, .dropbox, .microsoftGraphOneDrive:
            Label("Diese Sync-Quelle ist vorbereitet, aber noch nicht eingerichtet.", systemImage: "clock")
                .foregroundStyle(.secondary)
        }
    }
}
