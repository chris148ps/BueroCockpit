using BueroCockpit.Models;

namespace BueroCockpit.Services;

public static class MobileTaskRevisionService
{
    public const string NotesField = "notes";
    public const string CategoryField = "categoryId";
    public const string WorkflowField = "workflow";
    public const string DueDateField = "dueDate";
    public const string FollowUpDateField = "followUpDate";
    public const string FollowUpReasonField = "followUpReason";
    public const string TechnicianField = "technician";

    public static IReadOnlyList<MobileTaskFieldChange> BuildChanges(MobileInboxEntry entry, TaskItem desktopTask)
    {
        if (!entry.IsDesktopUpdate || entry.BaseValues is null)
        {
            return Array.Empty<MobileTaskFieldChange>();
        }

        var basis = entry.BaseValues;
        var changes = new List<MobileTaskFieldChange>();
        AddTextChange(changes, NotesField, "Notiz", basis.Notes, desktopTask.Description, entry.Notes);
        AddTextChange(changes, CategoryField, "Kategorie", basis.CategoryId, desktopTask.CategoryId, entry.CategoryId);
        AddWorkflowChange(changes, basis, desktopTask, entry);
        AddDateChange(changes, DueDateField, "Termin", basis.DueDate, desktopTask.DueDate, entry.DueDate);
        AddDateChange(changes, FollowUpDateField, "Wiedervorlage", basis.FollowUpDate, desktopTask.FollowUpDate, entry.FollowUpDate);
        AddTextChange(changes, FollowUpReasonField, "Wiedervorlagegrund", basis.FollowUpReason, desktopTask.FollowUpReason, entry.FollowUpReason);
        AddTextChange(changes, TechnicianField, "Monteur", basis.Technician, desktopTask.Technician, entry.Technician);
        return changes;
    }

    public static bool RevisionMatches(string? baseRevision, DateTime desktopRevision)
    {
        return DateTimeOffset.TryParse(baseRevision, out var parsed) &&
               Math.Abs((parsed.LocalDateTime - desktopRevision).TotalSeconds) < 1;
    }

    private static void AddTextChange(
        ICollection<MobileTaskFieldChange> changes,
        string field,
        string label,
        string? baseValue,
        string? desktopValue,
        string? mobileValue)
    {
        var normalizedBase = NormalizeText(baseValue);
        var normalizedDesktop = NormalizeText(desktopValue);
        var normalizedMobile = NormalizeText(mobileValue);
        if (string.Equals(normalizedBase, normalizedMobile, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new MobileTaskFieldChange(
            field,
            label,
            DisplayText(normalizedBase),
            DisplayText(normalizedDesktop),
            DisplayText(normalizedMobile),
            !string.Equals(normalizedBase, normalizedDesktop, StringComparison.Ordinal) &&
            !string.Equals(normalizedDesktop, normalizedMobile, StringComparison.Ordinal)));
    }

    private static void AddWorkflowChange(
        ICollection<MobileTaskFieldChange> changes,
        MobileTaskRevisionValues basis,
        TaskItem desktopTask,
        MobileInboxEntry entry)
    {
        var baseValue = JoinWorkflow(basis.WorkflowType, basis.WorkflowStep);
        var desktopValue = JoinWorkflow(desktopTask.WorkflowType, desktopTask.WorkflowStep);
        var mobileValue = JoinWorkflow(entry.WorkflowType, entry.WorkflowStep);
        if (string.Equals(baseValue, mobileValue, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new MobileTaskFieldChange(
            WorkflowField,
            "Vorgangstyp / Status",
            DisplayText(baseValue),
            DisplayText(desktopValue),
            DisplayText(mobileValue),
            !string.Equals(baseValue, desktopValue, StringComparison.Ordinal) &&
            !string.Equals(desktopValue, mobileValue, StringComparison.Ordinal)));
    }

    private static void AddDateChange(
        ICollection<MobileTaskFieldChange> changes,
        string field,
        string label,
        DateTime? baseValue,
        DateTime? desktopValue,
        DateTime? mobileValue)
    {
        var normalizedBase = baseValue?.Date;
        var normalizedDesktop = desktopValue?.Date;
        var normalizedMobile = mobileValue?.Date;
        if (normalizedBase == normalizedMobile)
        {
            return;
        }

        changes.Add(new MobileTaskFieldChange(
            field,
            label,
            DisplayDate(normalizedBase),
            DisplayDate(normalizedDesktop),
            DisplayDate(normalizedMobile),
            normalizedBase != normalizedDesktop && normalizedDesktop != normalizedMobile));
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    private static string JoinWorkflow(string? workflowType, string? workflowStep) =>
        $"{NormalizeText(workflowType)}\n{NormalizeText(workflowStep)}";

    private static string DisplayText(string value) =>
        string.IsNullOrWhiteSpace(value) ? "— leer —" : value.Replace("\n", " · ", StringComparison.Ordinal);

    private static string DisplayDate(DateTime? value) => value?.ToString("dd.MM.yyyy") ?? "— kein Datum —";
}

public sealed record MobileTaskFieldChange(
    string Field,
    string Label,
    string BaseValue,
    string DesktopValue,
    string MobileValue,
    bool HasConflict);
