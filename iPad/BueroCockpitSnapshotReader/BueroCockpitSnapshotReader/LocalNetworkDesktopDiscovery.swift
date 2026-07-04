import Darwin
import Foundation

struct LocalNetworkDiscoveredDesktop: Identifiable, Equatable, Sendable {
    let id: String
    let name: String
    let hostName: String?
    let address: String
    let port: Int
    let deviceId: String?

    var displayEndpoint: String {
        "\(address):\(port)"
    }
}

final class LocalNetworkDesktopDiscovery: NSObject, @unchecked Sendable, NetServiceBrowserDelegate, NetServiceDelegate {
    private let browser = NetServiceBrowser()
    private var services: [String: NetService] = [:]
    private var discoveredByID: [String: LocalNetworkDiscoveredDesktop] = [:]
    private var onUpdate: (([LocalNetworkDiscoveredDesktop]) -> Void)?

    override init() {
        super.init()
        browser.delegate = self
    }

    func start(onUpdate: @escaping ([LocalNetworkDiscoveredDesktop]) -> Void) {
        stop()
        self.onUpdate = onUpdate
        browser.delegate = self
        browser.searchForServices(ofType: "_buerocockpit._tcp.", inDomain: "local.")
    }

    func stop() {
        browser.stop()
        for service in services.values {
            service.stop()
            service.delegate = nil
        }
        services.removeAll()
        discoveredByID.removeAll()
        publish()
    }

    func netServiceBrowser(
        _ browser: NetServiceBrowser,
        didFind service: NetService,
        moreComing: Bool
    ) {
        let id = Self.serviceID(service)
        services[id] = service
        service.delegate = self
        service.resolve(withTimeout: 5)
        if !moreComing {
            publish()
        }
    }

    func netServiceBrowser(
        _ browser: NetServiceBrowser,
        didRemove service: NetService,
        moreComing: Bool
    ) {
        let id = Self.serviceID(service)
        services[id]?.stop()
        services[id]?.delegate = nil
        services.removeValue(forKey: id)
        discoveredByID.removeValue(forKey: id)
        if !moreComing {
            publish()
        }
    }

    func netServiceDidResolveAddress(_ sender: NetService) {
        let id = Self.serviceID(sender)
        guard let desktop = Self.discoveredDesktop(from: sender, id: id) else { return }
        discoveredByID[id] = desktop
        publish()
    }

    func netService(_ sender: NetService, didNotResolve errorDict: [String: NSNumber]) {
        let id = Self.serviceID(sender)
        discoveredByID.removeValue(forKey: id)
        publish()
    }

    private func publish() {
        let desktops = discoveredByID.values.sorted {
            if $0.name == $1.name {
                return $0.displayEndpoint.localizedStandardCompare($1.displayEndpoint) == .orderedAscending
            }
            return $0.name.localizedStandardCompare($1.name) == .orderedAscending
        }
        onUpdate?(desktops)
    }

    private static func discoveredDesktop(from service: NetService, id: String) -> LocalNetworkDiscoveredDesktop? {
        guard service.port > 0 else { return nil }
        guard let address = service.addresses?.compactMap(Self.ipAddress(from:)).first else { return nil }
        let txtValues = txtValues(from: service.txtRecordData())
        guard txtValues["app"] == "BueroCockpit",
              txtValues["mode"] == "pairing-test" else {
            return nil
        }

        let announcedPort = txtValues["port"].flatMap(Int.init) ?? service.port
        return LocalNetworkDiscoveredDesktop(
            id: id,
            name: service.name,
            hostName: service.hostName,
            address: address,
            port: announcedPort,
            deviceId: txtValues["deviceId"]
        )
    }

    private static func txtValues(from data: Data?) -> [String: String] {
        guard let data else { return [:] }
        let records = NetService.dictionary(fromTXTRecord: data)
        var values: [String: String] = [:]
        for (key, value) in records {
            guard let stringValue = String(data: value, encoding: .utf8) else { continue }
            values[key] = stringValue
        }
        return values
    }

    private static func serviceID(_ service: NetService) -> String {
        "\(service.name)|\(service.type)|\(service.domain)"
    }

    private static func ipAddress(from data: Data) -> String? {
        var storage = sockaddr_storage()
        let copied = withUnsafeMutableBytes(of: &storage) { rawBuffer in
            data.copyBytes(to: rawBuffer)
        }
        guard copied >= MemoryLayout<sockaddr>.size else { return nil }

        switch Int32(storage.ss_family) {
        case AF_INET:
            var address = withUnsafePointer(to: &storage) {
                $0.withMemoryRebound(to: sockaddr_in.self, capacity: 1) { $0.pointee.sin_addr }
            }
            var buffer = [CChar](repeating: 0, count: Int(INET_ADDRSTRLEN))
            guard inet_ntop(AF_INET, &address, &buffer, socklen_t(INET_ADDRSTRLEN)) != nil else {
                return nil
            }
            return Self.string(fromNullTerminatedBuffer: buffer)

        case AF_INET6:
            var address = withUnsafePointer(to: &storage) {
                $0.withMemoryRebound(to: sockaddr_in6.self, capacity: 1) { $0.pointee.sin6_addr }
            }
            var buffer = [CChar](repeating: 0, count: Int(INET6_ADDRSTRLEN))
            guard inet_ntop(AF_INET6, &address, &buffer, socklen_t(INET6_ADDRSTRLEN)) != nil else {
                return nil
            }
            let value = Self.string(fromNullTerminatedBuffer: buffer)
            return value.hasPrefix("fe80:") ? nil : value

        default:
            return nil
        }
    }

    private static func string(fromNullTerminatedBuffer buffer: [CChar]) -> String {
        let endIndex = buffer.firstIndex(of: 0) ?? buffer.endIndex
        return String(decoding: buffer[..<endIndex].map(UInt8.init(bitPattern:)), as: UTF8.self)
    }
}
