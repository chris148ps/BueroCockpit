namespace BueroCockpit.Models;

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _customerName = string.Empty;
    private string _description = string.Empty;
    private string _categoryId = string.Empty;
    private string _status = "Offen";
    private string _priority = "Normal";
    private DateTime? _dueDate;
    private DateTime? _followUpDate;
    private string _assignedTo = string.Empty;
    private DateTime? _completedAt;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string CategoryId { get => _categoryId; set => SetProperty(ref _categoryId, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public DateTime? DueDate { get => _dueDate; set => SetProperty(ref _dueDate, value); }
    public DateTime? FollowUpDate { get => _followUpDate; set => SetProperty(ref _followUpDate, value); }
    public string AssignedTo { get => _assignedTo; set => SetProperty(ref _assignedTo, value); }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get => _completedAt; set => SetProperty(ref _completedAt, value); }

    public string DueDateText => DueDate?.ToString("dd.MM.yyyy") ?? "-";
    public string FollowUpDateText => FollowUpDate?.ToString("dd.MM.yyyy") ?? "-";
}
