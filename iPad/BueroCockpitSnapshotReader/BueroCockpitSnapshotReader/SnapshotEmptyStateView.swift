import SwiftUI

struct SnapshotEmptyStateView: View {
    let title: String
    let message: String
    let systemImage: String
    let primaryButtonTitle: String?
    let primaryAction: (() -> Void)?
    let secondaryButtonTitle: String?
    let secondaryAction: (() -> Void)?
    let tertiaryButtonTitle: String?
    let tertiaryAction: (() -> Void)?

    init(
        title: String,
        message: String,
        systemImage: String,
        primaryButtonTitle: String? = nil,
        primaryAction: (() -> Void)? = nil,
        secondaryButtonTitle: String? = nil,
        secondaryAction: (() -> Void)? = nil,
        tertiaryButtonTitle: String? = nil,
        tertiaryAction: (() -> Void)? = nil
    ) {
        self.title = title
        self.message = message
        self.systemImage = systemImage
        self.primaryButtonTitle = primaryButtonTitle
        self.primaryAction = primaryAction
        self.secondaryButtonTitle = secondaryButtonTitle
        self.secondaryAction = secondaryAction
        self.tertiaryButtonTitle = tertiaryButtonTitle
        self.tertiaryAction = tertiaryAction
    }

    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: systemImage)
                .font(.system(size: 44, weight: .light))
                .foregroundStyle(.secondary)
            Text(title)
                .font(.title2.bold())
            Text(message)
                .font(.body)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 360)
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
