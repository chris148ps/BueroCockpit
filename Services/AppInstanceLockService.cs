using System.Diagnostics;
using System.Text.Json;
using BueroCockpit.Data;

namespace BueroCockpit.Services;

public sealed class AppInstanceLockService
{
    private static readonly TimeSpan StaleLockAge = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private AppInstanceLockInfo? _currentLock;

    public AppInstanceLockResult Acquire()
    {
        var lockPath = AppPaths.LockPath;

        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);

            if (File.Exists(lockPath))
            {
                var existingLock = ReadLock(lockPath);
                var lastWriteTime = File.GetLastWriteTimeUtc(lockPath);
                if (DateTimeOffset.UtcNow - lastWriteTime <= StaleLockAge)
                {
                    return AppInstanceLockResult.Warning(
                        lockPath,
                        "BüroCockpit scheint auf einem anderen Gerät bereits mit diesem Datenordner geöffnet zu sein.",
                        existingLock);
                }
            }

            _currentLock = CreateCurrentLock();
            WriteLock(lockPath, _currentLock);
            return AppInstanceLockResult.Acquired(lockPath, "Datenordner-Zugriffsschutz ist aktiv.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App instance lock could not be acquired: {ex}");
            return AppInstanceLockResult.Warning(
                lockPath,
                "Datenordner-Sperre konnte nicht geschrieben werden. Bitte BüroCockpit nicht gleichzeitig auf mehreren Geräten öffnen.",
                null);
        }
    }

    public void Release()
    {
        if (_currentLock is null)
        {
            return;
        }

        var lockPath = AppPaths.LockPath;
        try
        {
            if (!File.Exists(lockPath))
            {
                return;
            }

            var existingLock = ReadLock(lockPath);
            if (existingLock is null || !MatchesCurrentProcess(existingLock))
            {
                return;
            }

            File.Delete(lockPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App instance lock could not be released: {ex}");
        }
        finally
        {
            _currentLock = null;
        }
    }

    private static AppInstanceLockInfo CreateCurrentLock()
    {
        return new AppInstanceLockInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.Now,
            AppDataDirectory = AppPaths.AppDataDirectory
        };
    }

    private static void WriteLock(string lockPath, AppInstanceLockInfo lockInfo)
    {
        var json = JsonSerializer.Serialize(lockInfo, Options);
        File.WriteAllText(lockPath, json);
    }

    private static AppInstanceLockInfo? ReadLock(string lockPath)
    {
        try
        {
            var json = File.ReadAllText(lockPath);
            return JsonSerializer.Deserialize<AppInstanceLockInfo>(json, Options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App instance lock could not be read: {ex}");
            return null;
        }
    }

    private static bool MatchesCurrentProcess(AppInstanceLockInfo lockInfo)
    {
        return string.Equals(lockInfo.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase) &&
               lockInfo.ProcessId == Environment.ProcessId;
    }
}

public sealed class AppInstanceLockInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string AppDataDirectory { get; set; } = string.Empty;
}

public sealed record AppInstanceLockResult(
    bool IsAcquired,
    string LockPath,
    string Message,
    AppInstanceLockInfo? ExistingLock)
{
    public static AppInstanceLockResult Acquired(string lockPath, string message)
    {
        return new AppInstanceLockResult(true, lockPath, message, null);
    }

    public static AppInstanceLockResult Warning(string lockPath, string message, AppInstanceLockInfo? existingLock)
    {
        return new AppInstanceLockResult(false, lockPath, message, existingLock);
    }
}
