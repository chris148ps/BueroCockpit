using BueroCockpit.Models;

namespace BueroCockpit.Services;

public static class WorkflowCategoryService
{
    public const string OfferWorkflowType = "Angebotsvorgang";
    public const string DirectWorkflowType = "Direktauftrag";

    public static readonly IReadOnlyList<string> OfferWorkflowSteps =
        ["Ansicht", "Angebot", "Angebot gesendet", "Auftrag", "Material", "Termin", "Erledigt"];

    public static readonly IReadOnlyList<string> DirectWorkflowSteps =
        ["Auftrag", "Material", "Termin", "Erledigt"];

    public static IReadOnlyList<string> GetSteps(string? workflowType) =>
        string.Equals(workflowType, OfferWorkflowType, StringComparison.OrdinalIgnoreCase)
            ? OfferWorkflowSteps
            : DirectWorkflowSteps;

    public static string GetInitialStep(string? workflowType) =>
        string.Equals(workflowType, OfferWorkflowType, StringComparison.OrdinalIgnoreCase)
            ? "Angebot"
            : "Auftrag";

    public static bool IsValidStep(string? workflowType, string? workflowStep)
    {
        if (string.IsNullOrWhiteSpace(workflowStep))
        {
            return false;
        }

        return GetSteps(workflowType).Contains(workflowStep.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizeStep(string? workflowType, string? workflowStep)
    {
        var trimmed = workflowStep?.Trim() ?? string.Empty;
        var canonical = GetSteps(workflowType).FirstOrDefault(step =>
            string.Equals(step, trimmed, StringComparison.OrdinalIgnoreCase));
        if (canonical is not null)
        {
            return canonical;
        }

        return trimmed.ToLowerInvariant() switch
        {
            "offen" => GetInitialStep(workflowType),
            "material offen" => "Material",
            "terminiert" => "Termin",
            _ => trimmed
        };
    }

    public static void ApplyCategory(TaskItem task, string categoryId)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw new ArgumentException("Eine Workflowzuordnung benötigt eine Kategorie-ID.", nameof(categoryId));
        }

        var normalizedCategoryId = categoryId.Trim();
        task.CategoryId = normalizedCategoryId;
        task.CategoryIds = [normalizedCategoryId];
    }
}
