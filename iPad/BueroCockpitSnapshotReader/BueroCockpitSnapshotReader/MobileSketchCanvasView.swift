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
        NavigationStack {
            VStack(spacing: 10) {
                PencilSketchCanvas(controller: canvasController) { isEmpty in
                    if isCanvasEmpty != isEmpty {
                        isCanvasEmpty = isEmpty
                    }
                }
                    .background(Color(uiColor: .systemBackground))
                    .overlay(
                        RoundedRectangle(cornerRadius: 8)
                            .stroke(Color.secondary.opacity(0.25), lineWidth: 1)
                    )

                if let errorMessage {
                    Label(errorMessage, systemImage: "exclamationmark.triangle")
                        .foregroundStyle(.orange)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding(12)
            .navigationTitle("Skizze")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Abbrechen", action: onCancel)
                }
                ToolbarItem(placement: .primaryAction) {
                    Button("Leeren") {
                        canvasController.clear()
                        isCanvasEmpty = true
                        errorMessage = nil
                    }
                    .disabled(isCanvasEmpty)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Hinzufügen") {
                        saveSketch()
                    }
                    .disabled(isCanvasEmpty)
                }
            }
        }
    }

    private func saveSketch() {
        let drawing = canvasController.drawing
        guard !drawing.strokes.isEmpty else {
            errorMessage = "Bitte zuerst eine Skizze zeichnen."
            return
        }

        let bounds = exportBounds(for: drawing)
        let image = renderImage(drawing: drawing, bounds: bounds, scale: 4)
        guard let pngData = image.pngData(), !pngData.isEmpty else {
            errorMessage = "Die Skizze konnte nicht als PNG gespeichert werden."
            return
        }

        let drawingData = drawing.dataRepresentation()
        onSave(MobileInspectionSketchInput(
            id: UUID().uuidString,
            fileName: "Skizze",
            data: pngData,
            drawingData: drawingData.isEmpty ? nil : drawingData
        ))
    }

    private func exportBounds(for drawing: PKDrawing) -> CGRect {
        guard !drawing.bounds.isNull, !drawing.bounds.isEmpty else {
            return CGRect(x: 0, y: 0, width: 2400, height: 1800)
        }

        let padded = drawing.bounds.insetBy(dx: -160, dy: -160)
        return CGRect(
            x: min(0, padded.origin.x),
            y: min(0, padded.origin.y),
            width: max(2400, padded.width),
            height: max(1800, padded.height)
        )
    }

    private func renderImage(drawing: PKDrawing, bounds: CGRect, scale: CGFloat) -> UIImage {
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = scale
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: bounds.size, format: format)
        return renderer.image { context in
            UIColor.white.setFill()
            context.fill(CGRect(origin: .zero, size: bounds.size))
            let image = drawing.image(from: bounds, scale: scale)
            image.draw(in: CGRect(origin: .zero, size: bounds.size))
        }
    }
}

@MainActor
private final class PencilSketchCanvasController {
    private weak var canvasView: PKCanvasView?

    var drawing: PKDrawing {
        canvasView?.drawing ?? PKDrawing()
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
        viewController.canvasView.drawingPolicy = .anyInput
        viewController.canvasView.tool = PKInkingTool(.pen, color: .black, width: 5)
        controller.attach(viewController.canvasView)
        context.coordinator.attachToolPicker(to: viewController.canvasView)
        return viewController
    }

    func updateUIViewController(_ viewController: PencilSketchViewController, context: Context) {
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
