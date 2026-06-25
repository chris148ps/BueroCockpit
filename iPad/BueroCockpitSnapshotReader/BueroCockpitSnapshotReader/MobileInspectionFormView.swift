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
    @State private var sketches: [MobileInspectionSketchInput] = []
    @State private var isCameraPresented = false
    @State private var isSketchCanvasPresented = false
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
                Section("Kunde / Auftrag") {
                    TextField("Kunde / Name", text: $draft.customerName)
                    TextField("Adresse", text: $draft.address, axis: .vertical)
                    TextField("Telefon", text: $draft.phone)
                        .keyboardType(.phonePad)
                    TextField("E-Mail", text: $draft.email)
                        .keyboardType(.emailAddress)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                    TextField("Titel / Betreff", text: $draft.title)
                    Picker("Kategorie", selection: $draft.category) {
                        ForEach(effectiveCategoryNames, id: \.self) { category in
                            Text(category).tag(category)
                        }
                    }
                }

                Section("Notiz") {
                    TextField("Notiz", text: $draft.notes, axis: .vertical)
                        .lineLimit(8...14)
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

                Section("Skizzen") {
                    Button {
                        isSketchCanvasPresented = true
                    } label: {
                        Label("Skizze hinzufügen", systemImage: "pencil.tip")
                    }

                    if sketches.isEmpty {
                        Text("Noch keine Skizze hinzugefügt.")
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(sketches) { sketch in
                            HStack(spacing: 12) {
                                sketchPreview(sketch)
                                Text(sketch.fileName)
                                Spacer()
                                Button(role: .destructive) {
                                    removeSketch(sketch)
                                } label: {
                                    Image(systemName: "trash")
                                }
                                .buttonStyle(.borderless)
                            }
                        }
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
            .sheet(isPresented: $isSketchCanvasPresented) {
                MobileSketchCanvasView(
                    onSave: { data in
                        addSketch(data)
                        isSketchCanvasPresented = false
                    },
                    onCancel: {
                        isSketchCanvasPresented = false
                    }
                )
            }
        }
    }

    private func sketchPreview(_ sketch: MobileInspectionSketchInput) -> some View {
        Group {
            if let image = UIImage(data: sketch.data) {
                Image(uiImage: image)
                    .resizable()
                    .scaledToFit()
            } else {
                Image(systemName: "scribble")
                    .font(.title2)
                    .foregroundStyle(.secondary)
            }
        }
        .frame(width: 72, height: 48)
        .background(Color.secondary.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
        .overlay(
            RoundedRectangle(cornerRadius: 6)
                .stroke(Color.secondary.opacity(0.2), lineWidth: 1)
        )
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
            draft.sketches = sketches
            let result = try writer.save(draft)
            draft = MobileInspectionDraft(category: effectiveCategoryNames.first ?? "")
            selectedPhotoItems = []
            selectedLibraryPhotos = []
            cameraPhotos = []
            sketches = []
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

    private func addSketch(_ data: Data) {
        guard !data.isEmpty else {
            errorMessage = MobileInboxError.sketchDataIsEmpty("Skizze \(sketches.count + 1)").localizedDescription
            return
        }

        sketches.append(MobileInspectionSketchInput(
            id: UUID().uuidString,
            fileName: "Skizze \(sketches.count + 1)",
            data: data
        ))
        errorMessage = nil
    }

    private func removeSketch(_ sketch: MobileInspectionSketchInput) {
        sketches.removeAll { $0.id == sketch.id }
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
