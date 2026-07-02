import SwiftUI

struct SnapshotSetupView: View {
    let currentProvider: SyncProviderType
    let savedGoogleDriveLink: String
    let hasLocalSnapshot: Bool
    let hasICloudSnapshotSource: Bool
    let lastUpdatedText: String?
    let message: String?
    let statusMessage: String?
    let localNetworkPairingCode: String
    let isLocalNetworkPairingPrepared: Bool
    let mobileInboxFolderPath: String?
    let mobileInboxMessage: String?
    let isWorking: Bool
    let onSelectSnapshot: () -> Void
    let onSelectICloudSnapshot: () -> Void
    let onRefreshICloudSnapshot: () -> Void
    let onReload: () -> Void
    let onTestGoogleDrive: (String) -> Void
    let onPrepareLocalNetworkPairing: (String) -> Void
    let onSelectMobileInboxFolder: () -> Void
    let onDismiss: (() -> Void)?

    @State private var selectedProvider: SyncProviderType
    @State private var googleDriveLink: String
    @State private var localNetworkPairingCodeInput: String

    init(
        currentProvider: SyncProviderType,
        savedGoogleDriveLink: String,
        hasLocalSnapshot: Bool,
        hasICloudSnapshotSource: Bool,
        lastUpdatedText: String?,
        message: String?,
        statusMessage: String?,
        localNetworkPairingCode: String,
        isLocalNetworkPairingPrepared: Bool,
        mobileInboxFolderPath: String?,
        mobileInboxMessage: String?,
        isWorking: Bool,
        onSelectSnapshot: @escaping () -> Void,
        onSelectICloudSnapshot: @escaping () -> Void,
        onRefreshICloudSnapshot: @escaping () -> Void,
        onReload: @escaping () -> Void,
        onTestGoogleDrive: @escaping (String) -> Void,
        onPrepareLocalNetworkPairing: @escaping (String) -> Void,
        onSelectMobileInboxFolder: @escaping () -> Void,
        onDismiss: (() -> Void)? = nil
    ) {
        self.currentProvider = currentProvider
        self.savedGoogleDriveLink = savedGoogleDriveLink
        self.hasLocalSnapshot = hasLocalSnapshot
        self.hasICloudSnapshotSource = hasICloudSnapshotSource
        self.lastUpdatedText = lastUpdatedText
        self.message = message
        self.statusMessage = statusMessage
        self.localNetworkPairingCode = localNetworkPairingCode
        self.isLocalNetworkPairingPrepared = isLocalNetworkPairingPrepared
        self.mobileInboxFolderPath = mobileInboxFolderPath
        self.mobileInboxMessage = mobileInboxMessage
        self.isWorking = isWorking
        self.onSelectSnapshot = onSelectSnapshot
        self.onSelectICloudSnapshot = onSelectICloudSnapshot
        self.onRefreshICloudSnapshot = onRefreshICloudSnapshot
        self.onReload = onReload
        self.onTestGoogleDrive = onTestGoogleDrive
        self.onPrepareLocalNetworkPairing = onPrepareLocalNetworkPairing
        self.onSelectMobileInboxFolder = onSelectMobileInboxFolder
        self.onDismiss = onDismiss
        _selectedProvider = State(initialValue: currentProvider)
        _googleDriveLink = State(initialValue: savedGoogleDriveLink)
        _localNetworkPairingCodeInput = State(initialValue: localNetworkPairingCode)
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
                        VStack(alignment: .leading, spacing: 10) {
                            Label("Aktive Sync-Quelle: \(currentProvider.displayName)", systemImage: currentProvider.systemImage)
                                .font(.headline)

                            if let lastUpdatedText {
                                Text(lastUpdatedText)
                                    .foregroundStyle(.secondary)
                            } else {
                                Text("Zuletzt aktualisiert: noch nicht verfügbar")
                                    .foregroundStyle(.secondary)
                            }

                            if let statusMessage, !statusMessage.isEmpty {
                                Text("Letzter Status: \(statusMessage)")
                                    .foregroundStyle(.secondary)
                                    .fixedSize(horizontal: false, vertical: true)
                            } else {
                                Text("Letzter Status: noch nicht verfügbar")
                                    .foregroundStyle(.secondary)
                            }
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Aktueller Sync", systemImage: "arrow.triangle.2.circlepath")
                            .font(.headline)
                    }

                    GroupBox {
                        assistantContent
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Einrichtungsassistent", systemImage: "list.number")
                            .font(.headline)
                    }

                    GroupBox {
                        localNetworkSyncContent
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Lokaler Netzwerk-Sync", systemImage: "network")
                            .font(.headline)
                    }

                    GroupBox {
                        mobileInboxContent
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Mobile Eingänge", systemImage: "tray.and.arrow.down")
                            .font(.headline)
                    }

                    if let message, !message.isEmpty {
                        Label(message, systemImage: "exclamationmark.triangle")
                            .font(.callout)
                            .foregroundStyle(.orange)
                    }

                }
                .padding(32)
                .frame(maxWidth: 900)
                .frame(maxWidth: .infinity)
            }
            .navigationTitle("Einrichtung")
            .onChange(of: currentProvider) { _, provider in
                selectedProvider = provider
            }
            .onChange(of: savedGoogleDriveLink) { _, link in
                googleDriveLink = link
            }
            .onChange(of: localNetworkPairingCode) { _, code in
                localNetworkPairingCodeInput = code
            }
            .toolbar {
                if let onDismiss {
                    ToolbarItem(placement: .confirmationAction) {
                        Button("Fertig", action: onDismiss)
                    }
                }
            }
        }
    }

    private var localNetworkSyncContent: some View {
        VStack(alignment: .leading, spacing: 16) {
            Label(
                isLocalNetworkPairingPrepared ? "Status: Kopplung vorbereitet" : "Status: Nicht gekoppelt",
                systemImage: isLocalNetworkPairingPrepared ? "checkmark.circle" : "circle"
            )
                .font(.headline)
                .foregroundStyle(isLocalNetworkPairingPrepared ? .green : .secondary)

            Text("Die Verbindung wird erst in einem späteren Schritt aktiviert.")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 6) {
                Text("1. Pairing-Code vom BüroCockpit-Desktop ablesen.")
                Text("2. Code hier eingeben.")
                Text("3. \"Kopplung vorbereiten\" drücken.")
                Text("4. Später erkennt dieses iPad den Desktop automatisch wieder.")
                Text("5. Die echte Verbindung wird erst in einem späteren Schritt aktiviert.")
            }
            .font(.callout)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)

            TextField("ABCD-1234", text: $localNetworkPairingCodeInput)
                .textInputAutocapitalization(.characters)
                .autocorrectionDisabled()
                .textFieldStyle(.roundedBorder)

            Button("Kopplung vorbereiten") {
                onPrepareLocalNetworkPairing(localNetworkPairingCodeInput)
            }
            .buttonStyle(.bordered)
            .disabled(localNetworkPairingCodeInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
    }

    private var mobileInboxContent: some View {
        VStack(alignment: .leading, spacing: 16) {
            Button("Mobile-Inbox-Ordner wählen", action: onSelectMobileInboxFolder)
                .buttonStyle(.bordered)
                .disabled(isWorking)

            if let mobileInboxFolderPath, !mobileInboxFolderPath.isEmpty {
                Text("Ordner: \(mobileInboxFolderPath)")
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            } else {
                Text("Noch kein Mobile-Inbox-Ordner gewählt.")
                    .foregroundStyle(.secondary)
            }

            if let mobileInboxMessage, !mobileInboxMessage.isEmpty {
                Label(mobileInboxMessage, systemImage: "checkmark.circle")
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
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
                if hasICloudSnapshotSource {
                    Button("Aus iCloud Drive aktualisieren", action: onRefreshICloudSnapshot)
                        .buttonStyle(.bordered)
                        .disabled(isWorking)
                    Text("iCloud-Datei eingerichtet")
                        .foregroundStyle(.secondary)
                    if let lastUpdatedText {
                        Text(lastUpdatedText)
                            .foregroundStyle(.secondary)
                    }
                } else {
                    Text("Bitte zuerst iCloud-Datei auswählen.")
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
