import SwiftUI
import UIKit

struct MobileSketchCanvasView: View {
    let onSave: (Data) -> Void
    let onCancel: () -> Void

    @State private var strokes: [[CGPoint]] = []
    @State private var currentStroke: [CGPoint] = []
    @State private var canvasSize: CGSize = CGSize(width: 800, height: 520)
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            VStack(spacing: 12) {
                drawingSurface

                if let errorMessage {
                    Label(errorMessage, systemImage: "exclamationmark.triangle")
                        .foregroundStyle(.orange)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
            .padding()
            .navigationTitle("Skizze")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Abbrechen", action: onCancel)
                }
                ToolbarItem(placement: .primaryAction) {
                    Button("Leeren") {
                        strokes = []
                        currentStroke = []
                        errorMessage = nil
                    }
                    .disabled(strokes.isEmpty && currentStroke.isEmpty)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Hinzufügen") {
                        saveSketch()
                    }
                    .disabled(strokes.isEmpty && currentStroke.isEmpty)
                }
            }
        }
    }

    private var drawingSurface: some View {
        GeometryReader { proxy in
            Canvas { context, size in
                context.fill(Path(CGRect(origin: .zero, size: size)), with: .color(.white))
                for stroke in strokes + [currentStroke] {
                    draw(stroke, in: &context)
                }
            }
            .background(.white)
            .overlay(
                RoundedRectangle(cornerRadius: 8)
                    .stroke(Color.secondary.opacity(0.3), lineWidth: 1)
            )
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in
                        let point = clamped(value.location, in: proxy.size)
                        if currentStroke.isEmpty {
                            currentStroke = [point]
                        } else {
                            currentStroke.append(point)
                        }
                        canvasSize = proxy.size
                    }
                    .onEnded { _ in
                        guard !currentStroke.isEmpty else { return }
                        strokes.append(currentStroke)
                        currentStroke = []
                        canvasSize = proxy.size
                    }
            )
            .onAppear {
                canvasSize = proxy.size
            }
        }
        .frame(minHeight: 360)
    }

    private func draw(_ stroke: [CGPoint], in context: inout GraphicsContext) {
        guard let firstPoint = stroke.first else {
            return
        }

        var path = Path()
        path.move(to: firstPoint)
        for point in stroke.dropFirst() {
            path.addLine(to: point)
        }
        if stroke.count == 1 {
            path.addEllipse(in: CGRect(x: firstPoint.x - 1, y: firstPoint.y - 1, width: 2, height: 2))
        }

        context.stroke(path, with: .color(.black), style: StrokeStyle(lineWidth: 3, lineCap: .round, lineJoin: .round))
    }

    private func saveSketch() {
        let completeStrokes = strokes + (currentStroke.isEmpty ? [] : [currentStroke])
        guard !completeStrokes.isEmpty else {
            errorMessage = "Bitte zuerst eine Skizze zeichnen."
            return
        }

        guard canvasSize.width > 0, canvasSize.height > 0 else {
            errorMessage = "Die Skizze konnte nicht vorbereitet werden."
            return
        }

        guard let data = renderPNG(strokes: completeStrokes, size: canvasSize), !data.isEmpty else {
            errorMessage = "Die Skizze konnte nicht als PNG gespeichert werden."
            return
        }

        onSave(data)
    }

    private func renderPNG(strokes: [[CGPoint]], size: CGSize) -> Data? {
        let format = UIGraphicsImageRendererFormat.default()
        format.scale = 2
        format.opaque = true
        let renderer = UIGraphicsImageRenderer(size: size, format: format)
        let image = renderer.image { context in
            UIColor.white.setFill()
            context.fill(CGRect(origin: .zero, size: size))
            UIColor.black.setStroke()

            let cgContext = context.cgContext
            cgContext.setLineWidth(3)
            cgContext.setLineCap(.round)
            cgContext.setLineJoin(.round)

            for stroke in strokes {
                guard let firstPoint = stroke.first else {
                    continue
                }
                cgContext.beginPath()
                cgContext.move(to: firstPoint)
                for point in stroke.dropFirst() {
                    cgContext.addLine(to: point)
                }
                if stroke.count == 1 {
                    cgContext.addEllipse(in: CGRect(x: firstPoint.x - 1, y: firstPoint.y - 1, width: 2, height: 2))
                    cgContext.fillPath()
                } else {
                    cgContext.strokePath()
                }
            }
        }
        return image.pngData()
    }

    private func clamped(_ point: CGPoint, in size: CGSize) -> CGPoint {
        CGPoint(
            x: min(max(point.x, 0), size.width),
            y: min(max(point.y, 0), size.height)
        )
    }
}
