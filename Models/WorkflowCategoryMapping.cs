namespace BueroCockpit.Models;

public sealed record WorkflowCategoryMapping(
    string WorkflowType,
    string WorkflowStep,
    string CategoryId);
