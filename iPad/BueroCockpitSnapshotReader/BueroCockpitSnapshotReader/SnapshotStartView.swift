import SwiftUI

struct SnapshotStartView: View {
    let statusTitle: String
    let statusMessage: String?
    let primaryButtonTitle: String
    let primaryAction: () -> Void
    let secondaryButtonTitle: String
    let secondaryAction: () -> Void
    let tertiaryButtonTitle: String?
    let tertiaryAction: (() -> Void)?

    var body: some View {
        NavigationStack {
            ZStack {
                LinearGradient(
                    colors: [
                        Color(red: 0.95, green: 0.97, blue: 1.0),
                        Color(red: 0.90, green: 0.94, blue: 0.99),
                        Color(red: 0.97, green: 0.98, blue: 1.0)
                    ],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                )
                .ignoresSafeArea()

                ScrollView {
                    VStack(spacing: 18) {
                        Image(systemName: "tray.full")
                            .font(.system(size: 44, weight: .medium))
                            .foregroundStyle(.blue)
                            .padding(.bottom, 6)

                        Text("BüroCockpit iPad")
                            .font(.largeTitle.bold())

                        Text("Lokaler Netzwerk-Sync in Vorbereitung")
                            .font(.title2.weight(.semibold))
                            .multilineTextAlignment(.center)

                        if let statusMessage, !statusMessage.isEmpty {
                            Text(statusMessage)
                                .font(.footnote)
                                .foregroundStyle(.secondary)
                                .multilineTextAlignment(.leading)
                                .frame(maxWidth: 560, alignment: .leading)
                                .textSelection(.enabled)
                        }

                        VStack(spacing: 10) {
                            Text("Desktop im lokalen Netzwerk suchen oder IP manuell eingeben.")
                                .font(.callout)
                                .foregroundStyle(.secondary)
                                .multilineTextAlignment(.center)
                                .frame(maxWidth: 540)

                            Text("Noch kein echter Sync aktiv. Der Desktop-Testdienst muss in BüroCockpit manuell gestartet sein.")
                                .font(.footnote)
                                .foregroundStyle(.secondary)
                                .multilineTextAlignment(.center)
                                .frame(maxWidth: 540)

                            VStack(spacing: 10) {
                                Button(primaryButtonTitle, action: primaryAction)
                                    .buttonStyle(.borderedProminent)
                                    .controlSize(.large)

                                Button(secondaryButtonTitle, action: secondaryAction)
                                    .buttonStyle(.bordered)
                                    .controlSize(.large)

                                if let tertiaryButtonTitle, let tertiaryAction {
                                    Button(tertiaryButtonTitle, action: tertiaryAction)
                                        .buttonStyle(.bordered)
                                        .controlSize(.large)
                                }
                            }
                        }
                        .padding(.top, 8)
                    }
                    .padding(32)
                    .frame(maxWidth: 720)
                    .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 28, style: .continuous))
                    .overlay(
                        RoundedRectangle(cornerRadius: 28, style: .continuous)
                            .strokeBorder(Color.white.opacity(0.5), lineWidth: 1)
                    )
                    .shadow(color: Color.black.opacity(0.08), radius: 30, x: 0, y: 16)
                    .padding()
                }
            }
            .navigationTitle("BüroCockpit")
        }
    }
}
