namespace BueroCockpit.Models;

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _customerName = string.Empty;
    private string _customerAddress = string.Empty;
    private string _description = string.Empty;
    private string _categoryId = string.Empty;
    private string _status = "Offen";
    private string _priority = "Normal";
    private DateTime? _dueDate;
    private DateTime? _followUpDate;
    private DateTime? _sentAt;
    private string _assignedTo = string.Empty;
    private string _technician = string.Empty;
    private DateTime? _completedAt;
    private double _sortPosition;
    private string _categoryHint = string.Empty;
    private bool _showCategoryHint;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string CustomerAddress
    {
        get => _customerAddress;
        set
        {
            if (SetProperty(ref _customerAddress, value))
            {
                OnPropertyChanged(nameof(HasCustomerAddress));
            }
        }
    }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string CategoryId { get => _categoryId; set => SetProperty(ref _categoryId, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public DateTime? DueDate { get => _dueDate; set => SetProperty(ref _dueDate, value); }
    public DateTime? FollowUpDate { get => _followUpDate; set => SetProperty(ref _followUpDate, value); }
    public DateTime? SentAt
    {
        get => _sentAt;
        set
        {
            if (SetProperty(ref _sentAt, value))
            {
                OnPropertyChanged(nameof(SentAtText));
                OnPropertyChanged(nameof(HasSentAt));
            }
        }
    }
    public string AssignedTo { get => _assignedTo; set => SetProperty(ref _assignedTo, value); }
    public string Technician
    {
        get => _technician;
        set
        {
            if (SetProperty(ref _technician, value))
            {
                OnPropertyChanged(nameof(HasTechnician));
            }
        }
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get => _completedAt; set => SetProperty(ref _completedAt, value); }
    public double SortPosition { get => _sortPosition; set => SetProperty(ref _sortPosition, value); }
    public string CategoryHint { get => _categoryHint; set => SetProperty(ref _categoryHint, value); }
    public bool ShowCategoryHint { get => _showCategoryHint; set => SetProperty(ref _showCategoryHint, value); }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(CardBackground));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }
    }

    public string DueDateText => DueDate?.ToString("dd.MM.yyyy") ?? "-";
    public string FollowUpDateText => FollowUpDate?.ToString("dd.MM.yyyy") ?? "-";
    public string SentAtText => SentAt?.ToString("dd.MM.yyyy") ?? "-";
    public bool HasTechnician => !string.IsNullOrWhiteSpace(Technician);
    public bool HasCustomerAddress => !string.IsNullOrWhiteSpace(CustomerAddress);
    public bool HasSentAt => SentAt.HasValue;
    public string CardBackground => IsSelected ? "#EEF5FF" : "#FFFFFF";
    public string CardBorderBrush => IsSelected ? "#7CB7FF" : "#E0E0E5";
}
