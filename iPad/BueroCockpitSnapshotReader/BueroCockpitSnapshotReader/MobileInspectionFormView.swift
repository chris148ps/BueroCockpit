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
    @State private var activeSheet: MobileInspectionSheet?
    @State private var activePreview: MobileInspectionPreview?
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
                            activeSheet = .camera
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
                        ForEach(allPhotosForDisplay) { photo in
                            HStack(spacing: 12) {
                                VStack(alignment: .leading, spacing: 6) {
                                    previewButton(
                                        title: "Originalfoto",
                                        fileName: photo.fileName,
                                        previewData: photo.previewData,
                                        fullData: photo.data,
                                        systemImage: "photo"
                                    )
                                    if let annotatedData = photo.annotatedData {
                                        previewButton(
                                            title: "Markiertes Foto",
                                            fileName: photo.fileName,
                                            previewData: photo.annotatedPreviewData,
                                            fullData: annotatedData,
                                            systemImage: "pencil.tip.crop.circle"
                                        )
                                    }
                                }
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(photo.fileName)
                                    if photo.annotatedData != nil {
                                        Label("Markierte Version vorhanden", systemImage: "pencil.tip.crop.circle")
                                            .font(.caption)
                                            .foregroundStyle(.secondary)
                                    }
                                }
                                Spacer()
                                Button {
                                    activeSheet = .annotatePhoto(photo.id)
                                } label: {
                                    Label(
                                        photo.annotatedData == nil ? "Markieren" : "Bearbeiten",
                                        systemImage: "pencil.tip"
                                    )
                                }
                                .buttonStyle(.borderless)
                            }
                        }
                    }
                }

                Section("Skizzen") {
                    Button {
                        activeSheet = .sketch
                    } label: {
                        Label("Skizze hinzufügen", systemImage: "pencil.tip")
                    }

                    if sketches.isEmpty {
                        Text("Noch keine Skizze hinzugefügt.")
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(sketches) { sketch in
                            HStack(spacing: 12) {
                                previewButton(
                                    title: "Skizze",
                                    fileName: sketch.fileName,
                                    previewData: sketch.previewData,
                                    fullData: sketch.data,
                                    systemImage: "scribble"
                                )
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
            .sheet(item: $activeSheet) { sheet in
                switch sheet {
                case .camera:
                    MobileCameraPicker { image in
                        addCameraImage(image)
                        activeSheet = nil
                    }
                case .sketch:
                    MobileSketchCanvasView(
                        onSave: { sketch in
                            addSketch(sketch)
                            activeSheet = nil
                        },
                        onCancel: {
                            activeSheet = nil
                        }
                    )
                case .annotatePhoto(let photoID):
                    if let photo = photoInput(for: photoID) {
                        MobilePhotoMarkupView(
                            photoData: photo.annotatedData ?? photo.data,
                            title: photo.fileName,
                            onSave: { markedData in
                                updateAnnotatedPhoto(id: photoID, annotatedData: markedData)
                                activeSheet = nil
                            },
                            onCancel: {
                                activeSheet = nil
                            }
                        )
                    }
                }
            }
            .fullScreenCover(item: $activePreview) { preview in
                MobileInspectionPreviewView(preview: preview)
            }
        }
    }

    private var allPhotosForDisplay: [MobileInspectionPhotoInput] {
        selectedLibraryPhotos + cameraPhotos
    }

    private func previewButton(
        title: String,
        fileName: String,
        previewData: Data?,
        fullData: Data,
        systemImage: String
    ) -> some View {
        Button {
            activePreview = MobileInspectionPreview(
                title: title,
                fileName: fileName,
                data: fullData,
                systemImage: systemImage
            )
        } label: {
            VStack(alignment: .leading, spacing: 4) {
                previewImage(
                    data: previewData ?? fullData,
                    systemImage: systemImage,
                    message: "\(title) kann nicht angezeigt werden."
                )
                    .frame(width: 116, height: 86)
                    .clipped()
                    .background(Color.secondary.opacity(0.08), in: RoundedRectangle(cornerRadius: 8))
                    .overlay(
                        RoundedRectangle(cornerRadius: 8)
                            .stroke(Color.secondary.opacity(0.2), lineWidth: 1)
                    )
                Text(title)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .buttonStyle(.plain)
        .accessibilityLabel("\(title) öffnen")
    }

    private func previewImage(data: Data, systemImage: String, message: String) -> some View {
        Group {
            if let image = UIImage(data: data) {
                Image(uiImage: image)
                    .resizable()
                    .scaledToFit()
            } else {
                VStack(spacing: 4) {
                    Image(systemName: systemImage)
                        .font(.title3)
                    Text(message)
                        .font(.caption2)
                        .multilineTextAlignment(.center)
                }
                .foregroundStyle(.orange)
                .padding(6)
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
            let previewData = makePreviewJPEGData(from: data)
            inputs.append(MobileInspectionPhotoInput(
                id: UUID().uuidString,
                fileName: "Auswahl \(index + 1)",
                data: data,
                previewData: previewData,
                annotatedData: nil,
                annotatedPreviewData: nil
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
            data: data,
            previewData: makePreviewJPEGData(from: data),
            annotatedData: nil,
            annotatedPreviewData: nil
        ))
    }

    private func addSketch(_ sketch: MobileInspectionSketchInput) {
        guard !sketch.data.isEmpty else {
            errorMessage = MobileInboxError.sketchDataIsEmpty("Skizze \(sketches.count + 1)").localizedDescription
            return
        }

        sketches.append(MobileInspectionSketchInput(
            id: sketch.id,
            fileName: "Skizze \(sketches.count + 1)",
            data: sketch.data,
            previewData: sketch.previewData,
            drawingData: sketch.drawingData
        ))
        errorMessage = nil
    }

    private func removeSketch(_ sketch: MobileInspectionSketchInput) {
        sketches.removeAll { $0.id == sketch.id }
    }

    private func photoInput(for id: String) -> MobileInspectionPhotoInput? {
        selectedLibraryPhotos.first { $0.id == id } ?? cameraPhotos.first { $0.id == id }
    }

    private func updateAnnotatedPhoto(id: String, annotatedData: Data) {
        selectedLibraryPhotos = selectedLibraryPhotos.map { photo in
            guard photo.id == id else {
                return photo
            }

            return MobileInspectionPhotoInput(
                id: photo.id,
                fileName: photo.fileName,
                data: photo.data,
                previewData: photo.previewData,
                annotatedData: annotatedData,
                annotatedPreviewData: makePreviewJPEGData(from: annotatedData)
            )
        }
        cameraPhotos = cameraPhotos.map { photo in
            guard photo.id == id else {
                return photo
            }

            return MobileInspectionPhotoInput(
                id: photo.id,
                fileName: photo.fileName,
                data: photo.data,
                previewData: photo.previewData,
                annotatedData: annotatedData,
                annotatedPreviewData: makePreviewJPEGData(from: annotatedData)
            )
        }
        errorMessage = nil
    }

    private func makePreviewJPEGData(from data: Data) -> Data? {
        guard let image = UIImage(data: data) else {
            return nil
        }

        let longestSide = max(image.size.width, image.size.height)
        guard longestSide > 0 else {
            return nil
        }

        let maxPixelLength: CGFloat = 400
        let targetSize: CGSize
        if longestSide > maxPixelLength {
            let scale = maxPixelLength / longestSide
            targetSize = CGSize(width: image.size.width * scale, height: image.size.height * scale)
        } else {
            targetSize = image.size
        }

        let format = UIGraphicsImageRendererFormat.default()
        format.scale = 1
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: targetSize, format: format)
        let renderedImage = renderer.image { context in
            UIColor.white.setFill()
            context.fill(CGRect(origin: .zero, size: targetSize))
            image.draw(in: CGRect(origin: .zero, size: targetSize))
        }
        return renderedImage.jpegData(compressionQuality: 0.7)
    }
}

private enum MobileInspectionSheet: Identifiable {
    case camera
    case sketch
    case annotatePhoto(String)

    var id: String {
        switch self {
        case .camera:
            return "camera"
        case .sketch:
            return "sketch"
        case .annotatePhoto(let photoID):
            return "annotate-photo-\(photoID)"
        }
    }
}

private struct MobileInspectionPreview: Identifiable {
    let id = UUID()
    let title: String
    let fileName: String
    let data: Data
    let systemImage: String
}

private struct MobileInspectionPreviewView: View {
    let preview: MobileInspectionPreview
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            VStack(spacing: 16) {
                VStack(spacing: 4) {
                    Text(preview.title)
                        .font(.title2)
                        .fontWeight(.semibold)
                    Text(preview.fileName)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                }

                ZStack {
                    RoundedRectangle(cornerRadius: 8)
                        .fill(Color.secondary.opacity(0.08))

                    if let image = UIImage(data: preview.data) {
                        Image(uiImage: image)
                            .resizable()
                            .scaledToFit()
                            .padding(8)
                    } else {
                        ContentUnavailableView(
                            "Datei kann nicht angezeigt werden",
                            systemImage: preview.systemImage,
                            description: Text("Die Datei fehlt oder das Bildformat konnte nicht geladen werden.")
                        )
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            .padding()
            .navigationTitle("Detailansicht")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Schließen") {
                        dismiss()
                    }
                }
            }
        }
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
