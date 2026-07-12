namespace BueroCockpit.Models;

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _customerName = string.Empty;
    private string _customerAddress = string.Empty;
    private string _customerEmail = string.Empty;
    private string _customerPhone = string.Empty;
    private string _description = string.Empty;
    private string _categoryId = string.Empty;
    private string _status = "Offen";
    private string _workflowType = string.Empty;
    private string _workflowStep = string.Empty;
    private string _priority = "Normal";
    private DateTime? _dueDate;
    private DateTime? _followUpDate;
    private DateTime? _sentAt;
    private DateTime? _materialOrderedAt;
    private string _assignedTo = string.Empty;
    private string _technician = string.Empty;
    private DateTime? _completedAt;
    private bool _isDeleted;
    private DateTime? _deletedAt;
    private double _sortPosition;
    private string _categoryHint = string.Empty;
    private List<string> _categoryNameChips = new();
    private bool _showCategoryHint;
    private bool _isSelected;
    private bool _isMobileInboxCard;
    private string _mobileInboxCreatedAtText = string.Empty;
    private string _mobileInboxAttachmentOverviewText = string.Empty;
    private string _mobileInboxStatusBadgeBackground = "#F4F1EA";
    private string _mobileInboxStatusBadgeBorderBrush = "#DAD4C7";
    private string _mobileInboxStatusBadgeForeground = "#6E6255";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayNumber => $"V-{Id[..Math.Min(8, Id.Length)].ToUpperInvariant()}";
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
    public string CustomerEmail { get => _customerEmail; set => SetProperty(ref _customerEmail, value); }
    public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }
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
    public List<string> CategoryIds { get; set; } = new();
    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(WorkflowStatusText));
            }
        }
    }
    public string WorkflowType { get => _workflowType; set => SetProperty(ref _workflowType, value); }
    public string WorkflowStep
    {
        get => _workflowStep;
        set
        {
            if (SetProperty(ref _workflowStep, value))
            {
                OnPropertyChanged(nameof(WorkflowStatusText));
            }
        }
    }
    public string WorkflowStatusText => string.IsNullOrWhiteSpace(WorkflowStep) ? Status : WorkflowStep;
    public string Priority { get => _priority; set => SetProperty(ref _priority, value); }
    public DateTime? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetProperty(ref _dueDate, value))
            {
                OnPropertyChanged(nameof(DueDateText));
                OnPropertyChanged(nameof(DueDateCompactText));
                OnPropertyChanged(nameof(DueDateOverviewText));
                OnPropertyChanged(nameof(HasDueDate));
            }
        }
    }

    public DateTime? FollowUpDate
    {
        get => _followUpDate;
        set
        {
            if (SetProperty(ref _followUpDate, value))
            {
                OnPropertyChanged(nameof(FollowUpDateText));
                OnPropertyChanged(nameof(HasFollowUpDate));
                OnPropertyChanged(nameof(IsFollowUpOverdue));
                OnPropertyChanged(nameof(ReminderCardBackground));
                OnPropertyChanged(nameof(ReminderCardBorderBrush));
            }
        }
    }
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
    public DateTime? MaterialOrderedAt
    {
        get => _materialOrderedAt;
        set
        {
            if (SetProperty(ref _materialOrderedAt, value))
            {
                OnPropertyChanged(nameof(MaterialOrderedAtText));
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
    public bool IsDeleted { get => _isDeleted; set => SetProperty(ref _isDeleted, value); }
    public DateTime? DeletedAt { get => _deletedAt; set => SetProperty(ref _deletedAt, value); }
    public double SortPosition { get => _sortPosition; set => SetProperty(ref _sortPosition, value); }
    public string CategoryHint { get => _categoryHint; set => SetProperty(ref _categoryHint, value); }
    public List<string> CategoryNameChips
    {
        get => _categoryNameChips;
        set
        {
            if (SetProperty(ref _categoryNameChips, value ?? new List<string>()))
            {
                OnPropertyChanged(nameof(HasCategoryNameChips));
            }
        }
    }
    public bool HasCategoryNameChips => CategoryNameChips.Count > 0;
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
    public string DueDateCompactText => DueDate is null
        ? string.Empty
        : DueDate.Value.TimeOfDay == TimeSpan.Zero
            ? DueDate.Value.ToString("dd.MM.yyyy")
            : DueDate.Value.ToString("dd.MM.yyyy HH:mm");
    public string DueDateOverviewText => DueDate is null
        ? "-"
        : DueDate.Value.TimeOfDay == TimeSpan.Zero
            ? DueDate.Value.ToString("dddd, dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))
            : DueDate.Value.ToString("dddd, dd.MM.yyyy, HH:mm 'Uhr'", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
    public string FollowUpDateText => FollowUpDate?.ToString("dd.MM.yyyy") ?? "-";
    public string SentAtText => SentAt?.ToString("dd.MM.yyyy") ?? "-";
    public string MaterialOrderedAtText => MaterialOrderedAt?.ToString("dd.MM.yyyy") ?? "-";
    public bool HasDueDate => DueDate.HasValue;
    public bool HasFollowUpDate => FollowUpDate.HasValue;
    public bool IsFollowUpOverdue => FollowUpDate.HasValue && FollowUpDate.Value.Date < DateTime.Today;
    public bool HasTechnician => !string.IsNullOrWhiteSpace(Technician);
    public bool HasCustomerAddress => !string.IsNullOrWhiteSpace(CustomerAddress);
    public bool HasSentAt => SentAt.HasValue;
    public bool IsMobileInboxCard
    {
        get => _isMobileInboxCard;
        set
        {
            if (SetProperty(ref _isMobileInboxCard, value))
            {
                OnPropertyChanged(nameof(IsStandardTaskCard));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }
    }
    public bool IsStandardTaskCard => !IsMobileInboxCard;
    public string MobileInboxCreatedAtText { get => _mobileInboxCreatedAtText; set => SetProperty(ref _mobileInboxCreatedAtText, value); }
    public string MobileInboxAttachmentOverviewText
    {
        get => _mobileInboxAttachmentOverviewText;
        set
        {
            if (SetProperty(ref _mobileInboxAttachmentOverviewText, value))
            {
                OnPropertyChanged(nameof(HasMobileInboxAttachmentOverviewText));
            }
        }
    }
    public bool HasMobileInboxAttachmentOverviewText => !string.IsNullOrWhiteSpace(MobileInboxAttachmentOverviewText);
    public string MobileInboxStatusBadgeBackground { get => _mobileInboxStatusBadgeBackground; set => SetProperty(ref _mobileInboxStatusBadgeBackground, value); }
    public string MobileInboxStatusBadgeBorderBrush { get => _mobileInboxStatusBadgeBorderBrush; set => SetProperty(ref _mobileInboxStatusBadgeBorderBrush, value); }
    public string MobileInboxStatusBadgeForeground { get => _mobileInboxStatusBadgeForeground; set => SetProperty(ref _mobileInboxStatusBadgeForeground, value); }
    public string CardBackground => "#FFFFFF";
    public string CardBorderBrush => IsSelected ? "#000000" : IsMobileInboxCard && Status == "Fehler" ? "#E48A8A" : "#00000000";
    public string ReminderCardBackground => IsFollowUpOverdue ? "#FFF6EAEA" : "#FFFFFF";
    public string ReminderCardBorderBrush => IsFollowUpOverdue ? "#E48A8A" : "#00000000";

    public TaskItem Clone()
    {
        return new TaskItem
        {
            Id = Id,
            Title = Title,
            CustomerName = CustomerName,
            CustomerAddress = CustomerAddress,
            CustomerEmail = CustomerEmail,
            CustomerPhone = CustomerPhone,
            Description = Description,
            CategoryId = CategoryId,
            CategoryIds = new List<string>(CategoryIds),
            Status = Status,
            WorkflowType = WorkflowType,
            WorkflowStep = WorkflowStep,
            Priority = Priority,
            DueDate = DueDate,
            FollowUpDate = FollowUpDate,
            SentAt = SentAt,
            MaterialOrderedAt = MaterialOrderedAt,
            AssignedTo = AssignedTo,
            Technician = Technician,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            CompletedAt = CompletedAt,
            IsDeleted = IsDeleted,
            DeletedAt = DeletedAt,
            SortPosition = SortPosition,
            CategoryHint = CategoryHint,
            CategoryNameChips = new List<string>(CategoryNameChips),
            ShowCategoryHint = ShowCategoryHint,
            IsMobileInboxCard = IsMobileInboxCard,
            MobileInboxCreatedAtText = MobileInboxCreatedAtText,
            MobileInboxAttachmentOverviewText = MobileInboxAttachmentOverviewText,
            MobileInboxStatusBadgeBackground = MobileInboxStatusBadgeBackground,
            MobileInboxStatusBadgeBorderBrush = MobileInboxStatusBadgeBorderBrush,
            MobileInboxStatusBadgeForeground = MobileInboxStatusBadgeForeground
        };
    }
}
