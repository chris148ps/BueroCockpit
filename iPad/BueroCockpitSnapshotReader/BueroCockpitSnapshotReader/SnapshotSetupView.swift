import SwiftUI

struct SnapshotSetupView: View {
    let hasLocalSnapshot: Bool
    let lastUpdatedText: String?
    let message: String?
    let statusMessage: String?
    let localNetworkDesktopStatusMessage: String?
    let localNetworkDesktopAutoCheckMessage: String?
    let discoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop]
    let otherDiscoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop]
    let localNetworkDesktopAddress: String
    let localNetworkDesktopPort: Int
    let localNetworkDesktopName: String?
    let localNetworkDesktopLastSuccessfulCheckText: String?
    let localNetworkDesktopStoredStatus: String?
    let pendingMobileChangeCount: Int
    let openMobilePhotoDraftCount: Int
    let mobileInboxFolderPath: String?
    let mobileInboxMessage: String?
    let isWorking: Bool
    let onSelectSnapshot: () -> Void
    let onReload: () -> Void
    let onTestGoogleDrive: (String) -> Void
    let onTestLocalNetworkDesktopService: (String) -> Void
    let onUseLocalNetworkDesktop: (String) -> Void
    let onEnsureLocalNetworkMonitoring: (String) -> Void
    let onStopLocalNetworkMonitoring: () -> Void
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
        otherDiscoveredLocalNetworkDesktops: [LocalNetworkDiscoveredDesktop],
        localNetworkDesktopAddress: String,
        localNetworkDesktopPort: Int,
        localNetworkDesktopName: String?,
        localNetworkDesktopLastSuccessfulCheckText: String?,
        localNetworkDesktopStoredStatus: String?,
        pendingMobileChangeCount: Int,
        openMobilePhotoDraftCount: Int,
        mobileInboxFolderPath: String?,
        mobileInboxMessage: String?,
        isWorking: Bool,
        onSelectSnapshot: @escaping () -> Void,
        onReload: @escaping () -> Void,
        onTestGoogleDrive: @escaping (String) -> Void,
        onTestLocalNetworkDesktopService: @escaping (String) -> Void,
        onUseLocalNetworkDesktop: @escaping (String) -> Void,
        onEnsureLocalNetworkMonitoring: @escaping (String) -> Void,
        onStopLocalNetworkMonitoring: @escaping () -> Void,
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
        self.otherDiscoveredLocalNetworkDesktops = otherDiscoveredLocalNetworkDesktops
        self.localNetworkDesktopAddress = localNetworkDesktopAddress
        self.localNetworkDesktopPort = localNetworkDesktopPort
        self.localNetworkDesktopName = localNetworkDesktopName
        self.localNetworkDesktopLastSuccessfulCheckText = localNetworkDesktopLastSuccessfulCheckText
        self.localNetworkDesktopStoredStatus = localNetworkDesktopStoredStatus
        self.pendingMobileChangeCount = pendingMobileChangeCount
        self.openMobilePhotoDraftCount = openMobilePhotoDraftCount
        self.mobileInboxFolderPath = mobileInboxFolderPath
        self.mobileInboxMessage = mobileInboxMessage
        self.isWorking = isWorking
        self.onSelectSnapshot = onSelectSnapshot
        self.onReload = onReload
        self.onTestGoogleDrive = onTestGoogleDrive
        self.onTestLocalNetworkDesktopService = onTestLocalNetworkDesktopService
        self.onUseLocalNetworkDesktop = onUseLocalNetworkDesktop
        self.onEnsureLocalNetworkMonitoring = onEnsureLocalNetworkMonitoring
        self.onStopLocalNetworkMonitoring = onStopLocalNetworkMonitoring
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
                        Text("Lokaler Netzwerk-Sync")
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
                onEnsureLocalNetworkMonitoring(address)
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

            Label("Mobile Änderungen ausstehend: \(pendingMobileChangeCount)", systemImage: "tray.full")
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            Label("Foto-Entwürfe vorbereitet: \(openMobilePhotoDraftCount)", systemImage: "camera")
                .font(.callout)
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
                VStack(alignment: .leading, spacing: 6) {
                    Text("Aktueller vorgemerkter Desktop")
                        .font(.callout.weight(.semibold))
                    if hasStoredLocalNetworkDesktop {
                        Text(localNetworkDesktopNameText)
                            .font(.callout.weight(.medium))
                        Text("\(localNetworkDesktopAddress):\(localNetworkDesktopPort)")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    } else {
                        Text("Noch kein Desktop vorgemerkt.")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                    }
                }
                .fixedSize(horizontal: false, vertical: true)

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
                } else if otherDiscoveredLocalNetworkDesktops.isEmpty {
                    Label("Kein anderer BüroCockpit-Desktop gefunden.", systemImage: "dot.radiowaves.left.and.right")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                } else {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Anderen BüroCockpit-Desktop gefunden")
                            .font(.callout.weight(.semibold))
                        ForEach(otherDiscoveredLocalNetworkDesktops) { desktop in
                            VStack(alignment: .leading, spacing: 6) {
                                Text(desktop.name)
                                    .font(.callout.weight(.medium))
                                Text(localNetworkDesktopEndpointText(for: desktop))
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                Button("Diesen Desktop verwenden") {
                                    localNetworkDesktopAddressInput = desktop.address
                                    onUseDiscoveredLocalNetworkDesktop(desktop)
                                }
                                .buttonStyle(.borderedProminent)
                                .disabled(isWorking)
                            }
                            .padding(.vertical, 6)
                        }
                    }
                    .fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .onAppear {
            isLocalNetworkSyncVisible = true
            onEnsureLocalNetworkMonitoring(localNetworkDesktopAddressInput)
        }
        .onDisappear {
            isLocalNetworkSyncVisible = false
            onStopLocalNetworkMonitoring()
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
        isLocalNetworkDesktopAddressInputStored ? "Desktop ist vorgemerkt" : "Diesen Desktop verwenden"
    }

    private var isLocalNetworkDesktopRemembered: Bool {
        localNetworkDesktopStoredStatus == "lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStoredStatus == "Lokaler Desktop vorgemerkt" ||
            localNetworkDesktopStoredStatus == "Desktop im lokalen Netzwerk gefunden"
    }

    private var hasStoredLocalNetworkDesktop: Bool {
        !localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private var isLocalNetworkDesktopAddressInputStored: Bool {
        let storedAddress = localNetworkDesktopAddress.trimmingCharacters(in: .whitespacesAndNewlines)
        let inputAddress = localNetworkDesktopAddressInput.trimmingCharacters(in: .whitespacesAndNewlines)
        return isLocalNetworkDesktopRemembered && !storedAddress.isEmpty && storedAddress == inputAddress
    }

    private var localNetworkDesktopNameText: String {
        let trimmedName = localNetworkDesktopName?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmedName.isEmpty ? "BüroCockpit-Desktop" : trimmedName
    }

    private func localNetworkDesktopEndpointText(for desktop: LocalNetworkDiscoveredDesktop) -> String {
        if let hostName = desktop.hostName?.trimmingCharacters(in: .whitespacesAndNewlines),
           !hostName.isEmpty,
           hostName != desktop.address {
            return "\(hostName) / \(desktop.address):\(desktop.port)"
        }
        return desktop.displayEndpoint
    }

    private var localNetworkDesktopDiscoveryEmptyText: String {
        if isLocalNetworkDesktopRemembered || localNetworkDesktopLastSuccessfulCheckText != nil {
            return "Automatische Suche: kein weiterer Desktop gefunden."
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
