using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
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
    private CategoryItem? _selectedCategory;
    private TaskItem? _selectedTask;
    private CategoryItem? _selectedTaskCategory;
    private AttachmentItem? _selectedAttachment;
    private string _taskListCaption = "0 Aufgaben";
    private string _searchText = string.Empty;
    private string _categoryEditorName = string.Empty;
    private string _categoryMessage = string.Empty;
    private string _backupStatus = "Noch kein Backup erstellt.";
    private string _lastBackupPath = string.Empty;
    private string _lastBackupTime = string.Empty;
    private bool _isLoadingSelection;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<CategoryItem> TaskCategories { get; } = new();
    public ObservableCollection<TaskItem> AllTasks { get; } = new();
    public ObservableCollection<TaskItem> VisibleTasks { get; } = new();
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
            if (_selectedCategory == value)
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
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value)
            {
                return;
            }

            SaveCurrentMaterials();
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
            if (!_isLoadingSelection && SelectedTask is not null && value is not null)
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
        }
    }

    public bool HasSelectedTask => SelectedTask is not null;
    public bool IsOverviewSelected => SelectedCategory?.Name == OverviewCategoryName;
    public bool IsSettingsSelected => SelectedCategory?.Id == SettingsCategoryId;
    public bool IsTaskAreaVisible => !IsOverviewSelected && !IsSettingsSelected;
    public string DashboardDateText => DateTime.Today.ToString("dddd, dd. MMMM yyyy");
    public string AppDataDirectory => AppPaths.AppDataDirectory;
    public string DatabasePath => AppPaths.DatabasePath;
    public string TasksDirectory => AppPaths.TasksDirectory;
    public string BackupDirectory => AppPaths.BackupDirectory;

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
        }
    }

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
    }

    private void RefreshVisibleTasks()
    {
        if (IsOverviewSelected)
        {
            ShowOverview();
            return;
        }

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
            VisibleTasks.Add(task);
        }

        TaskListCaption = VisibleTasks.Count == 1 ? "1 Aufgabe" : $"{VisibleTasks.Count} Aufgaben";
        if (SelectedTask is null || !VisibleTasks.Contains(SelectedTask))
        {
            SelectedTask = VisibleTasks.FirstOrDefault();
        }
    }

    private void ShowOverview()
    {
        ClearSelectedTask();
        VisibleTasks.Clear();
        TaskListCaption = "Tagesübersicht";
        SearchText = string.Empty;
        RefreshDashboard();
    }

    private void ShowSettings()
    {
        ClearSelectedTask();
        VisibleTasks.Clear();
        TaskListCaption = "Einstellungen";
        SearchText = string.Empty;
    }

    private void ClearSelectedTask()
    {
        SaveCurrentMaterials();
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
            Color = "#F2F3F5",
            IsVisible = true
        };

        _repository.SaveCategory(category);
        InsertBeforeSettings(category);
        RefreshTaskCategories();
        SelectedCategory = category;
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
            Title = source.Title.StartsWith("Kopie - ", StringComparison.OrdinalIgnoreCase) ? source.Title : $"Kopie - {source.Title}",
            Description = source.Description,
            CategoryId = source.CategoryId,
            Status = source.Status,
            Priority = source.Priority,
            DueDate = source.DueDate,
            FollowUpDate = source.FollowUpDate,
            AssignedTo = source.AssignedTo,
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
        if (_isLoadingSelection || SelectedTask is null)
        {
            return;
        }

        if (sender is ComboBox { SelectedItem: string status })
        {
            SelectedTask.Status = status;
        }

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

        var category = Categories.FirstOrDefault(c => c.Id == task.CategoryId)
            ?? Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben")
            ?? Categories.FirstOrDefault(c => c.Name != "Übersicht");
        if (category is null)
        {
            return;
        }

        SelectedCategory = category;
        SelectedTask = task;
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SearchText = textBox.Text ?? string.Empty;
        }
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
        RefreshVisibleTasks();
    }

    private void TaskList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedTask));
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
        if (Contains(task.CustomerName, query) ||
            Contains(task.Title, query) ||
            Contains(task.Description, query) ||
            Contains(task.AssignedTo, query))
        {
            return true;
        }

        return _repository.GetMaterials(task.Id).Any(material => Contains(material.Name, query));
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
