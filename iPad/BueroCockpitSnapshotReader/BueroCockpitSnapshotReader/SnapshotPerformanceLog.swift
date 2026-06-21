import Foundation
import OSLog

enum SnapshotPerformanceLog {
    private static let logger = Logger(
        subsystem: "de.buerocockpit.ipad",
        category: "SnapshotPerformance"
    )

    static func event(_ message: String) {
#if DEBUG
        let uptime = ProcessInfo.processInfo.systemUptime
        let thread = Thread.isMainThread ? "main" : "background"
        logger.debug("Uptime: \(uptime, format: .fixed(precision: 3)), thread: \(thread, privacy: .public), event: \(message, privacy: .public)")
#endif
    }
}
