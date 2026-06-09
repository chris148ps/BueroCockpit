namespace BueroCockpit.Data;

public static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BueroCockpit");

    public static string DatabasePath => Path.Combine(AppDataDirectory, "buerocockpit.db");

    public static string GetAttachmentDirectory(string taskId)
    {
        return Path.Combine(AppDataDirectory, "Tasks", taskId, "Attachments");
    }

    public static void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
    }
}
