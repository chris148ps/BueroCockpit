namespace BueroCockpit.Models;

public sealed class WorkflowStepItem
{
    public WorkflowStepItem(string name, bool isCompleted, bool isCurrent)
    {
        Name = name;
        IsCompleted = isCompleted;
        IsCurrent = isCurrent;
    }

    public string Name { get; }
    public bool IsCompleted { get; }
    public bool IsCurrent { get; }
}
