import SwiftUI

struct SnapshotSetupView: View {
    let currentProvider: SyncProviderType
    let savedGoogleDriveLink: String
    let hasLocalSnapshot: Bool
    let hasICloudSnapshotSource: Bool
    let lastUpdatedText: String?
    let message: String?
    let statusMessage: String?
    let localNetworkDesktopAutoCheckMessage: String?
    let discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop]
    let localNetworkDesktopAddress: String
    let localNetworkDesktopLastSuccessfulCheckText: String?
    let localNetworkDesktopStoredStatus: String?
    let mobileInboxFolderPath: String?
    let mobileInboxMessage: String?
    let isWorking: Bool
    let onSelectSnapshot: () -> Void
    let onSelectICloudSnapshot: () -> Void
    let onRefreshICloudSnapshot: () -> Void
    let onReload: () -> Void
    let onTestGoogleDrive: (String) -> Void
    let onTestLocalNetworkDesktopService: (String) -> Void
    let onUseLocalNetworkDesktop: (String) -> Void
    let onStartLocalNetworkDesktopAutoCheck: (String) -> Void
    let onStopLocalNetworkDesktopAutoCheck: () -> Void
    let onStartLocalNetworkDesktopDiscovery: () -> Void
    let onStopLocalNetworkDesktopDiscovery: () -> Void
    let onUseDiscoveredLocalNetworkDesktop: (LocalNetworkDiscoveredDesktop) -> Void
    let onLocalNetworkDesktopAddressChanged: (String) -> Void
    let onSelectMobileInboxFolder: () -> Void
    let onDismiss: (() -> Void)?

    @State private var selectedProvider: SyncProviderType
    @State private var googleDriveLink: String
    @State private var localNetworkDesktopAddressInput: String
    @State private var isLocalNetworkSyncVisible = false

    init(
        currentProvider: SyncProviderType,
        savedGoogleDriveLink: String,
        hasLocalSnapshot: Bool,
        hasICloudSnapshotSource: Bool,
        lastUpdatedText: String?,
        message: String?,
        statusMessage: String?,
        localNetworkDesktopAutoCheckMessage: String?,
        discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop],
        localNetworkDesktopAddress: String,
        localNetworkDesktopLastSuccessfulCheckText: String?,
        localNetworkDesktopStoredStatus: String?,
        mobileInboxFolderPath: String?,
        mobileInboxMessage: String?,
        isWorking: Bool,
        onSelectSnapshot: @escaping () -> Void,
        onSelectICloudSnapshot: @escaping () -> Void,
        onRefreshICloudSnapshot: @escaping () -> Void,
        onReload: @escaping () -> Void,
        onTestGoogleDrive: @escaping (String) -> Void,
        onTestLocalNetworkDesktopService: @escaping (String) -> Void,
        onUseLocalNetworkDesktop: @escaping (String) -> Void,
        onStartLocalNetworkDesktopAutoCheck: @escaping (String) -> Void,
        onStopLocalNetworkDesktopAutoCheck: @escaping () -> Void,
        onStartLocalNetworkDesktopDiscovery: @escaping () -> Void,
        onStopLocalNetworkDesktopDiscovery: @escaping () -> Void,
        onUseDiscoveredLocalNetworkDesktop: @escaping (LocalNetworkDiscoveredDesktop) -> Void,
        onLocalNetworkDesktopAddressChanged: @escaping (String) -> Void,
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
        self.localNetworkDesktopAutoCheckMessage = localNetworkDesktopAutoCheckMessage
        self.discoveredLocalNetworkDesktops = discoveredLocalNetworkDesktops
        self.localNetworkDesktopAddress = localNetworkDesktopAddress
        self.localNetworkDesktopLastSuccessfulCheckText = localNetworkDesktopLastSuccessfulCheckText
        self.localNetworkDesktopStoredStatus = localNetworkDesktopStoredStatus
        self.mobileInboxFolderPath = mobileInboxFolderPath
        self.mobileInboxMessage = mobileInboxMessage
        self.isWorking = isWorking
        self.onSelectSnapshot = onSelectSnapshot
        self.onSelectICloudSnapshot = onSelectICloudSnapshot
        self.onRefreshICloudSnapshot = onRefreshICloudSnapshot
        self.onReload = onReload
        self.onTestGoogleDrive = onTestGoogleDrive
        self.onTestLocalNetworkDesktopService = onTestLocalNetworkDesktopService
        self.onUseLocalNetworkDesktop = onUseLocalNetworkDesktop
        self.onStartLocalNetworkDesktopAutoCheck = onStartLocalNetworkDesktopAutoCheck
        self.onStopLocalNetworkDesktopAutoCheck = onStopLocalNetworkDesktopAutoCheck
        self.onStartLocalNetworkDesktopDiscovery = onStartLocalNetworkDesktopDiscovery
        self.onStopLocalNetworkDesktopDiscovery = onStopLocalNetworkDesktopDiscovery
        self.onUseDiscoveredLocalNetworkDesktop = onUseDiscoveredLocalNetworkDesktop
        self.onLocalNetworkDesktopAddressChanged = onLocalNetworkDesktopAddressChanged
        self.onSelectMobileInboxFolder = onSelectMobileInboxFolder
        self.onDismiss = onDismiss
        _selectedProvider = State(initialValue: currentProvider)
        _googleDriveLink = State(initialValue: savedGoogleDriveLink)
        _localNetworkDesktopAddressInput = State(initialValue: localNetworkDesktopAddress)
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
            .onChange(of: localNetworkDesktopAddress) { _, address in
                localNetworkDesktopAddressInput = address
            }
            .onChange(of: localNetworkDesktopAddressInput) { _, address in
                onLocalNetworkDesktopAddressChanged(address)
                guard isLocalNetworkSyncVisible else { return }
                onStartLocalNetworkDesktopAutoCheck(address)
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
            Label("Status: \(localNetworkDesktopStatusText)", systemImage: localNetworkDesktopStatusIcon)
                .font(.headline)
                .foregroundStyle(localNetworkDesktopStatusColor)

            Text("Die Prüfung nutzt nur den lokalen Desktop-Testdienst. Es werden keine Aufgaben, Kategorien oder Anhänge übertragen.")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 6) {
                Text("1. BüroCockpit am Desktop öffnen.")
                Text("2. Lokalen Testdienst am Desktop starten.")
                Text("3. iPad sucht/prüft den Desktop im lokalen Netzwerk.")
                Text("4. Später diesen Desktop als lokalen Sync-Partner verwenden.")
            }
            .font(.callout)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                TextField("Desktop-Adresse, z. B. 192.168.1.20 oder mac-mini.local", text: $localNetworkDesktopAddressInput)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .textFieldStyle(.roundedBorder)

                Text("Port: 53941")
                    .font(.callout)
                    .foregroundStyle(.secondary)

                Button("Desktop-Testdienst prüfen") {
                    onTestLocalNetworkDesktopService(localNetworkDesktopAddressInput)
                }
                .buttonStyle(.bordered)
                .disabled(isWorking)

                if canUseLocalNetworkDesktop {
                    Button("Diesen Desktop verwenden") {
                        onUseLocalNetworkDesktop(localNetworkDesktopAddressInput)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(isWorking)
                }

                if let localNetworkDesktopLastSuccessfulCheckText {
                    Label("Letzte erfolgreiche Prüfung: \(localNetworkDesktopLastSuccessfulCheckText)", systemImage: "checkmark.circle")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                if let localNetworkDesktopStoredStatus, !localNetworkDesktopStoredStatus.isEmpty {
                    Label(localNetworkDesktopStoredStatus, systemImage: "desktopcomputer")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                if let localNetworkDesktopAutoCheckMessage, !localNetworkDesktopAutoCheckMessage.isEmpty {
                    Label(localNetworkDesktopAutoCheckMessage, systemImage: "clock.arrow.circlepath")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                if discoveredLocalNetworkDesktops.isEmpty {
                    Label(localNetworkDesktopDiscoveryEmptyText, systemImage: "dot.radiowaves.left.and.right")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                } else {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Gefundene Desktops")
                            .font(.callout.weight(.semibold))
                        ForEach(discoveredLocalNetworkDesktops) { desktop in
                            Button {
                                localNetworkDesktopAddressInput = desktop.address
                                onUseDiscoveredLocalNetworkDesktop(desktop)
                            } label: {
                                HStack {
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(desktop.name)
                                            .font(.callout.weight(.medium))
                                        Text(desktop.displayEndpoint)
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                    }
                                    Spacer()
                                    Image(systemName: "arrow.down.circle")
                                }
                            }
                            .buttonStyle(.bordered)
                        }
                    }
                    .fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .onAppear {
            isLocalNetworkSyncVisible = true
            onStartLocalNetworkDesktopAutoCheck(localNetworkDesktopAddressInput)
            onStartLocalNetworkDesktopDiscovery()
        }
        .onDisappear {
            isLocalNetworkSyncVisible = false
            onStopLocalNetworkDesktopAutoCheck()
            onStopLocalNetworkDesktopDiscovery()
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

    private var canUseLocalNetworkDesktop: Bool {
        if localNetworkDesktopStatusText == "Desktop-Testdienst erreichbar" {
            return true
        }

        let trimmedStatus = statusMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmedStatus.isEmpty &&
            localNetworkDesktopLastSuccessfulCheckText != nil &&
            localNetworkDesktopStoredStatus != "lokaler Desktop vorgemerkt"
    }

    private var localNetworkDesktopDiscoveryEmptyText: String {
        if localNetworkDesktopStoredStatus == "lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStoredStatus == "Lokaler Desktop vorgemerkt" ||
            localNetworkDesktopLastSuccessfulCheckText != nil {
            return "Automatische Suche hat keinen weiteren Desktop gefunden"
        }
        return "Bonjour-Suche hat keinen BüroCockpit-Desktop gefunden; manuelle Adresse kann verwendet werden"
    }

    private var localNetworkDesktopStatusText: String {
        let trimmedStatus = statusMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if trimmedStatus.hasPrefix("Desktop-Testdienst") ||
            trimmedStatus == "Desktop im lokalen Netzwerk gefunden" ||
            trimmedStatus == "Prüfung läuft …" ||
            trimmedStatus == "Bitte Desktop-Adresse oder IP eintragen." ||
            trimmedStatus == "Lokaler Desktop vorgemerkt" ||
            trimmedStatus == "Bereit zur Prüfung" {
            return trimmedStatus
        }
        if localNetworkDesktopAddressInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return "Desktop-Adresse fehlt"
        }
        if localNetworkDesktopStoredStatus == "lokaler Desktop vorgemerkt" {
            return "lokaler Desktop vorgemerkt"
        }
        return "Bereit zur Prüfung"
    }

    private var localNetworkDesktopStatusIcon: String {
        if localNetworkDesktopStatusText == "Desktop-Testdienst erreichbar" {
            return "checkmark.circle"
        }
        if localNetworkDesktopStatusText == "Desktop im lokalen Netzwerk gefunden" {
            return "dot.radiowaves.left.and.right"
        }
        if localNetworkDesktopStatusText == "lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStatusText == "Lokaler Desktop vorgemerkt" {
            return "desktopcomputer"
        }
        if localNetworkDesktopStatusText == "Prüfung läuft …" {
            return "clock.arrow.circlepath"
        }
        return "network"
    }

    private var localNetworkDesktopStatusColor: Color {
        if localNetworkDesktopStatusText == "Desktop-Testdienst erreichbar" ||
            localNetworkDesktopStatusText == "Desktop im lokalen Netzwerk gefunden" ||
            localNetworkDesktopStatusText == "lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStatusText == "Lokaler Desktop vorgemerkt" {
            return .green
        }
        if localNetworkDesktopStatusText.hasPrefix("Desktop-Testdienst nicht erreichbar") ||
            localNetworkDesktopStatusText == "Desktop-Adresse fehlt" ||
            localNetworkDesktopStatusText == "Bitte Desktop-Adresse oder IP eintragen." {
            return .orange
        }
        return .secondary
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
