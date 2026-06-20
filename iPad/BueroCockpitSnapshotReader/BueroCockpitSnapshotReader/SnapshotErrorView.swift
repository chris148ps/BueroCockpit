import SwiftUI

struct SnapshotErrorView: View {
    let title: String
    let message: String
    let primaryButtonTitle: String?
    let primaryAction: (() -> Void)?
    let secondaryButtonTitle: String?
    let secondaryAction: (() -> Void)?
    let tertiaryButtonTitle: String?
    let tertiaryAction: (() -> Void)?

    init(
        title: String,
        message: String,
        primaryButtonTitle: String? = nil,
        primaryAction: (() -> Void)? = nil,
        secondaryButtonTitle: String? = nil,
        secondaryAction: (() -> Void)? = nil,
        tertiaryButtonTitle: String? = nil,
        tertiaryAction: (() -> Void)? = nil
    ) {
        self.title = title
        self.message = message
        self.primaryButtonTitle = primaryButtonTitle
        self.primaryAction = primaryAction
        self.secondaryButtonTitle = secondaryButtonTitle
        self.secondaryAction = secondaryAction
        self.tertiaryButtonTitle = tertiaryButtonTitle
        self.tertiaryAction = tertiaryAction
    }

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 44, weight: .light))
                .foregroundStyle(.orange)
            Text(title)
                .font(.title2.bold())
            Text(message)
                .font(.body)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 380)
            if let primaryButtonTitle, let primaryAction {
                VStack(spacing: 10) {
                    Button(primaryButtonTitle, action: primaryAction)
                        .buttonStyle(.borderedProminent)

                    if let secondaryButtonTitle, let secondaryAction {
                        Button(secondaryButtonTitle, action: secondaryAction)
                            .buttonStyle(.bordered)
                    }

                    if let tertiaryButtonTitle, let tertiaryAction {
                        Button(tertiaryButtonTitle, action: tertiaryAction)
                            .buttonStyle(.bordered)
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(32)
    }
}
