import SwiftUI

struct SnapshotStartView: View {
    let statusTitle: String
    let statusMessage: String?
    let primaryButtonTitle: String
    let primaryAction: () -> Void
    let secondaryButtonTitle: String
    let secondaryAction: () -> Void

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

                VStack(spacing: 18) {
                    Image(systemName: "tray.full")
                        .font(.system(size: 44, weight: .medium))
                        .foregroundStyle(.blue)
                        .padding(.bottom, 6)

                    Text("BüroCockpit")
                        .font(.largeTitle.bold())

                    Text(statusTitle)
                        .font(.title2.weight(.semibold))
                        .multilineTextAlignment(.center)

                    if let statusMessage, !statusMessage.isEmpty {
                        Text(statusMessage)
                            .font(.body)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                            .frame(maxWidth: 500)
                    }

                    VStack(spacing: 10) {
                        Text("Die App arbeitet nur lesend. Sie zeigt Snapshot-Dateien an, schreibt aber nichts zurück.")
                            .font(.callout)
                            .foregroundStyle(.secondary)
                            .multilineTextAlignment(.center)
                            .frame(maxWidth: 540)

                        Text("Bei OneDrive bitte metadata.json auswählen, falls OneDrive bei der Ordnerauswahl ausgegraut ist.")
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
            .navigationTitle("BüroCockpit")
        }
    }
}
