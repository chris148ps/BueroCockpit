import PhotosUI
import SwiftUI
import UIKit
import UniformTypeIdentifiers

struct MobileInspectionFormView: View {
    let writer: MobileInboxWriter
    let draftStore: MobileInspectionDraftStore
    let editingEntryID: String?
    let availableCategories: [String]
    let onSaved: (MobileInspectionSaveResult) -> Void
    let onNeedsFolderSelection: () -> Void
    let onDraftStateChanged: (Bool) -> Void
    let onDismiss: () -> Void

    @Environment(\.scenePhase) private var scenePhase
    @State private var draft = MobileInspectionDraft()
    @State private var selectedPhotoItems: [PhotosPickerItem] = []
    @State private var selectedLibraryPhotos: [MobileInspectionPhotoInput] = []
    @State private var cameraPhotos: [MobileInspectionPhotoInput] = []
    @State private var sketches: [MobileInspectionSketchInput] = []
    @State private var files: [MobileInspectionFileInput] = []
    @State private var activeSheet: MobileInspectionSheet?
    @State private var activePreview: MobileInspectionPreview?
    @State private var isFileImporterPresented = false
    @State private var isSaving = false
    @State private var isLoadingPhotos = false
    @State private var errorMessage: String?
    @State private var hasRestoredDraft = false
    @State private var isRestoringDraft = false
    @State private var isDiscardConfirmationPresented = false
    @State private var pendingDeletion: MobileInspectionDeletion?

    var body: some View {
        NavigationStack {
            formView
        }
    }

    private var formView: some View {
        Form {
            customerSection
            categorySection
            notesSection
            photosSection
            sketchesSection
            filesSection
            errorSection
        }
        .navigationTitle(editingEntryID == nil ? "Neue Aufgabe" : "Eingang bearbeiten")
        .toolbar {
            ToolbarItem(placement: .cancellationAction) {
                Button("Abbrechen", action: cancel)
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
            restoreDraftIfNeeded()
        }
        .onChange(of: selectedPhotoItems) { _, items in
            loadPhotosFromPickerItems(items)
        }
        .onChange(of: scenePhase) { _, phase in
            handleScenePhaseChange(phase)
        }
        .fileImporter(
            isPresented: $isFileImporterPresented,
            allowedContentTypes: [.item],
            allowsMultipleSelection: true
        ) { result in
            loadFilesFromImporterResult(result)
        }
        .interactiveDismissDisabled(currentDraft.hasUserContent)
        .alert("Entwurf verwerfen?", isPresented: $isDiscardConfirmationPresented) {
            Button("Weiter bearbeiten", role: .cancel) {
            }
            Button("Entwurf verwerfen", role: .destructive) {
                discardDraftAndDismiss()
            }
        } message: {
            Text("Der angefangene mobile Eingang bleibt sonst lokal gespeichert.")
        }
        .alert("Anhang löschen?", isPresented: Binding(
            get: { pendingDeletion != nil },
            set: { isPresented in
                if !isPresented {
                    pendingDeletion = nil
                }
            }
        )) {
            Button("Behalten", role: .cancel) {
                pendingDeletion = nil
            }
            Button("Löschen", role: .destructive) {
                confirmPendingDeletion()
            }
        } message: {
            Text("Der Anhang wird erst beim Speichern dauerhaft aus diesem wartenden mobilen Eingang entfernt.")
        }
        .sheet(item: $activeSheet) { sheet in
            activeSheetView(sheet)
        }
        .fullScreenCover(item: $activePreview) { preview in
            MobileInspectionPreviewView(preview: preview)
        }
    }

    @ViewBuilder
    private func activeSheetView(_ sheet: MobileInspectionSheet) -> some View {
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

    private var allPhotosForDisplay: [MobileInspectionPhotoInput] {
        selectedLibraryPhotos + cameraPhotos
    }

    private var currentDraft: MobileInspectionDraft {
        var current = draft
        current.photos = selectedLibraryPhotos + cameraPhotos
        current.sketches = sketches
        current.files = files
        if Self.isLegacyMobileApprovalCategory(current.category) {
            current.category = ""
        }
        return current
    }

    private var categoryOptions: [String] {
        var seen = Set<String>()
        return availableCategories.compactMap { category in
            let trimmed = category.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty,
                  !Self.isLegacyMobileApprovalCategory(trimmed) else {
                return nil
            }

            let key = trimmed.lowercased()
            guard seen.insert(key).inserted else {
                return nil
            }

            return trimmed
        }
    }

    private var customerSection: AnyView {
        AnyView(Section("Kunde / Auftrag") {
            TextField("Kunde / Name", text: draftTextBinding(\.customerName))
            TextField("Adresse", text: draftTextBinding(\.address), axis: .vertical)
            TextField("Telefon", text: draftTextBinding(\.phone))
                .keyboardType(.phonePad)
            TextField("E-Mail", text: draftTextBinding(\.email))
                .keyboardType(.emailAddress)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
            TextField("Titel / Betreff", text: draftTextBinding(\.title))
        })
    }

    private var categorySection: AnyView {
        AnyView(Section("Kategorie") {
            Picker("Kategorie", selection: categoryBinding) {
                Text("Keine Kategorie").tag("")
                ForEach(categoryOptions, id: \.self) { category in
                    Text(category).tag(category)
                }
            }
        })
    }

    private var notesSection: AnyView {
        AnyView(Section("Notiz") {
            TextField("Notiz", text: draftTextBinding(\.notes), axis: .vertical)
                .lineLimit(8...14)
        })
    }

    private var photosSection: AnyView {
        AnyView(Section("Fotos") {
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
                    photoRow(photo)
                }
            }
        })
    }

    private var sketchesSection: AnyView {
        AnyView(Section("Skizzen") {
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
                    sketchRow(sketch)
                }
            }
        })
    }

    private var filesSection: AnyView {
        AnyView(Section("Dateien") {
            Button {
                isFileImporterPresented = true
            } label: {
                Label("Dateien auswählen", systemImage: "paperclip")
            }

            if files.isEmpty {
                Text("Noch keine Datei hinzugefügt.")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(files) { file in
                    fileRow(file)
                }
            }
        })
    }

    private var errorSection: AnyView {
        guard let errorMessage else {
            return AnyView(EmptyView())
        }

        return AnyView(
            Section {
                Label(errorMessage, systemImage: "exclamationmark.triangle")
                    .foregroundStyle(.orange)
            }
        )
    }

    private func photoRow(_ photo: MobileInspectionPhotoInput) -> some View {
        let annotateTitle = photo.annotatedData == nil ? "Markieren" : "Bearbeiten"
        return HStack(spacing: 12) {
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
                    Button(role: .destructive) {
                        pendingDeletion = .photoAnnotation(photo.id)
                    } label: {
                        Label("Markierung löschen", systemImage: "eraser")
                    }
                    .buttonStyle(.borderless)
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
                Label(annotateTitle, systemImage: "pencil.tip")
            }
            .buttonStyle(.borderless)
            Button(role: .destructive) {
                pendingDeletion = .photo(photo.id)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
        }
    }

    private func sketchRow(_ sketch: MobileInspectionSketchInput) -> some View {
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
                pendingDeletion = .sketch(sketch.id)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
        }
    }

    private func fileRow(_ file: MobileInspectionFileInput) -> some View {
        HStack(spacing: 12) {
            Image(systemName: "doc")
                .frame(width: 32)
            Text(file.fileName)
                .lineLimit(2)
            Spacer()
            Button(role: .destructive) {
                pendingDeletion = .file(file.id)
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
        }
    }

    private func draftTextBinding(_ keyPath: WritableKeyPath<MobileInspectionDraft, String>) -> Binding<String> {
        Binding(
            get: {
                draft[keyPath: keyPath]
            },
            set: { value in
                draft[keyPath: keyPath] = value
                persistCurrentDraft()
            }
        )
    }

    private var categoryBinding: Binding<String> {
        Binding(
            get: {
                Self.isLegacyMobileApprovalCategory(draft.category) ? "" : draft.category
            },
            set: { value in
                draft.category = Self.isLegacyMobileApprovalCategory(value) ? "" : value
                persistCurrentDraft()
            }
        )
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

    private func cancel() {
        if currentDraft.hasUserContent {
            persistCurrentDraft()
            isDiscardConfirmationPresented = true
            return
        }

        discardDraftAndDismiss()
    }

    private func restoreDraftIfNeeded() {
        guard !hasRestoredDraft else {
            return
        }

        hasRestoredDraft = true
        do {
            let storedDraft = try draftStore.load()
            let restoredDraft: MobileInspectionDraft
            if let storedDraft, storedDraft.editingEntryID == editingEntryID {
                restoredDraft = storedDraft
            } else if let editingEntryID {
                restoredDraft = try writer.loadDraftForEditing(entryID: editingEntryID)
            } else if let storedDraft, storedDraft.editingEntryID == nil {
                restoredDraft = storedDraft
            } else {
                onDraftStateChanged(storedDraft != nil)
                return
            }

            isRestoringDraft = true
            draft = MobileInspectionDraft(
                editingEntryID: restoredDraft.editingEntryID,
                editingEntryDirectoryName: restoredDraft.editingEntryDirectoryName,
                originalCreatedAt: restoredDraft.originalCreatedAt,
                customerName: restoredDraft.customerName,
                address: restoredDraft.address,
                phone: restoredDraft.phone,
                email: restoredDraft.email,
                title: restoredDraft.title,
                category: Self.isLegacyMobileApprovalCategory(restoredDraft.category) ? "" : restoredDraft.category,
                notes: restoredDraft.notes
            )
            selectedPhotoItems = []
            selectedLibraryPhotos = []
            cameraPhotos = restoredDraft.photos
            sketches = restoredDraft.sketches
            files = restoredDraft.files
            isRestoringDraft = false
            onDraftStateChanged(true)
        } catch {
            errorMessage = "Der lokale Entwurf konnte nicht geladen werden: \(error.localizedDescription)"
            onDraftStateChanged(draftStore.hasDraft())
        }
    }

    private func persistCurrentDraft() {
        guard !isRestoringDraft else {
            return
        }

        do {
            let current = currentDraft
            try draftStore.save(current)
            onDraftStateChanged(current.hasUserContent)
        } catch {
            errorMessage = "Der lokale Entwurf konnte nicht gespeichert werden: \(error.localizedDescription)"
            onDraftStateChanged(draftStore.hasDraft())
        }
    }

    private func handleScenePhaseChange(_ phase: ScenePhase) {
        switch phase {
        case .inactive, .background:
            persistCurrentDraft()
        case .active:
            break
        @unknown default:
            persistCurrentDraft()
        }
    }

    private func discardDraftAndDismiss() {
        do {
            try draftStore.discard()
            onDraftStateChanged(false)
            onDismiss()
        } catch {
            errorMessage = "Der lokale Entwurf konnte nicht verworfen werden: \(error.localizedDescription)"
            onDraftStateChanged(draftStore.hasDraft())
        }
    }

    private static func isLegacyMobileApprovalCategory(_ value: String) -> Bool {
        let legacyName = ["Wartet", "auf", "Freigabe"].joined(separator: " ")
        return value.trimmingCharacters(in: .whitespacesAndNewlines)
            .localizedCaseInsensitiveCompare(legacyName) == .orderedSame
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
            draft.files = files
            if Self.isLegacyMobileApprovalCategory(draft.category) {
                draft.category = ""
            }
            let result: MobileInspectionSaveResult
            if let editingEntryID {
                result = try writer.update(entryID: editingEntryID, draft: draft)
            } else {
                result = try writer.save(draft)
            }
            try? draftStore.discard()
            draft = MobileInspectionDraft()
            selectedPhotoItems = []
            selectedLibraryPhotos = []
            cameraPhotos = []
            sketches = []
            files = []
            onDraftStateChanged(false)
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
            persistCurrentDraft()
        } catch {
            selectedLibraryPhotos = []
            errorMessage = error.localizedDescription
            persistCurrentDraft()
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
        persistCurrentDraft()
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
        persistCurrentDraft()
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
        persistCurrentDraft()
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

    private func loadFilesFromImporterResult(_ result: Result<[URL], Error>) {
        do {
            let urls = try result.get()
            for url in urls {
                let accessGranted = url.startAccessingSecurityScopedResource()
                defer {
                    if accessGranted {
                        url.stopAccessingSecurityScopedResource()
                    }
                }
                let data = try Data(contentsOf: url)
                guard !data.isEmpty else {
                    throw MobileInboxError.fileIsEmpty(url.lastPathComponent)
                }
                files.append(MobileInspectionFileInput(
                    id: UUID().uuidString,
                    fileName: url.lastPathComponent,
                    data: data
                ))
            }
            errorMessage = nil
            persistCurrentDraft()
        } catch {
            errorMessage = error.localizedDescription
            persistCurrentDraft()
        }
    }

    private func confirmPendingDeletion() {
        guard let pendingDeletion else {
            return
        }
        switch pendingDeletion {
        case .photo(let id):
            selectedLibraryPhotos.removeAll { $0.id == id }
            cameraPhotos.removeAll { $0.id == id }
        case .photoAnnotation(let id):
            removePhotoAnnotation(id: id)
        case .sketch(let id):
            sketches.removeAll { $0.id == id }
        case .file(let id):
            files.removeAll { $0.id == id }
        }
        self.pendingDeletion = nil
        persistCurrentDraft()
    }

    private func removePhotoAnnotation(id: String) {
        selectedLibraryPhotos = selectedLibraryPhotos.map { photo in
            guard photo.id == id else {
                return photo
            }
            return MobileInspectionPhotoInput(
                id: photo.id,
                fileName: photo.fileName,
                data: photo.data,
                previewData: photo.previewData,
                annotatedData: nil,
                annotatedPreviewData: nil
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
                annotatedData: nil,
                annotatedPreviewData: nil
            )
        }
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

private enum MobileInspectionDeletion: Identifiable {
    case photo(String)
    case photoAnnotation(String)
    case sketch(String)
    case file(String)

    var id: String {
        switch self {
        case .photo(let id):
            return "photo-\(id)"
        case .photoAnnotation(let id):
            return "photo-annotation-\(id)"
        case .sketch(let id):
            return "sketch-\(id)"
        case .file(let id):
            return "file-\(id)"
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
