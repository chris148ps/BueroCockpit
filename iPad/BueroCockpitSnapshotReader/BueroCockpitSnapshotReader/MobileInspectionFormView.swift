import PhotosUI
import SwiftUI
import UIKit

struct MobileInspectionFormView: View {
    let categoryNames: [String]
    let writer: MobileInboxWriter
    let onSaved: (MobileInspectionSaveResult) -> Void
    let onNeedsFolderSelection: () -> Void
    let onDismiss: () -> Void

    @State private var draft = MobileInspectionDraft()
    @State private var selectedPhotoItems: [PhotosPickerItem] = []
    @State private var selectedLibraryPhotos: [MobileInspectionPhotoInput] = []
    @State private var cameraPhotos: [MobileInspectionPhotoInput] = []
    @State private var isCameraPresented = false
    @State private var isSaving = false
    @State private var isLoadingPhotos = false
    @State private var errorMessage: String?

    private var effectiveCategoryNames: [String] {
        let snapshotCategories = categoryNames
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        return snapshotCategories.isEmpty
            ? ["Angebot erstellen", "Besichtigung", "Material bestellen", "Rückfrage"]
            : snapshotCategories
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Kunde") {
                    TextField("Kunde / Name", text: $draft.customerName)
                    TextField("Adresse", text: $draft.address, axis: .vertical)
                    TextField("Telefon", text: $draft.phone)
                        .keyboardType(.phonePad)
                    TextField("E-Mail", text: $draft.email)
                        .keyboardType(.emailAddress)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                }

                Section("Aufgabe") {
                    TextField("Titel / Betreff", text: $draft.title)
                    Picker("Kategorie", selection: $draft.category) {
                        ForEach(effectiveCategoryNames, id: \.self) { category in
                            Text(category).tag(category)
                        }
                    }
                    TextField("Notiz", text: $draft.notes, axis: .vertical)
                        .lineLimit(4...8)
                }

                Section("Fotos") {
                    PhotosPicker(
                        selection: $selectedPhotoItems,
                        maxSelectionCount: 30,
                        matching: .images
                    ) {
                        Label("Fotos auswählen", systemImage: "photo.on.rectangle")
                    }

                    if UIImagePickerController.isSourceTypeAvailable(.camera) {
                        Button {
                            isCameraPresented = true
                        } label: {
                            Label("Kameraaufnahme", systemImage: "camera")
                        }
                    }

                    if isLoadingPhotos {
                        ProgressView("Fotos werden vorbereitet ...")
                    } else if selectedLibraryPhotos.isEmpty && cameraPhotos.isEmpty {
                        Text("Noch keine Fotos ausgewählt.")
                            .foregroundStyle(.secondary)
                    } else {
                        Label("\(selectedLibraryPhotos.count + cameraPhotos.count) Foto(s) ausgewählt", systemImage: "checkmark.circle")
                            .foregroundStyle(.secondary)
                    }
                }

                if let errorMessage {
                    Section {
                        Label(errorMessage, systemImage: "exclamationmark.triangle")
                            .foregroundStyle(.orange)
                    }
                }
            }
            .navigationTitle("Neue Besichtigung")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Abbrechen", action: onDismiss)
                        .disabled(isSaving)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isSaving ? "Speichert ..." : "Speichern") {
                        save()
                    }
                    .disabled(isSaving || isLoadingPhotos)
                }
            }
            .onAppear {
                if draft.category.isEmpty {
                    draft.category = effectiveCategoryNames.first ?? ""
                }
            }
            .onChange(of: categoryNames) { _, _ in
                if draft.category.isEmpty || !effectiveCategoryNames.contains(draft.category) {
                    draft.category = effectiveCategoryNames.first ?? ""
                }
            }
            .onChange(of: selectedPhotoItems) { _, items in
                loadPhotosFromPickerItems(items)
            }
            .sheet(isPresented: $isCameraPresented) {
                MobileCameraPicker { image in
                    addCameraImage(image)
                }
            }
        }
    }

    private func save() {
        Task {
            await saveAsync()
        }
    }

    @MainActor
    private func saveAsync() async {
        if selectedPhotoItems.count != selectedLibraryPhotos.count {
            errorMessage = "Die ausgewählten Fotos sind noch nicht vollständig vorbereitet."
            return
        }

        isSaving = true
        errorMessage = nil
        defer {
            isSaving = false
        }

        do {
            draft.photos = selectedLibraryPhotos + cameraPhotos
            let result = try writer.save(draft)
            draft = MobileInspectionDraft(category: effectiveCategoryNames.first ?? "")
            selectedPhotoItems = []
            selectedLibraryPhotos = []
            cameraPhotos = []
            onSaved(result)
        } catch MobileInboxError.folderNotSelected {
            errorMessage = MobileInboxError.folderNotSelected.localizedDescription
            onNeedsFolderSelection()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    private func loadPhotosFromPickerItems(_ items: [PhotosPickerItem]) {
        Task {
            await loadPhotosFromPickerItemsAsync(items)
        }
    }

    @MainActor
    private func loadPhotosFromPickerItemsAsync(_ items: [PhotosPickerItem]) async {
        isLoadingPhotos = true
        errorMessage = nil
        defer {
            isLoadingPhotos = false
        }

        do {
            selectedLibraryPhotos = try await loadSelectedPhotos(items)
        } catch {
            selectedLibraryPhotos = []
            errorMessage = error.localizedDescription
        }
    }

    private func loadSelectedPhotos(_ items: [PhotosPickerItem]) async throws -> [MobileInspectionPhotoInput] {
        var inputs: [MobileInspectionPhotoInput] = []
        for (index, item) in items.enumerated() {
            guard let data = try await item.loadTransferable(type: Data.self) else {
                throw MobileInboxError.imageCouldNotBeDecoded("Foto \(index + 1)")
            }
            guard !data.isEmpty else {
                throw MobileInboxError.imageDataIsEmpty("Foto \(index + 1)")
            }
            inputs.append(MobileInspectionPhotoInput(
                id: UUID().uuidString,
                fileName: "Auswahl \(index + 1)",
                data: data
            ))
        }
        return inputs
    }

    private func addCameraImage(_ image: UIImage) {
        guard let data = image.jpegData(compressionQuality: 0.95) else {
            errorMessage = MobileInboxError.imageCouldNotBeEncoded("Kameraaufnahme").localizedDescription
            return
        }
        guard !data.isEmpty else {
            errorMessage = MobileInboxError.imageDataIsEmpty("Kameraaufnahme").localizedDescription
            return
        }

        cameraPhotos.append(MobileInspectionPhotoInput(
            id: UUID().uuidString,
            fileName: "Kameraaufnahme \(cameraPhotos.count + 1)",
            data: data
        ))
    }
}

private struct MobileCameraPicker: UIViewControllerRepresentable {
    let onImagePicked: (UIImage) -> Void
    @Environment(\.dismiss) private var dismiss

    func makeCoordinator() -> Coordinator {
        Coordinator(onImagePicked: onImagePicked, dismiss: dismiss)
    }

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
        picker.sourceType = .camera
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_ uiViewController: UIImagePickerController, context: Context) {
    }

    final class Coordinator: NSObject, UINavigationControllerDelegate, UIImagePickerControllerDelegate {
        private let onImagePicked: (UIImage) -> Void
        private let dismiss: DismissAction

        init(onImagePicked: @escaping (UIImage) -> Void, dismiss: DismissAction) {
            self.onImagePicked = onImagePicked
            self.dismiss = dismiss
        }

        func imagePickerController(
            _ picker: UIImagePickerController,
            didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]
        ) {
            if let image = info[.originalImage] as? UIImage {
                onImagePicked(image)
            }
            dismiss()
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            dismiss()
        }
    }
}
