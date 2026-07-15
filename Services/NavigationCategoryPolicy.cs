namespace BueroCockpit.Services;

public static class NavigationCategoryPolicy
{
    public const string OverviewId = "__overview";
    public const string AllTasksId = "__all_tasks";
    public const string DeskId = "__desk";
    public const string TrashId = "__trash";
    public const string SettingsId = "__settings";
    public const string MobileInboxId = "__mobile_inbox";

    public static readonly IReadOnlyList<(string Id, string Name)> PrimaryNavigation =
    [
        (OverviewId, "Übersicht"),
        (AllTasksId, "Alle Vorgänge")
    ];

    private static readonly HashSet<string> TechnicalIds = new(StringComparer.OrdinalIgnoreCase)
    {
        OverviewId,
        AllTasksId,
        DeskId,
        TrashId,
        SettingsId,
        MobileInboxId
    };

    private static readonly HashSet<string> LegacyWorkIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "__orders",
        "__offers",
        "__materials",
        "__appointments"
    };

    public static bool IsTechnicalId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TechnicalIds.Contains(id);

    public static bool IsLegacyWorkId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && LegacyWorkIds.Contains(id);
}
