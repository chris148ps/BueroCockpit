using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using BueroCockpit.Data;
using BueroCockpit.Models;
using BueroCockpit.Services;

namespace BueroCockpit;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string OverviewCategoryName = "Übersicht";
    private const string SettingsCategoryId = "__settings";
    private const string SettingsCategoryName = "Einstellungen";
    private readonly BueroRepository _repository = new();
    private readonly ThumbnailService _thumbnailService = new();
    private readonly BackupService _backupService = new();
    private readonly UpdateService _updateService = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly FileHashService _hashService = new();
    private AppSettings _appSettings = new();
    private CategoryItem? _selectedCategory;
    private TaskItem? _selectedTask;
    private CategoryItem? _selectedTaskCategory;
    private AttachmentItem? _selectedAttachment;
    private AttachmentEditSession? _selectedAttachmentEditSession;
    private string _taskListCaption = "0 Aufgaben";
    private string _globalSearchCaption = string.Empty;
    private string _searchText = string.Empty;
    private string _dueDateText = string.Empty;
    private string _followUpDateText = string.Empty;
    private string _sentAtText = string.Empty;
    private string _dateInputMessage = string.Empty;
    private bool _isGlobalSearchEnabled;
    private string _categoryEditorName = string.Empty;
    private string _categoryMessage = string.Empty;
    private string _backupStatus = "Noch kein Backup erstellt.";
    private string _lastBackupPath = string.Empty;
    private string _lastBackupTime = string.Empty;
    private string _updateStatus = "Noch kein Update-Kanal eingerichtet.";
    private string _updateFeedUrl = string.Empty;
    private bool _isUpdateAvailable;
    private string _attachmentEditStatus = string.Empty;
    private bool _isLoadingSelection;
    private bool _isUpdatingSelection;
    private bool _isRefreshingVisibleTasks;
    private bool _suppressTaskListSelectionChanged;
    private bool _suppressCategorySelectionChanged;
    private bool _suppressStatusSelectionChanged;
    private bool _suppressSavingDuringSelection;
    private bool _isUpdatingDateFields;
    private int _selectionNavigationDepth;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<CategoryItem> TaskCategories { get; } = new();
    public ObservableCollection<TaskItem> AllTasks { get; } = new();
    public ObservableCollection<TaskItem> VisibleTasks { get; } = new();
    public ObservableCollection<TaskSearchResult> GlobalSearchResults { get; } = new();
    public ObservableCollection<MaterialItem> Materials { get; } = new();
    public ObservableCollection<AttachmentItem> Attachments { get; } = new();
    public ObservableCollection<DashboardSection> DashboardSections { get; } = new();

    public string[] StatusOptions { get; } = ["Offen", "Wartet auf Kunde", "Material offen", "Terminiert", "Erledigt", "Archiv"];
    public string[] PriorityOptions { get; } = ["Niedrig", "Normal", "Hoch", "Dringend"];
    public string[] MaterialStatusOptions { get; } = ["benötigt", "bestellt", "vorhanden", "verbaut", "retour", "erledigt"];

    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value || (_selectedCategory is not null && value is not null && _selectedCategory.Id == value.Id))
            {
                return;
            }

            if (_selectedCategory is not null)
            {
                _selectedCategory.IsSelected = false;
            }

            _selectedCategory = value;
            if (_selectedCategory is not null)
            {
                _selectedCategory.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(IsOverviewSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
            OnPropertyChanged(nameof(IsTaskAreaVisible));
            CategoryEditorName = _selectedCategory?.Name ?? string.Empty;
            CategoryMessage = string.Empty;
        }
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value || (_selectedTask is not null && value is not null && _selectedTask.Id == value.Id))
            {
                return;
            }

            if (!_suppressSavingDuringSelection)
            {
                SaveCurrentMaterials();
            }
            if (_selectedTask is not null)
            {
                _selectedTask.IsSelected = false;
            }

            _selectedTask = value;
            if (_selectedTask is not null)
            {
                _selectedTask.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectedTask));
            OnPropertyChanged(nameof(HasSelectedTask));
            LoadTaskDetails();
        }
    }

    public CategoryItem? SelectedTaskCategory
    {
        get => _selectedTaskCategory;
        set
        {
            if (_selectedTaskCategory == value)
            {
                return;
            }

            _selectedTaskCategory = value;
            OnPropertyChanged(nameof(SelectedTaskCategory));
            if (!_isLoadingSelection &&
                !_isUpdatingSelection &&
                !_isRefreshingVisibleTasks &&
                _selectionNavigationDepth == 0 &&
                SelectedTask is not null &&
                value is not null)
            {
                SelectedTask.CategoryId = value.Id;
                _repository.SaveTask(SelectedTask);
                RefreshVisibleTasks();
                UpdateCategoryCounts();
            }
        }
    }

    public DateTimeOffset? SelectedDueDate
    {
        get => SelectedTask?.DueDate is null ? null : new DateTimeOffset(SelectedTask.DueDate.Value);
        set
        {
            if (SelectedTask is null)
            {
                return;
            }

            SelectedTask.DueDate = value?.DateTime;
            OnPropertyChanged(nameof(SelectedDueDate));
            UpdateDateTextFieldsFromSelectedTask();
        }
    }

    public DateTimeOffset? SelectedFollowUpDate
    {
        get => SelectedTask?.FollowUpDate is null ? null : new DateTimeOffset(SelectedTask.FollowUpDate.Value);
        set
        {
            if (SelectedTask is null)
            {
                return;
            }

            SelectedTask.FollowUpDate = value?.DateTime;
            OnPropertyChanged(nameof(SelectedFollowUpDate));
            UpdateDateTextFieldsFromSelectedTask();
        }
    }

    public DateTimeOffset? SelectedSentAt
    {
        get => SelectedTask?.SentAt is null ? null : new DateTimeOffset(SelectedTask.SentAt.Value);
        set
        {
            if (SelectedTask is null)
            {
                return;
            }

            SelectedTask.SentAt = value?.DateTime;
            OnPropertyChanged(nameof(SelectedSentAt));
            UpdateDateTextFieldsFromSelectedTask();
        }
    }

    public string DueDateInputText
    {
        get => _dueDateText;
        set
        {
            if (_dueDateText != value)
            {
                _dueDateText = value;
                OnPropertyChanged(nameof(DueDateInputText));
            }
        }
    }

    public string FollowUpDateInputText
    {
        get => _followUpDateText;
        set
        {
            if (_followUpDateText != value)
            {
                _followUpDateText = value;
                OnPropertyChanged(nameof(FollowUpDateInputText));
            }
        }
    }

    public string SentAtInputText
    {
        get => _sentAtText;
        set
        {
            if (_sentAtText != value)
            {
                _sentAtText = value;
                OnPropertyChanged(nameof(SentAtInputText));
            }
        }
    }

    public string DateInputMessage
    {
        get => _dateInputMessage;
        set
        {
            if (_dateInputMessage != value)
            {
                _dateInputMessage = value;
                OnPropertyChanged(nameof(DateInputMessage));
                OnPropertyChanged(nameof(HasDateInputMessage));
            }
        }
    }

    public bool HasDateInputMessage => !string.IsNullOrWhiteSpace(DateInputMessage);

    public bool HasSelectedTask => SelectedTask is not null;
    public bool IsOverviewSelected => SelectedCategory?.Name == OverviewCategoryName;
    public bool IsSettingsSelected => SelectedCategory?.Id == SettingsCategoryId;
    public bool IsTaskAreaVisible => !IsOverviewSelected && !IsSettingsSelected;
    public string DashboardDateText => DateTime.Today.ToString("dddd, dd. MMMM yyyy");
    public string AppDataDirectory => AppPaths.AppDataDirectory;
    public string DatabasePath => AppPaths.DatabasePath;
    public string TasksDirectory => AppPaths.TasksDirectory;
    public string BackupDirectory => AppPaths.BackupDirectory;
    public string CurrentAppVersion => _updateService.GetCurrentVersion();
    public string UpdateSource => _updateService.UpdateSource;
    public string UpdateFeedUrl
    {
        get => _updateFeedUrl;
        set
        {
            if (_updateFeedUrl != value)
            {
                _updateFeedUrl = value;
                OnPropertyChanged(nameof(UpdateFeedUrl));
            }
        }
    }

    public AttachmentItem? SelectedAttachment
    {
        get => _selectedAttachment;
        set
        {
            if (_selectedAttachment == value)
            {
                return;
            }

            if (_selectedAttachment is not null)
            {
                _selectedAttachment.IsSelected = false;
            }

            _selectedAttachment = value;
            if (_selectedAttachment is not null)
            {
                EnsureAttachmentThumbnail(_selectedAttachment);
                _selectedAttachment.IsSelected = true;
            }

            LoadSelectedAttachmentEditSession();
            OnPropertyChanged(nameof(SelectedAttachment));
            OnPropertyChanged(nameof(HasSelectedAttachment));
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(HasPreviewPlaceholder));
        }
    }

    public bool HasSelectedAttachment => SelectedAttachment is not null;
    public string PreviewImagePath => SelectedAttachment?.ThumbnailPath ?? string.Empty;
    public bool HasPreviewImage => !string.IsNullOrWhiteSpace(PreviewImagePath) && File.Exists(PreviewImagePath);
    public bool HasPreviewPlaceholder => HasSelectedAttachment && !HasPreviewImage;
    public bool HasNoMaterials => Materials.Count == 0;
    public string OneDriveEditDirectory => _appSettings.OneDriveEditDirectory;
    public bool HasOneDriveEditDirectory => !string.IsNullOrWhiteSpace(OneDriveEditDirectory);
    public bool HasNoOneDriveEditDirectory => !HasOneDriveEditDirectory;
    public string AttachmentEditStatus
    {
        get => _attachmentEditStatus;
        set
        {
            if (_attachmentEditStatus != value)
            {
                _attachmentEditStatus = value;
                OnPropertyChanged(nameof(AttachmentEditStatus));
                OnPropertyChanged(nameof(HasAttachmentEditStatus));
            }
        }
    }

    public bool HasAttachmentEditStatus => !string.IsNullOrWhiteSpace(AttachmentEditStatus);
    public bool CanImportAttachmentEdit =>
        _selectedAttachmentEditSession?.Status is "Changed" or "Conflict" &&
        SelectedAttachment is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            RefreshVisibleTasks();
            RefreshGlobalSearchResults();
        }
    }

    public bool IsGlobalSearchEnabled
    {
        get => _isGlobalSearchEnabled;
        set
        {
            if (_isGlobalSearchEnabled == value)
            {
                return;
            }

            _isGlobalSearchEnabled = value;
            OnPropertyChanged(nameof(IsGlobalSearchEnabled));
            OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
            RefreshVisibleTasks();
            RefreshGlobalSearchResults();
        }
    }

    public string GlobalSearchCaption
    {
        get => _globalSearchCaption;
        set
        {
            if (_globalSearchCaption != value)
            {
                _globalSearchCaption = value;
                OnPropertyChanged(nameof(GlobalSearchCaption));
            }
        }
    }

    public bool IsGlobalSearchPanelVisible =>
        IsGlobalSearchEnabled &&
        !string.IsNullOrWhiteSpace(SearchText) &&
        GlobalSearchResults.Count > 0;

    public string CategoryEditorName
    {
        get => _categoryEditorName;
        set
        {
            if (_categoryEditorName != value)
            {
                _categoryEditorName = value;
                OnPropertyChanged(nameof(CategoryEditorName));
            }
        }
    }

    public string CategoryMessage
    {
        get => _categoryMessage;
        set
        {
            if (_categoryMessage != value)
            {
                _categoryMessage = value;
                OnPropertyChanged(nameof(CategoryMessage));
                OnPropertyChanged(nameof(HasCategoryMessage));
            }
        }
    }

    public bool HasCategoryMessage => !string.IsNullOrWhiteSpace(CategoryMessage);
    public string BackupStatus
    {
        get => _backupStatus;
        set
        {
            if (_backupStatus != value)
            {
                _backupStatus = value;
                OnPropertyChanged(nameof(BackupStatus));
            }
        }
    }

    public string LastBackupPath
    {
        get => _lastBackupPath;
        set
        {
            if (_lastBackupPath != value)
            {
                _lastBackupPath = value;
                OnPropertyChanged(nameof(LastBackupPath));
                OnPropertyChanged(nameof(HasLastBackup));
            }
        }
    }

    public string LastBackupTime
    {
        get => _lastBackupTime;
        set
        {
            if (_lastBackupTime != value)
            {
                _lastBackupTime = value;
                OnPropertyChanged(nameof(LastBackupTime));
                OnPropertyChanged(nameof(HasLastBackup));
            }
        }
    }

    public bool HasLastBackup => !string.IsNullOrWhiteSpace(LastBackupPath);
    public string UpdateStatus
    {
        get => _updateStatus;
        set
        {
            if (_updateStatus != value)
            {
                _updateStatus = value;
                OnPropertyChanged(nameof(UpdateStatus));
            }
        }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set
        {
            if (_isUpdateAvailable != value)
            {
                _isUpdateAvailable = value;
                OnPropertyChanged(nameof(IsUpdateAvailable));
            }
        }
    }

    public string TaskListCaption
    {
        get => _taskListCaption;
        set
        {
            if (_taskListCaption != value)
            {
                _taskListCaption = value;
                OnPropertyChanged(nameof(TaskListCaption));
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        _appSettings = _settingsService.Load();
        UpdateFeedUrl = _appSettings.UpdateFeedUrl;
        _updateService.UpdateFeedUrl = UpdateFeedUrl;
        UpdateStatus = _updateService.GetUpdateStatusText();
        DataContext = this;

        _repository.Initialize();
        LoadData();
    }

    private void LoadData()
    {
        Categories.Clear();
        foreach (var category in _repository.GetCategories())
        {
            Categories.Add(category);
        }
        Categories.Add(CreateSettingsCategory());
        RefreshTaskCategories();

        AllTasks.Clear();
        foreach (var task in _repository.GetTasks())
        {
            AllTasks.Add(task);
        }

        UpdateCategoryCounts();
        SelectedCategory = Categories.FirstOrDefault(c => c.Name == OverviewCategoryName) ?? Categories.FirstOrDefault();
        ApplySelectedCategoryContent();
    }

    private void RefreshVisibleTasks()
    {
        if (_isRefreshingVisibleTasks)
        {
            return;
        }

        if (IsOverviewSelected)
        {
            ShowOverview();
            return;
        }

        _isRefreshingVisibleTasks = true;
        _suppressTaskListSelectionChanged = true;
        try
        {
            var selectedTaskId = SelectedTask?.Id;
            VisibleTasks.Clear();

            var selected = SelectedCategory;
            IEnumerable<TaskItem> tasks = selected is null
                ? AllTasks
                : AllTasks.Where(t => t.CategoryId == selected.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.Trim();
                tasks = tasks.Where(task => TaskMatchesSearch(task, query));
            }

            foreach (var task in tasks)
            {
                task.CategoryHint = GetCategoryName(task.CategoryId);
                task.ShowCategoryHint = false;
                VisibleTasks.Add(task);
            }

            TaskListCaption = VisibleTasks.Count == 1 ? "1 Aufgabe" : $"{VisibleTasks.Count} Aufgaben";

            var taskToSelect = selectedTaskId is null
                ? VisibleTasks.FirstOrDefault()
                : VisibleTasks.FirstOrDefault(task => task.Id == selectedTaskId) ?? VisibleTasks.FirstOrDefault();
            SetSelectedTaskDuringRefresh(taskToSelect);
        }
        finally
        {
            _suppressTaskListSelectionChanged = false;
            _isRefreshingVisibleTasks = false;
        }
    }

    private void RefreshGlobalSearchResults()
    {
        if (_selectionNavigationDepth > 0 || _isUpdatingSelection)
        {
            return;
        }

        GlobalSearchResults.Clear();
        if (!IsGlobalSearchEnabled || string.IsNullOrWhiteSpace(SearchText))
        {
            GlobalSearchCaption = string.Empty;
            OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
            return;
        }

        var query = SearchText.Trim();
        foreach (var task in AllTasks.Where(task => TaskMatchesSearch(task, query)).Take(50))
        {
            GlobalSearchResults.Add(CreateSearchResult(task, query));
        }

        GlobalSearchCaption = GlobalSearchResults.Count == 1
            ? "1 Treffer in allen Bereichen"
            : $"{GlobalSearchResults.Count} Treffer in allen Bereichen";
        OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
    }

    private TaskSearchResult CreateSearchResult(TaskItem task, string query)
    {
        var categoryName = GetCategoryName(task.CategoryId);
        var matchInfo = GetSearchMatchInfo(task, categoryName, query);
        return new TaskSearchResult(
            task,
            categoryName,
            task.CustomerName,
            task.Title,
            matchInfo,
            task.Technician,
            task.CustomerAddress,
            task.DueDate,
            task.SentAt);
    }

    private string GetSearchMatchInfo(TaskItem task, string categoryName, string query)
    {
        if (Contains(task.CustomerAddress, query))
        {
            return "Treffer in Adresse";
        }

        if (Contains(task.Technician, query))
        {
            return "Treffer in Monteur";
        }

        if (Contains(task.Status, query))
        {
            return "Treffer in Status";
        }

        if (Contains(categoryName, query))
        {
            return "Treffer in Bereich";
        }

        if (_repository.GetMaterials(task.Id).Any(material => Contains(material.Name, query)))
        {
            return "Treffer in Material";
        }

        if (_repository.GetAttachments(task.Id).Any(attachment => Contains(attachment.FileName, query)))
        {
            return "Treffer in Anhang";
        }

        return "Treffer in Aufgabe";
    }

    private void UpdateDateTextFieldsFromSelectedTask()
    {
        if (_isUpdatingDateFields)
        {
            return;
        }

        _isUpdatingDateFields = true;
        try
        {
            DueDateInputText = FormatDateShort(SelectedTask?.DueDate);
            FollowUpDateInputText = FormatDateShort(SelectedTask?.FollowUpDate);
            SentAtInputText = FormatDateShort(SelectedTask?.SentAt);
        }
        finally
        {
            _isUpdatingDateFields = false;
        }
    }

    private void ApplyDueDateText()
    {
        ApplyDateText(
            DueDateInputText,
            "Termin",
            () => SelectedTask?.DueDate,
            value =>
            {
                if (SelectedTask is not null)
                {
                    SelectedTask.DueDate = value;
                    OnPropertyChanged(nameof(SelectedDueDate));
                }
            });
    }

    private void ApplyFollowUpDateText()
    {
        ApplyDateText(
            FollowUpDateInputText,
            "Wiedervorlage",
            () => SelectedTask?.FollowUpDate,
            value =>
            {
                if (SelectedTask is not null)
                {
                    SelectedTask.FollowUpDate = value;
                    OnPropertyChanged(nameof(SelectedFollowUpDate));
                }
            });
    }

    private void ApplySentAtText()
    {
        ApplyDateText(
            SentAtInputText,
            "Gesendet am",
            () => SelectedTask?.SentAt,
            value =>
            {
                if (SelectedTask is not null)
                {
                    SelectedTask.SentAt = value;
                    OnPropertyChanged(nameof(SelectedSentAt));
                }
            });
    }

    private void ApplyDateText(string input, string fieldName, Func<DateTime?> getCurrentValue, Action<DateTime?> setValue)
    {
        if (_isUpdatingDateFields || SelectedTask is null)
        {
            return;
        }

        if (!TryParseGermanDate(input, out var parsedDate))
        {
            DateInputMessage = $"{fieldName}: Bitte Datum als TT.MM.JJJJ eingeben.";
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        var current = getCurrentValue();
        if (current?.Date == parsedDate?.Date)
        {
            DateInputMessage = string.Empty;
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        setValue(parsedDate);
        _repository.SaveTask(SelectedTask);
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private static string FormatDateShort(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? string.Empty;
    }

    private static string FormatDateLong(DateTime? value)
    {
        return value?.ToString("ddd., dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? string.Empty;
    }

    private static bool TryParseGermanDate(string input, out DateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" };
        if (!DateTime.TryParseExact(input.Trim(), formats, CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        value = parsed.Date;
        return true;
    }

    private void ApplySelectedCategoryContent()
    {
        if (IsOverviewSelected)
        {
            ShowOverview();
        }
        else if (IsSettingsSelected)
        {
            ShowSettings();
        }
        else
        {
            RefreshVisibleTasks();
        }
    }

    private void SetSelectedTaskDuringRefresh(TaskItem? task)
    {
        var wasUpdatingSelection = _isUpdatingSelection;
        var wasSuppressingStatusSelection = _suppressStatusSelectionChanged;
        var wasSuppressingSaving = _suppressSavingDuringSelection;

        _isUpdatingSelection = true;
        _suppressStatusSelectionChanged = true;
        _suppressSavingDuringSelection = true;
        try
        {
            SelectedTask = task;
            TaskList.SelectedItem = task;
        }
        finally
        {
            _suppressSavingDuringSelection = wasSuppressingSaving;
            _suppressStatusSelectionChanged = wasSuppressingStatusSelection;
            _isUpdatingSelection = wasUpdatingSelection;
        }
    }

    private void ShowOverview()
    {
        ClearSelectedTask();
        VisibleTasks.Clear();
        TaskListCaption = "Tagesübersicht";
        ClearSearchTextWithoutRefresh();
        RefreshDashboard();
    }

    private void ShowSettings()
    {
        ClearSelectedTask();
        VisibleTasks.Clear();
        TaskListCaption = "Einstellungen";
        ClearSearchTextWithoutRefresh();
    }

    private void ClearSearchTextWithoutRefresh()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            GlobalSearchResults.Clear();
            GlobalSearchCaption = string.Empty;
            OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
            return;
        }

        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        GlobalSearchResults.Clear();
        GlobalSearchCaption = string.Empty;
        OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
    }

    private void ClearSelectedTask()
    {
        if (!_suppressSavingDuringSelection)
        {
            SaveCurrentMaterials();
        }
        if (_selectedTask is not null)
        {
            _selectedTask.IsSelected = false;
        }

        _selectedTask = null;
        SelectedAttachment = null;
        SelectedTaskCategory = null;
        Materials.Clear();
        Attachments.Clear();
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(HasSelectedTask));
        OnPropertyChanged(nameof(HasNoMaterials));
        OnPropertyChanged(nameof(SelectedDueDate));
        OnPropertyChanged(nameof(SelectedFollowUpDate));
        OnPropertyChanged(nameof(SelectedSentAt));
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private void RefreshDashboard()
    {
        DashboardSections.Clear();
        var today = DateTime.Today;
        var activeTasks = AllTasks.Where(task => !IsDoneOrArchived(task)).ToList();

        DashboardSections.Add(CreateDashboardSection(
            "Heute fällig",
            AllTasks.Where(task => task.DueDate?.Date == today)));

        DashboardSections.Add(CreateDashboardSection(
            "Überfällig",
            activeTasks.Where(task => task.DueDate?.Date < today)));

        DashboardSections.Add(CreateDashboardSection(
            "Wiedervorlage heute",
            AllTasks.Where(task => task.FollowUpDate?.Date == today)));

        DashboardSections.Add(CreateDashboardSection(
            "Material offen",
            activeTasks.Where(HasOpenMaterial)));

        DashboardSections.Add(CreateDashboardSection(
            "Angebote / offene Büroaufgaben",
            AllTasks.Where(IsOfficeTask)));
    }

    private static DashboardSection CreateDashboardSection(string title, IEnumerable<TaskItem> tasks)
    {
        var ordered = tasks
            .OrderBy(task => task.DueDate ?? task.FollowUpDate ?? DateTime.MaxValue)
            .ThenBy(task => task.CustomerName)
            .ThenBy(task => task.Title)
            .ToList();

        return new DashboardSection(title, ordered.Count, ordered.Take(5));
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in Categories)
        {
            category.TaskCount = category.Name == "Übersicht"
                ? AllTasks.Count
                : category.Id == SettingsCategoryId
                    ? 0
                : AllTasks.Count(t => t.CategoryId == category.Id);
        }
    }

    private void LoadTaskDetails()
    {
        _isLoadingSelection = true;
        Materials.Clear();
        Attachments.Clear();
        SelectedAttachment = null;
        OnPropertyChanged(nameof(HasNoMaterials));

        if (SelectedTask is not null)
        {
            SelectedTaskCategory = Categories.FirstOrDefault(c => c.Id == SelectedTask.CategoryId);

            foreach (var item in _repository.GetMaterials(SelectedTask.Id))
            {
                Materials.Add(item);
            }
            OnPropertyChanged(nameof(HasNoMaterials));

            foreach (var item in _repository.GetAttachments(SelectedTask.Id))
            {
                EnsureAttachmentThumbnail(item);
                Attachments.Add(item);
            }
        }
        else
        {
            SelectedTaskCategory = null;
        }

        _isLoadingSelection = false;
        OnPropertyChanged(nameof(SelectedDueDate));
        OnPropertyChanged(nameof(SelectedFollowUpDate));
        OnPropertyChanged(nameof(SelectedSentAt));
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private void NewTask_OnClick(object? sender, RoutedEventArgs e)
    {
        var category = IsOverviewSelected || IsSettingsSelected ? Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben") : SelectedCategory;
        category ??= Categories.FirstOrDefault();
        if (category is null)
        {
            return;
        }

        var now = DateTime.Now;
        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CategoryId = category.Id,
            CustomerName = "Neuer Kunde",
            Title = "Neue Aufgabe",
            Status = "Offen",
            Priority = "Normal",
            SortPosition = _repository.GetNextTaskSortPosition(category.Id),
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.SaveTask(task);
        AllTasks.Insert(0, task);
        SelectedCategory = category;
        RefreshVisibleTasks();
        SelectedTask = task;
        UpdateCategoryCounts();
    }

    private void SaveTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        ApplySelectedTaskStatusRules();

        _repository.SaveTask(SelectedTask);
        SaveCurrentMaterials();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private void DeleteTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        _repository.DeleteTask(task.Id);
        AllTasks.Remove(task);
        Materials.Clear();
        Attachments.Clear();
        SelectedAttachment = null;
        SelectedTask = null;
        OnPropertyChanged(nameof(HasNoMaterials));
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private void AddMaterial_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var item = new MaterialItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TaskId = SelectedTask.Id,
            Name = "Neues Material",
            Quantity = 1,
            Unit = "Stk.",
            Status = "benötigt"
        };

        Materials.Add(item);
        OnPropertyChanged(nameof(HasNoMaterials));
        _repository.SaveMaterial(item);
    }

    private void DeleteMaterial_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialItem item })
        {
            return;
        }

        _repository.DeleteMaterial(item.Id);
        Materials.Remove(item);
        OnPropertyChanged(nameof(HasNoMaterials));
    }

    private async void AddAttachment_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        _repository.SaveTask(SelectedTask);
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Anhang auswählen",
            AllowMultiple = false
        });

        var pickedFile = files.FirstOrDefault();
        var sourcePath = pickedFile?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        var attachmentDirectory = AppPaths.GetAttachmentDirectory(SelectedTask.Id);
        Directory.CreateDirectory(attachmentDirectory);

        var originalName = Path.GetFileName(sourcePath);
        var storedName = CreateStoredFileName(originalName);
        var destinationPath = Path.Combine(attachmentDirectory, storedName);
        File.Copy(sourcePath, destinationPath, overwrite: false);

        var attachment = new AttachmentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TaskId = SelectedTask.Id,
            FileName = originalName,
            StoredPath = destinationPath,
            ThumbnailPath = string.Empty,
            FileType = Path.GetExtension(originalName),
            AddedAt = DateTime.Now
        };

        EnsureAttachmentThumbnail(attachment);
        _repository.SaveAttachment(attachment);
        Attachments.Insert(0, attachment);
        SelectAttachment(attachment);
    }

    private void OpenAttachment_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AttachmentItem item })
        {
            return;
        }

        SelectAttachment(item);
        OpenAttachmentExternal(item);
    }

    private void DeleteAttachment_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AttachmentItem item })
        {
            return;
        }

        if (SelectedAttachment == item)
        {
            SelectedAttachment = null;
        }

        _repository.DeleteAttachment(item.Id);
        TryDeleteFile(item.ThumbnailPath);
        TryDeleteFile(item.StoredPath);
        Attachments.Remove(item);
    }

    private void OpenSelectedAttachmentExternal_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedAttachment is not null)
        {
            OpenAttachmentExternal(SelectedAttachment);
        }
    }

    private void ClosePreview_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectedAttachment = null;
    }

    private void AttachmentCard_OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Avalonia.StyledElement { DataContext: AttachmentItem item })
        {
            SelectAttachment(item);
        }
    }

    private void SelectAttachment(AttachmentItem item)
    {
        SelectedAttachment = item;
    }

    private void AddCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = CategoryEditorName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryMessage = "Bitte einen Kategorienamen eingeben.";
            return;
        }

        var category = new CategoryItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            SortOrder = _repository.GetNextCategorySortOrder(),
            SortMode = "Geändert am",
            Color = "#F2F3F5",
            IsVisible = true
        };

        _repository.SaveCategory(category);
        InsertBeforeSettings(category);
        RefreshTaskCategories();
        SelectedCategory = category;
        ApplySelectedCategoryContent();
        UpdateCategoryCounts();
        CategoryMessage = "Kategorie angelegt.";
    }

    private void RenameCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            CategoryMessage = "Diese Kategorie kann nicht umbenannt werden.";
            return;
        }

        var name = CategoryEditorName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryMessage = "Bitte einen Kategorienamen eingeben.";
            return;
        }

        SelectedCategory.Name = name;
        _repository.SaveCategory(SelectedCategory);
        RefreshTaskCategories();
        OnPropertyChanged(nameof(SelectedCategory));
        CategoryMessage = "Kategorie umbenannt.";
    }

    private void HideCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            CategoryMessage = "Diese Kategorie kann nicht ausgeblendet werden.";
            return;
        }

        if (_repository.GetTaskCountForCategory(SelectedCategory.Id) > 0)
        {
            CategoryMessage = "Kategorie enthält Aufgaben und wurde nicht ausgeblendet.";
            return;
        }

        var category = SelectedCategory;
        _repository.HideCategory(category.Id);
        Categories.Remove(category);
        RefreshTaskCategories();
        SelectedCategory = Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben") ?? Categories.FirstOrDefault();
        ApplySelectedCategoryContent();
        UpdateCategoryCounts();
        CategoryMessage = "Kategorie ausgeblendet.";
    }

    private void DuplicateTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        SaveCurrentMaterials();
        var source = SelectedTask;
        var now = DateTime.Now;
        var copy = new TaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CustomerName = source.CustomerName,
            CustomerAddress = source.CustomerAddress,
            Title = source.Title.StartsWith("Kopie - ", StringComparison.OrdinalIgnoreCase) ? source.Title : $"Kopie - {source.Title}",
            Description = source.Description,
            CategoryId = source.CategoryId,
            Status = source.Status,
            Priority = source.Priority,
            DueDate = source.DueDate,
            FollowUpDate = source.FollowUpDate,
            SentAt = source.SentAt,
            AssignedTo = source.AssignedTo,
            Technician = source.Technician,
            SortPosition = _repository.GetNextTaskSortPosition(source.CategoryId),
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = source.Status == "Erledigt" ? now : null
        };

        _repository.SaveTask(copy);
        foreach (var material in _repository.GetMaterials(source.Id))
        {
            _repository.SaveMaterial(new MaterialItem
            {
                Id = Guid.NewGuid().ToString("N"),
                TaskId = copy.Id,
                Quantity = material.Quantity,
                Unit = material.Unit,
                Name = material.Name,
                Status = material.Status,
                Supplier = material.Supplier,
                OrderedAt = material.OrderedAt,
                Note = material.Note
            });
        }

        AllTasks.Insert(0, copy);
        RefreshVisibleTasks();
        SelectedTask = copy;
        UpdateCategoryCounts();
    }

    private void StatusCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSelection ||
            _isUpdatingSelection ||
            _isRefreshingVisibleTasks ||
            _suppressStatusSelectionChanged ||
            _selectionNavigationDepth > 0 ||
            SelectedTask is null)
        {
            return;
        }

        if (sender is not ComboBox { SelectedItem: string status } ||
            SelectedTask.Status.Equals(status, StringComparison.Ordinal))
        {
            return;
        }

        SelectedTask.Status = status;
        ApplySelectedTaskStatusRules();
        _repository.SaveTask(SelectedTask);
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private void DashboardTask_OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Avalonia.StyledElement { DataContext: TaskItem task })
        {
            return;
        }

        NavigateToTask(task, fromGlobalSearch: false);
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SearchText = textBox.Text ?? string.Empty;
        }
    }

    private void DueDateTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyDueDateText();
    }

    private void FollowUpDateTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyFollowUpDateText();
    }

    private void SentAtTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplySentAtText();
    }

    private void DateTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        switch ((sender as Control)?.Tag as string)
        {
            case "DueDate":
                ApplyDueDateText();
                break;
            case "FollowUpDate":
                ApplyFollowUpDateText();
                break;
            case "SentAt":
                ApplySentAtText();
                break;
        }

        e.Handled = true;
    }

    private void CategoryEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CategoryEditorName = textBox.Text ?? string.Empty;
        }
    }

    private void OpenDataFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(AppPaths.AppDataDirectory);
    }

    private void OpenBackupFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(AppPaths.BackupDirectory);
    }

    private void UpdateFeed_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateFeedUrl = textBox.Text ?? string.Empty;
        }
    }

    private void SaveUpdateFeed_OnClick(object? sender, RoutedEventArgs e)
    {
        _appSettings.UpdateFeedUrl = UpdateFeedUrl.Trim();
        _settingsService.Save(_appSettings);
        _updateService.UpdateFeedUrl = _appSettings.UpdateFeedUrl;
        OnPropertyChanged(nameof(UpdateSource));
        IsUpdateAvailable = false;
        UpdateStatus = string.IsNullOrWhiteSpace(_appSettings.UpdateFeedUrl)
            ? "Noch kein Update-Kanal eingerichtet."
            : "Update-Kanal gespeichert.";
    }

    private async void SelectOneDriveFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            BackupStatus = "Ordnerauswahl ist nicht verfügbar.";
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "OneDrive-Bearbeitungsordner auswählen",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        _appSettings.OneDriveEditDirectory = folderPath;
        _settingsService.Save(_appSettings);
        OnPropertyChanged(nameof(OneDriveEditDirectory));
        OnPropertyChanged(nameof(HasOneDriveEditDirectory));
        OnPropertyChanged(nameof(HasNoOneDriveEditDirectory));
        BackupStatus = $"OneDrive-Bearbeitungsordner gesetzt: {folderPath}";
    }

    private void OpenOneDriveFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!HasOneDriveEditDirectory)
        {
            BackupStatus = "Noch kein OneDrive-Bearbeitungsordner gewählt.";
            return;
        }

        OpenFolder(OneDriveEditDirectory);
    }

    private void CreateBackup_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = _backupService.CreateBackup();
            LastBackupPath = result.BackupPath;
            LastBackupTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            BackupStatus = result.SkippedFiles == 0
                ? "Backup wurde erstellt."
                : $"Backup wurde erstellt. {result.SkippedFiles} Datei(en) konnten nicht gelesen werden.";
        }
        catch (Exception ex)
        {
            BackupStatus = "Backup konnte nicht erstellt werden.";
            Debug.WriteLine($"Backup failed: {ex}");
        }
    }

    private async void CheckUpdates_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateStatus = "Updateprüfung läuft...";
        IsUpdateAvailable = await _updateService.CheckForUpdatesAsync();
        UpdateStatus = _updateService.GetUpdateStatusText();
    }

    private async void InstallUpdate_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateStatus = "Update wird vorbereitet...";
        var started = await _updateService.DownloadAndApplyUpdateAsync();
        UpdateStatus = _updateService.GetUpdateStatusText();
        IsUpdateAvailable = started && _updateService.HasPendingUpdate;
    }

    private void ExportAttachmentForIpad_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedAttachment is null)
        {
            return;
        }

        if (!HasOneDriveEditDirectory)
        {
            AttachmentEditStatus = "Noch kein OneDrive-Bearbeitungsordner gewählt.";
            return;
        }

        if (!File.Exists(SelectedAttachment.StoredPath))
        {
            AttachmentEditStatus = "Die gespeicherte Anhangdatei wurde nicht gefunden.";
            return;
        }

        var originalHash = _hashService.ComputeSha256(SelectedAttachment.StoredPath);
        if (string.IsNullOrWhiteSpace(originalHash))
        {
            AttachmentEditStatus = "Die Anhangdatei konnte nicht gelesen werden.";
            return;
        }

        try
        {
            var task = SelectedTask ?? AllTasks.FirstOrDefault(item => item.Id == SelectedAttachment.TaskId);
            var folderName = SanitizeFileName($"{task?.CustomerName}_{task?.Title}_{SelectedAttachment.TaskId}");
            if (string.IsNullOrWhiteSpace(folderName))
            {
                folderName = SelectedAttachment.TaskId;
            }

            var exportDirectory = Path.Combine(OneDriveEditDirectory, folderName);
            Directory.CreateDirectory(exportDirectory);
            var exportPath = CreateUniquePath(exportDirectory, SelectedAttachment.FileName);
            File.Copy(SelectedAttachment.StoredPath, exportPath, overwrite: false);

            var exportedHash = _hashService.ComputeSha256(exportPath);
            if (string.IsNullOrWhiteSpace(exportedHash))
            {
                AttachmentEditStatus = "Die bereitgestellte Datei konnte nicht geprüft werden.";
                TryDeleteFile(exportPath);
                return;
            }

            var session = new AttachmentEditSession
            {
                Id = Guid.NewGuid().ToString("N"),
                AttachmentId = SelectedAttachment.Id,
                TaskId = SelectedAttachment.TaskId,
                ExportPath = exportPath,
                ExportedAt = DateTime.Now,
                OriginalHashAtExport = originalHash,
                ExportedFileHashAtExport = exportedHash,
                Status = "Exported"
            };
            _repository.SaveAttachmentEditSession(session);
            SetSelectedAttachmentEditSession(session, $"Für iPad bereitgestellt: {exportPath}");
        }
        catch (Exception ex)
        {
            AttachmentEditStatus = "Anhang konnte nicht für iPad bereitgestellt werden.";
            Debug.WriteLine($"Attachment export failed: {ex}");
        }
    }

    private void CheckIpadChanges_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedAttachment is null)
        {
            return;
        }

        var session = _selectedAttachmentEditSession ?? _repository.GetLatestEditSessionForAttachment(SelectedAttachment.Id);
        if (session is null)
        {
            AttachmentEditStatus = "Noch nicht für iPad bereitgestellt.";
            UpdateImportAvailability();
            return;
        }

        if (!File.Exists(session.ExportPath))
        {
            session.Status = "Missing";
            _repository.SaveAttachmentEditSession(session);
            SetSelectedAttachmentEditSession(session, "Die bereitgestellte iPad-Datei wurde nicht gefunden.");
            return;
        }

        var exportedHash = _hashService.ComputeSha256(session.ExportPath);
        if (string.IsNullOrWhiteSpace(exportedHash))
        {
            AttachmentEditStatus = "Die iPad-Datei konnte nicht gelesen werden.";
            UpdateImportAvailability();
            return;
        }

        if (exportedHash == session.ExportedFileHashAtExport)
        {
            session.Status = "Exported";
            _repository.SaveAttachmentEditSession(session);
            SetSelectedAttachmentEditSession(session, "Keine Änderung gefunden.");
            return;
        }

        var currentOriginalHash = _hashService.ComputeSha256(SelectedAttachment.StoredPath);
        var hasConflict = !string.IsNullOrWhiteSpace(currentOriginalHash) &&
                          currentOriginalHash != session.OriginalHashAtExport;
        session.Status = hasConflict ? "Conflict" : "Changed";
        _repository.SaveAttachmentEditSession(session);

        var message = hasConflict
            ? "Original und iPad-Datei wurden beide geändert. Beim Übernehmen wird die aktuelle Originaldatei vorher gesichert."
            : "Änderung gefunden. Die Bearbeitung kann übernommen werden.";
        SetSelectedAttachmentEditSession(session, message);
    }

    private void ImportIpadChanges_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedAttachment is null || _selectedAttachmentEditSession is null)
        {
            return;
        }

        var session = _selectedAttachmentEditSession;
        if (session.Status is not ("Changed" or "Conflict"))
        {
            AttachmentEditStatus = "Keine geänderte iPad-Datei zum Übernehmen vorhanden.";
            UpdateImportAvailability();
            return;
        }

        if (!File.Exists(session.ExportPath))
        {
            session.Status = "Missing";
            _repository.SaveAttachmentEditSession(session);
            SetSelectedAttachmentEditSession(session, "Die bereitgestellte iPad-Datei wurde nicht gefunden.");
            return;
        }

        try
        {
            var backupDirectory = AppPaths.GetAttachmentBackupDirectory(SelectedAttachment.TaskId);
            Directory.CreateDirectory(backupDirectory);
            var backupPath = CreateBackupPath(backupDirectory, SelectedAttachment.FileName);

            if (File.Exists(SelectedAttachment.StoredPath))
            {
                File.Copy(SelectedAttachment.StoredPath, backupPath, overwrite: false);
                session.BackupPath = backupPath;
            }

            File.Copy(session.ExportPath, SelectedAttachment.StoredPath, overwrite: true);
            TryDeleteFile(SelectedAttachment.ThumbnailPath);
            SelectedAttachment.ThumbnailPath = string.Empty;
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(HasPreviewPlaceholder));

            EnsureAttachmentThumbnail(SelectedAttachment);
            session.Status = "Imported";
            session.ImportedAt = DateTime.Now;
            _repository.SaveAttachmentEditSession(session);
            SetSelectedAttachmentEditSession(session, "iPad-Bearbeitung übernommen. Alte Version wurde gesichert.");
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(HasPreviewPlaceholder));
        }
        catch (Exception ex)
        {
            AttachmentEditStatus = "iPad-Bearbeitung konnte nicht übernommen werden.";
            Debug.WriteLine($"Attachment import failed: {ex}");
            UpdateImportAvailability();
        }
    }

    private static void OpenAttachmentExternal(AttachmentItem item)
    {
        if (!File.Exists(item.StoredPath))
        {
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = item.StoredPath,
            UseShellExecute = true
        };
        process.Start();
    }

    private void CategoryList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressCategorySelectionChanged || _isUpdatingSelection || _selectionNavigationDepth > 0)
        {
            return;
        }

        ApplySelectedCategoryContent();
    }

    private void TaskList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTaskListSelectionChanged || _isUpdatingSelection || _isRefreshingVisibleTasks || _selectionNavigationDepth > 0)
        {
            return;
        }

        if (sender is not ListBox { SelectedItem: TaskItem selectedTask })
        {
            return;
        }

        SelectedTask = selectedTask;
    }

    private void GlobalSearchResult_OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Avalonia.StyledElement { DataContext: TaskSearchResult result })
        {
            e.Handled = true;
            NavigateToTask(result.Task, fromGlobalSearch: true);
        }
    }

    private void NavigateToTask(TaskItem? task, bool fromGlobalSearch)
    {
        if (task is null || _selectionNavigationDepth > 0)
        {
            return;
        }

        var category = Categories.FirstOrDefault(c => c.Id == task.CategoryId);
        if (category is null)
        {
            ClearSelectedTask();
            return;
        }

        _selectionNavigationDepth++;
        _isUpdatingSelection = true;
        _suppressTaskListSelectionChanged = true;
        _suppressCategorySelectionChanged = true;
        _suppressStatusSelectionChanged = true;
        _suppressSavingDuringSelection = true;
        try
        {
            if (fromGlobalSearch && _isGlobalSearchEnabled)
            {
                _isGlobalSearchEnabled = false;
                OnPropertyChanged(nameof(IsGlobalSearchEnabled));
                GlobalSearchResults.Clear();
                GlobalSearchCaption = string.Empty;
                OnPropertyChanged(nameof(IsGlobalSearchPanelVisible));
            }

            SelectedCategory = category;
            CategoryList.SelectedItem = category;
            RefreshVisibleTasks();
            SelectedTask = VisibleTasks.FirstOrDefault(item => item.Id == task.Id);
            TaskList.SelectedItem = SelectedTask;
        }
        finally
        {
            _suppressSavingDuringSelection = false;
            _suppressStatusSelectionChanged = false;
            _suppressCategorySelectionChanged = false;
            _suppressTaskListSelectionChanged = false;
            _isUpdatingSelection = false;
            _selectionNavigationDepth--;
        }
    }

    private void SaveCurrentMaterials()
    {
        foreach (var item in Materials.Where(m => !string.IsNullOrWhiteSpace(m.TaskId)))
        {
            _repository.SaveMaterial(item);
        }
    }

    private bool TaskMatchesSearch(TaskItem task, string query)
    {
        var categoryName = GetCategoryName(task.CategoryId);
        if (Contains(task.CustomerName, query) ||
            Contains(task.CustomerAddress, query) ||
            Contains(task.Title, query) ||
            Contains(task.Description, query) ||
            Contains(task.AssignedTo, query) ||
            Contains(task.Technician, query) ||
            Contains(task.Status, query) ||
            Contains(categoryName, query))
        {
            return true;
        }

        return _repository.GetMaterials(task.Id).Any(material => Contains(material.Name, query)) ||
               _repository.GetAttachments(task.Id).Any(attachment => Contains(attachment.FileName, query));
    }

    private string GetCategoryName(string categoryId)
    {
        return Categories.FirstOrDefault(category => category.Id == categoryId)?.Name ?? string.Empty;
    }

    private bool HasOpenMaterial(TaskItem task)
    {
        return _repository.GetMaterials(task.Id).Any(material =>
            material.Status.Equals("benötigt", StringComparison.OrdinalIgnoreCase) ||
            material.Status.Equals("bestellt", StringComparison.OrdinalIgnoreCase) ||
            material.Status.Equals("retour", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsOfficeTask(TaskItem task)
    {
        if (IsDoneOrArchived(task))
        {
            return false;
        }

        var categoryName = Categories.FirstOrDefault(c => c.Id == task.CategoryId)?.Name ?? string.Empty;
        return Contains(categoryName, "Angebote erstellen") ||
               Contains(categoryName, "Wartet auf Kunde") ||
               Contains(categoryName, "Material bestellen") ||
               task.Status.Equals("Wartet auf Kunde", StringComparison.OrdinalIgnoreCase) ||
               task.Status.Equals("Material offen", StringComparison.OrdinalIgnoreCase) ||
               !IsDoneOrArchived(task);
    }

    private static bool IsDoneOrArchived(TaskItem task)
    {
        return task.Status.Equals("Erledigt", StringComparison.OrdinalIgnoreCase) ||
               task.Status.Equals("Archiv", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string query)
    {
        return value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void RefreshTaskCategories()
    {
        TaskCategories.Clear();
        foreach (var category in Categories.Where(category => !IsSpecialCategory(category)))
        {
            TaskCategories.Add(category);
        }
    }

    private static CategoryItem CreateSettingsCategory()
    {
        return new CategoryItem
        {
            Id = SettingsCategoryId,
            Name = SettingsCategoryName,
            SortOrder = int.MaxValue,
            Color = "#F2F3F5",
            IsVisible = true
        };
    }

    private void InsertBeforeSettings(CategoryItem category)
    {
        var settingsIndex = Categories.ToList().FindIndex(item => item.Id == SettingsCategoryId);
        if (settingsIndex >= 0)
        {
            Categories.Insert(settingsIndex, category);
        }
        else
        {
            Categories.Add(category);
        }
    }

    private static bool IsSpecialCategory(CategoryItem category)
    {
        return category.Id == SettingsCategoryId || category.Name == OverviewCategoryName;
    }

    private void LoadSelectedAttachmentEditSession()
    {
        _selectedAttachmentEditSession = SelectedAttachment is null
            ? null
            : _repository.GetLatestEditSessionForAttachment(SelectedAttachment.Id);

        AttachmentEditStatus = _selectedAttachmentEditSession is null
            ? string.Empty
            : GetAttachmentEditSessionStatusText(_selectedAttachmentEditSession);
        UpdateImportAvailability();
    }

    private void SetSelectedAttachmentEditSession(AttachmentEditSession session, string status)
    {
        _selectedAttachmentEditSession = session;
        AttachmentEditStatus = status;
        UpdateImportAvailability();
    }

    private void UpdateImportAvailability()
    {
        OnPropertyChanged(nameof(CanImportAttachmentEdit));
    }

    private static string GetAttachmentEditSessionStatusText(AttachmentEditSession session)
    {
        return session.Status switch
        {
            "Exported" => $"Für iPad bereitgestellt: {session.ExportPath}",
            "Changed" => "Änderung gefunden. Die Bearbeitung kann übernommen werden.",
            "Imported" => session.ImportedAt is null
                ? "iPad-Bearbeitung wurde übernommen."
                : $"iPad-Bearbeitung übernommen am {session.ImportedAt:dd.MM.yyyy HH:mm}.",
            "Missing" => "Die bereitgestellte iPad-Datei wurde nicht gefunden.",
            "Conflict" => "Original und iPad-Datei wurden beide geändert. Beim Übernehmen wird die aktuelle Originaldatei vorher gesichert.",
            _ => string.Empty
        };
    }

    private static string SanitizeFileName(string value)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalidChar, '_');
        }

        cleaned = cleaned.Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');

        return cleaned.Length <= 90 ? cleaned : cleaned[..90];
    }

    private static string CreateUniquePath(string directory, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = $"Anhang_{Guid.NewGuid():N}";
        }

        var candidate = Path.Combine(directory, safeFileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var extension = Path.GetExtension(safeFileName);
        var stem = Path.GetFileNameWithoutExtension(safeFileName);
        for (var i = 1; i < 1000; i++)
        {
            candidate = Path.Combine(directory, $"{stem}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}_{Guid.NewGuid():N}{extension}");
    }

    private static string CreateBackupPath(string directory, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var stem = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "Anhang";
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return CreateUniquePath(directory, $"{stem}_vor_iPad_{timestamp}{extension}");
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            process.Start();
            BackupStatus = $"Ordner geöffnet: {path}";
        }
        catch (Exception ex)
        {
            BackupStatus = $"Ordner konnte nicht geöffnet werden: {path}";
            Debug.WriteLine($"Could not open folder '{path}': {ex}");
        }
    }

    private void ApplySelectedTaskStatusRules()
    {
        if (SelectedTask is null)
        {
            return;
        }

        SelectedTask.CategoryId = SelectedTaskCategory?.Id ?? SelectedTask.CategoryId;
        if (SelectedTask.Status == "Erledigt" && SelectedTask.CompletedAt is null)
        {
            SelectedTask.CompletedAt = DateTime.Now;
        }
        else if (SelectedTask.Status != "Erledigt")
        {
            SelectedTask.CompletedAt = null;
        }

        if (SelectedTask.Status == "Archiv")
        {
            var archive = Categories.FirstOrDefault(c => c.Name == "Archiv");
            if (archive is not null)
            {
                SelectedTask.CategoryId = archive.Id;
                SelectedTaskCategory = archive;
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not delete file '{path}': {ex}");
        }
    }

    private void EnsureAttachmentThumbnail(AttachmentItem attachment)
    {
        var thumbnailPath = _thumbnailService.EnsureThumbnail(attachment);
        if (string.IsNullOrWhiteSpace(thumbnailPath) || thumbnailPath == attachment.ThumbnailPath)
        {
            return;
        }

        attachment.ThumbnailPath = thumbnailPath;
        _repository.UpdateAttachmentThumbnail(attachment.Id, thumbnailPath);
    }

    private static string CreateStoredFileName(string originalName)
    {
        var extension = Path.GetExtension(originalName);
        var safeStem = Path.GetFileNameWithoutExtension(originalName);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            safeStem = safeStem.Replace(invalidChar, '_');
        }

        return $"{safeStem}_{Guid.NewGuid():N}{extension}";
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class DashboardSection
{
    public DashboardSection(string title, int count, IEnumerable<TaskItem> tasks)
    {
        Title = title;
        Count = count;
        Tasks = new ObservableCollection<TaskItem>(tasks);
    }

    public string Title { get; }
    public int Count { get; }
    public ObservableCollection<TaskItem> Tasks { get; }
    public bool HasTasks => Tasks.Count > 0;
    public bool HasNoTasks => !HasTasks;
}

public sealed class TaskSearchResult
{
    public TaskSearchResult(
        TaskItem task,
        string categoryName,
        string customerName,
        string title,
        string matchInfo,
        string technician,
        string customerAddress,
        DateTime? dueDate,
        DateTime? sentAt)
    {
        Task = task;
        CategoryName = categoryName;
        CustomerName = customerName;
        Title = title;
        MatchInfo = matchInfo;
        Technician = technician;
        CustomerAddress = customerAddress;
        DueDate = dueDate;
        SentAt = sentAt;
    }

    public TaskItem Task { get; }
    public string CategoryName { get; }
    public string CustomerName { get; }
    public string Title { get; }
    public string MatchInfo { get; }
    public string Technician { get; }
    public string CustomerAddress { get; }
    public DateTime? DueDate { get; }
    public DateTime? SentAt { get; }
}
