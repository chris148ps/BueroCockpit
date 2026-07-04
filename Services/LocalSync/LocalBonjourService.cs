using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace BueroCockpit.Services.LocalSync;

internal sealed class LocalBonjourService : IDisposable
{
    public const string ServiceType = "_buerocockpit._tcp";
    private const string UnavailableMessage = "Bonjour nicht verfuegbar; manuelle IP-Eingabe verwenden.";
    private const string WindowsRequiredMessage = "Bonjour fuer automatische Desktop-Suche auf Windows erforderlich.";

    private static readonly DNSServiceRegisterReply RegisterReply = OnRegisterReply;
    private readonly object _gate = new();
    private IntPtr _serviceRef;
    private CancellationTokenSource? _processCts;
    private Task? _processTask;

    public string? LastError { get; private set; }
    public bool IsAvailable { get; private set; }
    public bool IsRunning { get; private set; }

    public bool Start(LocalSyncOptions options)
    {
        var availability = GetAvailabilityStatus();
        if (!availability.CanPublish)
        {
            LastError = availability.DisplayText;
            IsAvailable = false;
            IsRunning = false;
            return false;
        }

        lock (_gate)
        {
            StopLocked();

            var serviceName = BuildServiceName(options.DeviceName);
            var txtRecord = BuildTxtRecord(options);
            var port = (ushort)IPAddress.HostToNetworkOrder((short)options.Port);
            DNSServiceErrorType error;
            try
            {
                error = DNSServiceRegister(
                    out _serviceRef,
                    0,
                    0,
                    serviceName,
                    ServiceType,
                    null,
                    null,
                    port,
                    (ushort)txtRecord.Length,
                    txtRecord,
                    RegisterReply,
                    IntPtr.Zero);
            }
            catch (Exception ex) when (IsNativeBonjourException(ex))
            {
                _serviceRef = IntPtr.Zero;
                LastError = UnavailableMessage;
                IsAvailable = false;
                IsRunning = false;
                return false;
            }
            catch
            {
                _serviceRef = IntPtr.Zero;
                LastError = UnavailableMessage;
                IsAvailable = false;
                IsRunning = false;
                return false;
            }

            if (error != 0)
            {
                _serviceRef = IntPtr.Zero;
                LastError = UnavailableMessage;
                IsAvailable = false;
                IsRunning = false;
                return false;
            }

            _processCts = new CancellationTokenSource();
            var token = _processCts.Token;
            _processTask = Task.Run(() => ProcessResults(_serviceRef, token), CancellationToken.None);
            LastError = null;
            IsAvailable = true;
            IsRunning = true;
            return true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void StopLocked()
    {
        _processCts?.Cancel();
        _processCts?.Dispose();
        _processCts = null;
        IsRunning = false;

        if (_serviceRef != IntPtr.Zero)
        {
            try
            {
                DNSServiceRefDeallocate(_serviceRef);
            }
            catch (Exception ex) when (IsNativeBonjourException(ex))
            {
                LastError = UnavailableMessage;
                IsAvailable = false;
            }
            catch
            {
                LastError = UnavailableMessage;
                IsAvailable = false;
            }

            _serviceRef = IntPtr.Zero;
        }

        _processTask = null;
    }

    private static string BuildServiceName(string? deviceName)
    {
        var trimmedDeviceName = deviceName?.Trim();
        return string.IsNullOrWhiteSpace(trimmedDeviceName)
            ? "BueroCockpit"
            : $"BueroCockpit-{trimmedDeviceName}";
    }

    private static byte[] BuildTxtRecord(LocalSyncOptions options)
    {
        var entries = new List<string>
        {
            "app=BueroCockpit",
            "mode=local-network-test",
            $"port={options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        };

        if (!string.IsNullOrWhiteSpace(options.DeviceId))
        {
            entries.Add($"deviceId={options.DeviceId.Trim()}");
        }

        using var stream = new MemoryStream();
        foreach (var entry in entries)
        {
            var bytes = Encoding.UTF8.GetBytes(entry);
            if (bytes.Length > byte.MaxValue)
            {
                continue;
            }

            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    private void ProcessResults(IntPtr serviceRef, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && serviceRef != IntPtr.Zero)
            {
                var error = DNSServiceProcessResult(serviceRef);
                if (error != 0)
                {
                    LastError = UnavailableMessage;
                    IsAvailable = false;
                    break;
                }
            }
        }
        catch (Exception ex) when (IsNativeBonjourException(ex))
        {
            LastError = UnavailableMessage;
            IsAvailable = false;
        }
        catch
        {
            LastError = UnavailableMessage;
            IsAvailable = false;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static void OnRegisterReply(
        IntPtr serviceRef,
        DNSServiceFlags flags,
        DNSServiceErrorType errorCode,
        string name,
        string regtype,
        string domain,
        IntPtr context)
    {
    }

    private static bool IsNativeBonjourException(Exception ex)
    {
        return ex is DllNotFoundException
            or EntryPointNotFoundException
            or BadImageFormatException
            or MarshalDirectiveException
            or SEHException
            or ExternalException
            or InvalidOperationException;
    }

    public static BonjourAvailabilityStatus GetAvailabilityStatus()
    {
        if (OperatingSystem.IsWindows())
        {
            var serviceStatus = GetWindowsMdnsResponderStatus();
            var hasDnsSd = TryLoadDnsSdLibrary("dns_sd.dll") || TryLoadDnsSdLibrary("dns_sd");
            if (serviceStatus == WindowsServiceStatus.Running && hasDnsSd)
            {
                return new BonjourAvailabilityStatus(true, true, "Bonjour aktiv");
            }

            if (serviceStatus == WindowsServiceStatus.NotInstalled && !hasDnsSd)
            {
                return new BonjourAvailabilityStatus(false, false, $"{UnavailableMessage} {WindowsRequiredMessage}");
            }

            return new BonjourAvailabilityStatus(false, hasDnsSd, $"{UnavailableMessage} {WindowsRequiredMessage}");
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryLoadDnsSdLibrary("dns_sd")
                ? new BonjourAvailabilityStatus(true, true, "Bonjour aktiv")
                : new BonjourAvailabilityStatus(false, false, UnavailableMessage);
        }

        return new BonjourAvailabilityStatus(false, false, UnavailableMessage);
    }

    private static bool TryLoadDnsSdLibrary(string libraryName)
    {
        try
        {
            if (!NativeLibrary.TryLoad(libraryName, out var handle))
            {
                return false;
            }

            NativeLibrary.Free(handle);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static WindowsServiceStatus GetWindowsMdnsResponderStatus()
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "query mDNSResponder",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!process.Start())
            {
                return WindowsServiceStatus.Unknown;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return WindowsServiceStatus.Unknown;
            }

            if (process.ExitCode != 0)
            {
                return WindowsServiceStatus.NotInstalled;
            }

            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
                ? WindowsServiceStatus.Running
                : WindowsServiceStatus.Stopped;
        }
        catch
        {
            return WindowsServiceStatus.Unknown;
        }
    }

    [DllImport("dns_sd", EntryPoint = "DNSServiceRegister", CallingConvention = CallingConvention.Cdecl)]
    private static extern DNSServiceErrorType DNSServiceRegister(
        out IntPtr sdRef,
        DNSServiceFlags flags,
        uint interfaceIndex,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string regtype,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? domain,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? host,
        ushort port,
        ushort txtLen,
        byte[] txtRecord,
        DNSServiceRegisterReply callBack,
        IntPtr context);

    [DllImport("dns_sd", EntryPoint = "DNSServiceProcessResult", CallingConvention = CallingConvention.Cdecl)]
    private static extern DNSServiceErrorType DNSServiceProcessResult(IntPtr sdRef);

    [DllImport("dns_sd", EntryPoint = "DNSServiceRefDeallocate", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DNSServiceRefDeallocate(IntPtr sdRef);

    private delegate void DNSServiceRegisterReply(
        IntPtr sdRef,
        DNSServiceFlags flags,
        DNSServiceErrorType errorCode,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string regtype,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string domain,
        IntPtr context);

    [Flags]
    private enum DNSServiceFlags : uint
    {
        None = 0
    }

    private enum DNSServiceErrorType : int
    {
    }

    private enum WindowsServiceStatus
    {
        Unknown,
        NotInstalled,
        Stopped,
        Running
    }
}

internal sealed record BonjourAvailabilityStatus(bool CanPublish, bool DnsSdLibraryAvailable, string DisplayText);
