namespace BueroCockpit.Models;

public sealed class WorkflowStepItem
{
    public WorkflowStepItem(string name, int position, bool isCompleted, bool isCurrent)
    {
        Name = name;
        Position = position;
        IsCompleted = isCompleted;
        IsCurrent = isCurrent;
    }

    public string Name { get; }
    public int Position { get; }
    public bool IsCompleted { get; }
    public bool IsCurrent { get; }
    public bool IsFuture => !IsCompleted && !IsCurrent;
    public bool HasLeadingConnector => Position > 0;
    public bool IsConnectorActive => IsCompleted || IsCurrent;
    public string Glyph => IsCompleted ? "✓" : IsCurrent ? "●" : Position.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string StateText => IsCompleted ? "abgeschlossen" : IsCurrent ? "aktuell" : "zukünftig";
    public string AutomationName => $"{Name}, Schritt {Position}, {StateText}";
}
