import SwiftUI

struct SnapshotSetupView: View {
    let hasLocalSnapshot: Bool
    let lastUpdatedText: String?
    let message: String?
    let statusMessage: String?
    let localNetworkDesktopStatusMessage: String?
    let localNetworkDesktopAutoCheckMessage: String?
    let discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop]
    let localNetworkDesktopAddress: String
    let localNetworkDesktopLastSuccessfulCheckText: String?
    let localNetworkDesktopStoredStatus: String?
    let mobileInboxFolderPath: String?
    let mobileInboxMessage: String?
    let isWorking: Bool
    let onSelectSnapshot: () -> Void
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

    @State private var localNetworkDesktopAddressInput: String
    @State private var isLocalNetworkSyncVisible = false

    init(
        hasLocalSnapshot: Bool,
        lastUpdatedText: String?,
        message: String?,
        statusMessage: String?,
        localNetworkDesktopStatusMessage: String?,
        localNetworkDesktopAutoCheckMessage: String?,
        discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop],
        localNetworkDesktopAddress: String,
        localNetworkDesktopLastSuccessfulCheckText: String?,
        localNetworkDesktopStoredStatus: String?,
        mobileInboxFolderPath: String?,
        mobileInboxMessage: String?,
        isWorking: Bool,
        onSelectSnapshot: @escaping () -> Void,
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
        self.hasLocalSnapshot = hasLocalSnapshot
        self.lastUpdatedText = lastUpdatedText
        self.message = message
        self.statusMessage = statusMessage
        self.localNetworkDesktopStatusMessage = localNetworkDesktopStatusMessage
        self.localNetworkDesktopAutoCheckMessage = localNetworkDesktopAutoCheckMessage
        self.discoveredLocalNetworkDesktops = discoveredLocalNetworkDesktops
        self.localNetworkDesktopAddress = localNetworkDesktopAddress
        self.localNetworkDesktopLastSuccessfulCheckText = localNetworkDesktopLastSuccessfulCheckText
        self.localNetworkDesktopStoredStatus = localNetworkDesktopStoredStatus
        self.mobileInboxFolderPath = mobileInboxFolderPath
        self.mobileInboxMessage = mobileInboxMessage
        self.isWorking = isWorking
        self.onSelectSnapshot = onSelectSnapshot
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
        _localNetworkDesktopAddressInput = State(initialValue: localNetworkDesktopAddress)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 24) {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("BüroCockpit iPad")
                            .font(.largeTitle.bold())
                        Text("Lokaler Netzwerk-Sync in Vorbereitung")
                            .font(.title3)
                            .foregroundStyle(.secondary)
                    }

                    GroupBox {
                        localNetworkSyncContent
                        .frame(maxWidth: .infinity, alignment: .leading)
                    } label: {
                        Label("Desktop im lokalen Netzwerk verbinden", systemImage: "network")
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
            .navigationTitle("Sync-Einstellungen")
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

            Text("Desktop im lokalen Netzwerk suchen oder IP manuell eingeben. Noch kein echter Sync aktiv.")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            VStack(alignment: .leading, spacing: 6) {
                Text("1. Desktop-Testdienst in BüroCockpit manuell starten.")
                Text("2. Bonjour findet den Desktop automatisch, manuelle IP bleibt Fallback.")
                Text("3. Desktop prüfen.")
                Text("4. Diesen Desktop verwenden.")
                Text("5. iPad meldet sich am Desktop als vorgemerktes lokales Gerät.")
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

                Button("Desktop prüfen") {
                    onTestLocalNetworkDesktopService(localNetworkDesktopAddressInput)
                }
                .buttonStyle(.bordered)
                .disabled(isWorking)

                if canUseLocalNetworkDesktop {
                    Button(localNetworkDesktopUseButtonTitle) {
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
        if isLocalNetworkDesktopRemembered {
            return true
        }

        let trimmedStatus = localNetworkDesktopStatusMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmedStatus.isEmpty &&
            localNetworkDesktopLastSuccessfulCheckText != nil &&
            !isLocalNetworkDesktopRemembered
    }

    private var localNetworkDesktopUseButtonTitle: String {
        isLocalNetworkDesktopRemembered ? "Desktop ist vorgemerkt" : "Diesen Desktop verwenden"
    }

    private var isLocalNetworkDesktopRemembered: Bool {
        localNetworkDesktopStoredStatus == "lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStoredStatus == "Lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStoredStatus == "Desktop im lokalen Netzwerk gefunden"
    }

    private var localNetworkDesktopDiscoveryEmptyText: String {
        if isLocalNetworkDesktopRemembered || localNetworkDesktopLastSuccessfulCheckText != nil {
            return "Automatische Suche hat aktuell keinen Desktop gefunden."
        }
        return "Desktop im lokalen Netzwerk suchen oder IP manuell eingeben."
    }

    private var localNetworkDesktopStatusText: String {
        let trimmedStatus = localNetworkDesktopStatusMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if isLocalNetworkDesktopRemembered &&
            (trimmedStatus.isEmpty ||
             trimmedStatus.hasPrefix("Desktop-Testdienst nicht erreichbar") ||
             trimmedStatus == "Desktop-Adresse fehlt" ||
             trimmedStatus == "Bereit zur Prüfung") {
            return "Lokaler Desktop vorgemerkt"
        }
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
        if isLocalNetworkDesktopRemembered {
            return "Lokaler Desktop vorgemerkt"
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

}
