import PencilKit
import SwiftUI
import UIKit

struct MobileSketchCanvasView: View {
    let onSave: (MobileInspectionSketchInput) -> Void
    let onCancel: () -> Void

    @State private var canvasController = PencilSketchCanvasController()
    @State private var isCanvasEmpty = true
    @State private var errorMessage: String?

    var body: some View {
        VStack(spacing: 0) {
            sketchHeader
            Divider()
            PencilSketchCanvas(controller: canvasController) { isEmpty in
                if isCanvasEmpty != isEmpty {
                    isCanvasEmpty = isEmpty
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(Color(uiColor: .systemBackground))
            .clipped()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private var sketchHeader: some View {
        VStack(spacing: 8) {
            HStack(spacing: 12) {
                Button("Abbrechen", action: onCancel)

                Spacer()

                Text("Skizze")
                    .font(.headline)
                    .fontWeight(.semibold)

                Spacer()

                Button("Leeren") {
                    canvasController.clear()
                    isCanvasEmpty = true
                    errorMessage = nil
                }
                .disabled(isCanvasEmpty)

                Button("Hinzufügen") {
                    saveSketch()
                }
                .buttonStyle(.borderedProminent)
                .disabled(isCanvasEmpty)
            }

            if let errorMessage {
                Label(errorMessage, systemImage: "exclamationmark.triangle")
                    .foregroundStyle(.orange)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .padding(12)
        .background(Color(uiColor: .systemBackground))
    }

    private func saveSketch() {
        let drawing = canvasController.drawing
        guard !drawing.strokes.isEmpty else {
            errorMessage = "Bitte zuerst eine Skizze zeichnen."
            return
        }

        let image = renderImageOnWhiteBackground(
            drawing: drawing,
            exportRect: canvasController.exportRect(for: drawing),
            scale: min(UIScreen.main.scale, 2)
        )
        guard let pngData = image.pngData(), !pngData.isEmpty else {
            errorMessage = "Die Skizze konnte nicht als PNG gespeichert werden."
            return
        }

        let previewImage = renderPreviewOnWhiteBackground(drawing: drawing, scale: min(UIScreen.main.scale, 2))
        guard let previewData = previewImage.pngData(), !previewData.isEmpty else {
            errorMessage = "Die Skizzenvorschau konnte nicht als PNG gespeichert werden."
            return
        }

        let drawingData = drawing.dataRepresentation()
        onSave(MobileInspectionSketchInput(
            id: UUID().uuidString,
            fileName: "Skizze",
            data: pngData,
            previewData: previewData,
            drawingData: drawingData.isEmpty ? nil : drawingData
        ))
    }

    private func renderImageOnWhiteBackground(drawing: PKDrawing, exportRect: CGRect, scale: CGFloat) -> UIImage {
        let safeOutputSize = CGSize(
            width: max(1, ceil(exportRect.width)),
            height: max(1, ceil(exportRect.height))
        )
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = scale
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: safeOutputSize, format: format)
        return renderer.image { context in
            let outputRect = CGRect(origin: .zero, size: safeOutputSize)
            context.cgContext.setFillColor(UIColor.white.cgColor)
            context.cgContext.fill(outputRect)

            let drawingImage = drawing.image(from: exportRect, scale: scale)
            drawingImage.draw(
                in: outputRect,
                blendMode: .normal,
                alpha: 1
            )
        }
    }

    private func renderPreviewOnWhiteBackground(drawing: PKDrawing, scale: CGFloat) -> UIImage {
        let previewRect = previewExportRect(for: drawing)
        let previewSize = CGSize(width: 600, height: 400)
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = scale
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: previewSize, format: format)
        return renderer.image { context in
            let outputRect = CGRect(origin: .zero, size: previewSize)
            context.cgContext.setFillColor(UIColor.white.cgColor)
            context.cgContext.fill(outputRect)

            let drawingImage = drawing.image(from: previewRect, scale: scale)
            drawingImage.draw(in: outputRect, blendMode: .normal, alpha: 1)
        }
    }

    private func previewExportRect(for drawing: PKDrawing) -> CGRect {
        guard !drawing.bounds.isNull, !drawing.bounds.isEmpty else {
            return CGRect(x: 0, y: 0, width: 600, height: 400)
        }

        let padded = drawing.bounds.insetBy(dx: -80, dy: -80)
        let minimumWidth: CGFloat = 600
        let minimumHeight: CGFloat = 400
        let width = max(minimumWidth, padded.width)
        let height = max(minimumHeight, padded.height)
        let originX = padded.midX - width / 2
        let originY = padded.midY - height / 2
        return CGRect(x: originX, y: originY, width: width, height: height).integral
    }
}

struct MobilePhotoMarkupView: View {
    let photoData: Data
    let title: String
    let onSave: (Data) -> Void
    let onCancel: () -> Void

    @State private var canvasController = PhotoMarkupCanvasController()
    @State private var isCanvasEmpty = true
    @State private var errorMessage: String?

    private var image: UIImage? {
        UIImage(data: photoData)
    }

    var body: some View {
        VStack(spacing: 0) {
            markupHeader
            Divider()
            if let image {
                GeometryReader { proxy in
                    let fit = fittedImageRect(imageSize: image.size, containerSize: proxy.size)
                    ZStack(alignment: .topLeading) {
                        Color(uiColor: .systemBackground)
                        Image(uiImage: image)
                            .resizable()
                            .scaledToFit()
                            .frame(width: fit.width, height: fit.height)
                            .position(x: fit.midX, y: fit.midY)
                        PhotoMarkupCanvas(controller: canvasController) { isEmpty in
                            if isCanvasEmpty != isEmpty {
                                isCanvasEmpty = isEmpty
                            }
                        }
                        .frame(width: fit.width, height: fit.height)
                        .position(x: fit.midX, y: fit.midY)
                    }
                }
            } else {
                ContentUnavailableView(
                    "Foto nicht lesbar",
                    systemImage: "photo",
                    description: Text("Dieses Foto kann nicht markiert werden.")
                )
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(uiColor: .systemBackground))
    }

    private var markupHeader: some View {
        VStack(spacing: 8) {
            HStack(spacing: 12) {
                Button("Abbrechen", action: onCancel)

                Spacer()

                Text(title.isEmpty ? "Foto markieren" : title)
                    .font(.headline)
                    .fontWeight(.semibold)
                    .lineLimit(1)

                Spacer()

                Button("Leeren") {
                    canvasController.clear()
                    isCanvasEmpty = true
                    errorMessage = nil
                }
                .disabled(isCanvasEmpty)

                Button("Speichern") {
                    saveMarkedPhoto()
                }
                .buttonStyle(.borderedProminent)
                .disabled(image == nil || isCanvasEmpty)
            }

            if let errorMessage {
                Label(errorMessage, systemImage: "exclamationmark.triangle")
                    .foregroundStyle(.orange)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .padding(12)
        .background(Color(uiColor: .systemBackground))
    }

    private func saveMarkedPhoto() {
        guard let image else {
            errorMessage = "Das Foto konnte nicht gelesen werden."
            return
        }

        let drawing = canvasController.drawing
        guard !drawing.strokes.isEmpty else {
            errorMessage = "Bitte zuerst eine Markierung zeichnen."
            return
        }

        let renderedImage = renderMarkedImage(image: image, drawing: drawing)
        guard let jpgData = renderedImage.jpegData(compressionQuality: 0.9), !jpgData.isEmpty else {
            errorMessage = "Die markierte Version konnte nicht als JPG gespeichert werden."
            return
        }

        onSave(jpgData)
    }

    private func renderMarkedImage(image: UIImage, drawing: PKDrawing) -> UIImage {
        let outputSize = CGSize(width: max(1, image.size.width), height: max(1, image.size.height))
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = image.scale
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: outputSize, format: format)
        return renderer.image { context in
            let outputRect = CGRect(origin: .zero, size: outputSize)
            UIColor.white.setFill()
            context.fill(outputRect)
            image.draw(in: outputRect)

            let drawingImage = drawing.image(from: canvasController.exportRect, scale: image.scale)
            drawingImage.draw(in: outputRect, blendMode: .normal, alpha: 1)
        }
    }

    private func fittedImageRect(imageSize: CGSize, containerSize: CGSize) -> CGRect {
        guard imageSize.width > 0, imageSize.height > 0, containerSize.width > 0, containerSize.height > 0 else {
            return CGRect(origin: .zero, size: containerSize)
        }

        let scale = min(containerSize.width / imageSize.width, containerSize.height / imageSize.height)
        let size = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
        return CGRect(
            x: (containerSize.width - size.width) / 2,
            y: (containerSize.height - size.height) / 2,
            width: size.width,
            height: size.height
        )
    }
}

@MainActor
private final class PencilSketchCanvasController {
    private weak var canvasView: PKCanvasView?

    var drawing: PKDrawing {
        canvasView?.drawing ?? PKDrawing()
    }

    func exportRect(for drawing: PKDrawing) -> CGRect {
        var exportRect = CGRect(origin: .zero, size: CGSize(width: 2400, height: 1800))

        if let canvasView {
            let contentSize = canvasView.contentSize
            if contentSize.width > 0, contentSize.height > 0 {
                exportRect = CGRect(origin: .zero, size: contentSize)
            } else {
                let boundsSize = canvasView.bounds.size
                if boundsSize.width > 0, boundsSize.height > 0 {
                    exportRect = CGRect(origin: .zero, size: boundsSize)
                }
            }
        }

        if !drawing.bounds.isNull, !drawing.bounds.isEmpty {
            exportRect = exportRect.union(drawing.bounds.insetBy(dx: -160, dy: -160))
        }

        return exportRect.integral
    }

    func attach(_ canvasView: PKCanvasView) {
        self.canvasView = canvasView
    }

    func clear() {
        canvasView?.drawing = PKDrawing()
    }

}

private struct PencilSketchCanvas: UIViewControllerRepresentable {
    let controller: PencilSketchCanvasController
    let onDrawingEmptyChanged: (Bool) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onDrawingEmptyChanged: onDrawingEmptyChanged)
    }

    func makeUIViewController(context: Context) -> PencilSketchViewController {
        let viewController = PencilSketchViewController()
        viewController.canvasView.delegate = context.coordinator
        viewController.canvasView.drawingPolicy = .pencilOnly
        viewController.canvasView.tool = PencilSketchDefaults.defaultDrawingTool()
        controller.attach(viewController.canvasView)
        context.coordinator.attachToolPicker(to: viewController.canvasView)
        return viewController
    }

    func updateUIViewController(_ viewController: PencilSketchViewController, context: Context) {
        viewController.canvasView.drawingPolicy = .pencilOnly
        controller.attach(viewController.canvasView)
        context.coordinator.attachToolPicker(to: viewController.canvasView)
    }

    final class Coordinator: NSObject, PKCanvasViewDelegate {
        private let onDrawingEmptyChanged: (Bool) -> Void
        private var lastIsEmpty = true
        private weak var currentCanvasView: PKCanvasView?
        private var toolPicker: PKToolPicker?

        init(onDrawingEmptyChanged: @escaping (Bool) -> Void) {
            self.onDrawingEmptyChanged = onDrawingEmptyChanged
        }

        func canvasViewDrawingDidChange(_ canvasView: PKCanvasView) {
            let isEmpty = canvasView.drawing.strokes.isEmpty
            guard lastIsEmpty != isEmpty else {
                return
            }

            lastIsEmpty = isEmpty
            DispatchQueue.main.async { [onDrawingEmptyChanged] in
                onDrawingEmptyChanged(isEmpty)
            }
        }

        func attachToolPicker(to canvasView: PKCanvasView) {
            guard currentCanvasView !== canvasView else {
                return
            }

            currentCanvasView = canvasView
            let picker = PKToolPicker()
            picker.setVisible(true, forFirstResponder: canvasView)
            picker.addObserver(canvasView)
            canvasView.becomeFirstResponder()
            toolPicker = picker
        }
    }
}

private final class PencilSketchViewController: UIViewController {
    let canvasView = PKCanvasView()

    override func viewDidLoad() {
        super.viewDidLoad()

        view.backgroundColor = .systemBackground
        view.isOpaque = true

        canvasView.translatesAutoresizingMaskIntoConstraints = false
        canvasView.backgroundColor = .white
        canvasView.isOpaque = true
        canvasView.drawingPolicy = .pencilOnly
        canvasView.alwaysBounceVertical = true
        canvasView.alwaysBounceHorizontal = true
        canvasView.minimumZoomScale = 0.35
        canvasView.maximumZoomScale = 6
        canvasView.contentSize = CGSize(width: 3000, height: 2200)
        canvasView.contentInset = UIEdgeInsets(top: 24, left: 24, bottom: 24, right: 24)
        canvasView.contentInsetAdjustmentBehavior = .never
        canvasView.showsVerticalScrollIndicator = true
        canvasView.showsHorizontalScrollIndicator = true

        view.addSubview(canvasView)
        NSLayoutConstraint.activate([
            canvasView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            canvasView.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            canvasView.topAnchor.constraint(equalTo: view.topAnchor),
            canvasView.bottomAnchor.constraint(equalTo: view.bottomAnchor)
        ])
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        canvasView.becomeFirstResponder()
    }
}

private enum PencilSketchDefaults {
    static func defaultDrawingTool() -> PKInkingTool {
        PKInkingTool(.pen, color: .black, width: 1)
    }
}

@MainActor
private final class PhotoMarkupCanvasController {
    private weak var canvasView: PKCanvasView?

    var drawing: PKDrawing {
        canvasView?.drawing ?? PKDrawing()
    }

    var exportRect: CGRect {
        if let canvasView, canvasView.bounds.width > 0, canvasView.bounds.height > 0 {
            return CGRect(origin: .zero, size: canvasView.bounds.size)
        }

        return CGRect(x: 0, y: 0, width: 1200, height: 900)
    }

    func attach(_ canvasView: PKCanvasView) {
        self.canvasView = canvasView
    }

    func clear() {
        canvasView?.drawing = PKDrawing()
    }
}

private struct PhotoMarkupCanvas: UIViewControllerRepresentable {
    let controller: PhotoMarkupCanvasController
    let onDrawingEmptyChanged: (Bool) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onDrawingEmptyChanged: onDrawingEmptyChanged)
    }

    func makeUIViewController(context: Context) -> PhotoMarkupViewController {
        let viewController = PhotoMarkupViewController()
        viewController.canvasView.delegate = context.coordinator
        viewController.canvasView.drawingPolicy = .anyInput
        viewController.canvasView.tool = PencilSketchDefaults.defaultDrawingTool()
        controller.attach(viewController.canvasView)
        context.coordinator.attachToolPicker(to: viewController.canvasView)
        return viewController
    }

    func updateUIViewController(_ viewController: PhotoMarkupViewController, context: Context) {
        viewController.canvasView.drawingPolicy = .anyInput
        controller.attach(viewController.canvasView)
        context.coordinator.attachToolPicker(to: viewController.canvasView)
    }

    final class Coordinator: NSObject, PKCanvasViewDelegate {
        private let onDrawingEmptyChanged: (Bool) -> Void
        private var lastIsEmpty = true
        private weak var currentCanvasView: PKCanvasView?
        private var toolPicker: PKToolPicker?

        init(onDrawingEmptyChanged: @escaping (Bool) -> Void) {
            self.onDrawingEmptyChanged = onDrawingEmptyChanged
        }

        func canvasViewDrawingDidChange(_ canvasView: PKCanvasView) {
            let isEmpty = canvasView.drawing.strokes.isEmpty
            guard lastIsEmpty != isEmpty else {
                return
            }

            lastIsEmpty = isEmpty
            DispatchQueue.main.async { [onDrawingEmptyChanged] in
                onDrawingEmptyChanged(isEmpty)
            }
        }

        func attachToolPicker(to canvasView: PKCanvasView) {
            guard currentCanvasView !== canvasView else {
                return
            }

            currentCanvasView = canvasView
            let picker = PKToolPicker()
            picker.setVisible(true, forFirstResponder: canvasView)
            picker.addObserver(canvasView)
            canvasView.becomeFirstResponder()
            toolPicker = picker
        }
    }
}

private final class PhotoMarkupViewController: UIViewController {
    let canvasView = PKCanvasView()

    override func viewDidLoad() {
        super.viewDidLoad()

        view.backgroundColor = .clear
        view.isOpaque = false

        canvasView.translatesAutoresizingMaskIntoConstraints = false
        canvasView.backgroundColor = .clear
        canvasView.isOpaque = false
        canvasView.drawingPolicy = .anyInput
        canvasView.alwaysBounceVertical = false
        canvasView.alwaysBounceHorizontal = false
        canvasView.minimumZoomScale = 1
        canvasView.maximumZoomScale = 1
        canvasView.contentInsetAdjustmentBehavior = .never

        view.addSubview(canvasView)
        NSLayoutConstraint.activate([
            canvasView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            canvasView.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            canvasView.topAnchor.constraint(equalTo: view.topAnchor),
            canvasView.bottomAnchor.constraint(equalTo: view.bottomAnchor)
        ])
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        canvasView.becomeFirstResponder()
    }
}
