namespace BueroCockpit.Data;

public static class AppPaths
{
    public static string DefaultAppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BueroCockpit");

    public static string AppDataDirectory { get; private set; } = DefaultAppDataDirectory;
    public static string BootstrapSettingsPath => Path.Combine(DefaultAppDataDirectory, "storage-location.json");
    public static string DatabasePath => Path.Combine(AppDataDirectory, "buerocockpit.db");
    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");
    public static string TasksDirectory => Path.Combine(AppDataDirectory, "Tasks");
    public static string BackupDirectory => Path.Combine(AppDataDirectory, "Backups");

    public static bool IsUsingCustomDataDirectory =>
        !string.Equals(AppDataDirectory, DefaultAppDataDirectory, StringComparison.Ordinal);

    public static void UseDefaultAppDataDirectory()
    {
        AppDataDirectory = DefaultAppDataDirectory;
    }

    public static void UseAppDataDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            UseDefaultAppDataDirectory();
            return;
        }

        AppDataDirectory = Path.GetFullPath(ExpandHomeDirectory(Environment.ExpandEnvironmentVariables(directory.Trim())));
    }

    public static string GetAttachmentDirectory(string taskId)
    {
        return Path.Combine(AppDataDirectory, "Tasks", taskId, "Attachments");
    }

    public static string GetAttachmentBackupDirectory(string taskId)
    {
        return Path.Combine(AppDataDirectory, "Tasks", taskId, "AttachmentBackups");
    }

    public static void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(DefaultAppDataDirectory);
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(TasksDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            path.StartsWith($"~{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }
}
