import Foundation

enum SnapshotPerformanceLog {
    static func event(_ message: String) {
#if DEBUG
        let uptime = ProcessInfo.processInfo.systemUptime
        let thread = Thread.isMainThread ? "main" : "background"
        print(String(format: "[SnapshotPerformance %.3f] [%@] %@", uptime, thread, message))
#endif
    }
}
