import PencilKit
import SwiftUI
import UIKit

struct MobileSketchCanvasView: View {
    let onSave: (MobileInspectionSketchInput) -> Void
    let onCancel: () -> Void

    @State private var drawing = PKDrawing()
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            VStack(spacing: 10) {
                PencilSketchCanvas(drawing: $drawing)
                    .background(.white)
                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                    .overlay(
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
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
                        drawing = PKDrawing()
                        errorMessage = nil
                    }
                    .disabled(drawing.strokes.isEmpty)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Hinzufügen") {
                        saveSketch()
                    }
                    .disabled(drawing.strokes.isEmpty)
                }
            }
        }
    }

    private func saveSketch() {
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

private struct PencilSketchCanvas: UIViewRepresentable {
    @Binding var drawing: PKDrawing

    func makeCoordinator() -> Coordinator {
        Coordinator(drawing: $drawing)
    }

    func makeUIView(context: Context) -> PKCanvasView {
        let canvasView = PKCanvasView()
        canvasView.backgroundColor = .white
        canvasView.isOpaque = true
        canvasView.drawing = drawing
        canvasView.delegate = context.coordinator
        canvasView.drawingPolicy = .anyInput
        canvasView.alwaysBounceVertical = true
        canvasView.alwaysBounceHorizontal = true
        canvasView.minimumZoomScale = 0.35
        canvasView.maximumZoomScale = 6
        canvasView.contentSize = CGSize(width: 3000, height: 2200)
        canvasView.tool = PKInkingTool(.pen, color: .black, width: 5)
        canvasView.becomeFirstResponder()
        context.coordinator.attachToolPicker(to: canvasView)
        return canvasView
    }

    func updateUIView(_ canvasView: PKCanvasView, context: Context) {
        if canvasView.drawing != drawing {
            canvasView.drawing = drawing
        }
        context.coordinator.attachToolPicker(to: canvasView)
    }

    final class Coordinator: NSObject, PKCanvasViewDelegate {
        private var drawing: Binding<PKDrawing>
        private weak var currentCanvasView: PKCanvasView?
        private var toolPicker: PKToolPicker?

        init(drawing: Binding<PKDrawing>) {
            self.drawing = drawing
        }

        func canvasViewDrawingDidChange(_ canvasView: PKCanvasView) {
            drawing.wrappedValue = canvasView.drawing
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
