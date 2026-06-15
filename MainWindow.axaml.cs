using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using BueroCockpit.Data;
using BueroCockpit.Models;
using BueroCockpit.Services;

namespace BueroCockpit;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private bool _dateTextFormattingActive;
    private const string DeskCategoryId = "__desk";
    private const string DeskCategoryName = "Schreibtisch";
    private const string OverviewCategoryName = "Übersicht";
    private const string SettingsCategoryId = "__settings";
    private const string SettingsCategoryName = "Einstellungen";
    private const string DeskItemTypeNote = "Note";
    private const string DeskItemTypeFile = "File";
    private const string DeskItemTypePdf = "Pdf";
    private const string DeskItemTypeImage = "Image";
    private static readonly HashSet<string> DeskImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp"
    };
    private const string LightMode = "Light Mode";
    private const string DarkMode = "Dark Mode";
    private readonly StorageLocationService _storageLocationService = new();
    private readonly AppInstanceLockService _appInstanceLockService = new();
    private readonly BueroRepository _repository;
    private readonly ThumbnailService _thumbnailService;
    private readonly BackupService _backupService;
    private readonly UpdateService _updateService;
    private readonly AppSettingsService _settingsService;
    private readonly FileHashService _hashService;
    private readonly HashSet<string> _tasksPendingDuplicateCheck = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _appSettings = new();
    private CategoryItem? _selectedCategory;
    private TaskItem? _selectedTask;
    private CategoryItem? _selectedTaskCategory;
    private AttachmentItem? _selectedAttachment;
    private AttachmentEditSession? _selectedAttachmentEditSession;
    private DeskItem? _draggedDeskItem;
    private Control? _draggedDeskContainer;
    private IPointer? _draggedDeskPointer;
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
    private string _storageLocationStatus = "Speicherort nicht geändert.";
    private string _appInstanceLockStatus = "Datenordner-Zugriffsschutz noch nicht geprüft.";
    private string _lastBackupPath = string.Empty;
    private string _lastBackupTime = string.Empty;
    private string _updateStatus = "Noch kein Update-Kanal eingerichtet.";
    private string _updateFeedUrl = string.Empty;
    private string _appearanceMode = DarkMode;
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
    private Point _deskDragStartPointerPosition;
    private Point _deskDragStartItemPosition;
    private Vector _deskDragCurrentDelta;
    private bool _isDraggingDeskItem;
    private IPointer? _deskPanPointer;
    private Point _deskPanStartPointerPosition;
    private Vector _deskPanStartOffset;
    private bool _isLoadingDeskItems;
    private bool _isApplyingDeskDrag;
    private bool _deskInitialViewApplied;
    private double _deskFitZoom = 1.0;
    private double _deskUserZoom = 1.0;
    private double _deskSurfaceWidth = 2400;
    private double _deskSurfaceHeight = 1600;
    private const double DeskBaseSurfaceWidth = 2400;
    private const double DeskBaseSurfaceHeight = 1600;
    private const double DeskMinZoom = 0.2;
    private const double DeskMaxZoom = 3.0;
    private const double DeskFitMargin = 120;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<CategoryItem> TaskCategories { get; } = new();
    public ObservableCollection<TaskCategorySelection> TaskCategorySelections { get; } = new();
    public ObservableCollection<TaskItem> AllTasks { get; } = new();
    public ObservableCollection<TaskItem> VisibleTasks { get; } = new();
    public ObservableCollection<TaskSearchResult> GlobalSearchResults { get; } = new();
    public ObservableCollection<MaterialItem> Materials { get; } = new();
    public ObservableCollection<AttachmentItem> Attachments { get; } = new();
    public ObservableCollection<DashboardSection> DashboardSections { get; } = new();
    public ObservableCollection<DeskItem> DeskItems { get; } = new();

    public string[] SortModeOptions { get; } = ["Manuell", "Termin", "Wiedervorlage", "Gesendet am", "Geändert am"];
    public string[] StatusOptions { get; } = ["Offen", "Wartet auf Kunde", "Material offen", "Terminiert", "Erledigt", "Archiv"];
    public ObservableCollection<string> TechnicianOptions { get; } = new();
    private string _newTechnicianName = string.Empty;

    public string NewTechnicianName
    {
        get => _newTechnicianName;
        set
        {
            if (_newTechnicianName == value)
            {
                return;
            }

            _newTechnicianName = value;
            OnPropertyChanged(nameof(NewTechnicianName));
        }
    }

    public string[] PriorityOptions { get; } = ["Niedrig", "Normal", "Hoch", "Dringend"];
    public string[] MaterialStatusOptions { get; } = ["benötigt", "bestellt", "vorhanden", "verbaut", "retour", "erledigt"];
    public string[] AppearanceModeOptions { get; } = [LightMode, DarkMode];

    public string DeskZoomLabel => $"{Math.Round(_deskUserZoom * 100):0} %";

    public double DeskZoom => _deskFitZoom * _deskUserZoom;

    public Avalonia.Media.ITransform DeskZoomTransform => new ScaleTransform(DeskZoom, DeskZoom);

    public double DeskSurfaceWidth
    {
        get => _deskSurfaceWidth;
        private set
        {
            if (Math.Abs(_deskSurfaceWidth - value) < 0.5)
            {
                return;
            }

            _deskSurfaceWidth = value;
            OnPropertyChanged(nameof(DeskSurfaceWidth));
        }
    }

    public double DeskSurfaceHeight
    {
        get => _deskSurfaceHeight;
        private set
        {
            if (Math.Abs(_deskSurfaceHeight - value) < 0.5)
            {
                return;
            }

            _deskSurfaceHeight = value;
            OnPropertyChanged(nameof(DeskSurfaceHeight));
        }
    }

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

            if (IsUnsavedPlaceholderTask(_selectedTask))
            {
                RemovePendingNewTask(_selectedTask!);
            }

            _selectedCategory = value;
            if (_selectedCategory is not null)
            {
                _selectedCategory.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(IsOverviewSelected));
            OnPropertyChanged(nameof(IsDeskSelected));
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

            var removedPendingPlaceholder = false;
            if (_selectedTask is not null && value is not null && IsUnsavedPlaceholderTask(_selectedTask))
            {
                RemovePendingNewTask(_selectedTask);
                removedPendingPlaceholder = true;
            }
            else if (!_suppressSavingDuringSelection)
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

            if (removedPendingPlaceholder)
            {
                RefreshVisibleTasks();
                UpdateCategoryCounts();
            }
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
                if (!SelectedTask.CategoryIds.Contains(value.Id, StringComparer.OrdinalIgnoreCase))
                {
                    SelectedTask.CategoryIds.Add(value.Id);
                }
                _repository.SaveTask(SelectedTask);
                RefreshTaskCategorySelections();
                RefreshVisibleTasks();
                UpdateCategoryCounts();
            }
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
    public bool IsDeskSelected => SelectedCategory?.Id == DeskCategoryId;
    public bool IsSettingsSelected => SelectedCategory?.Id == SettingsCategoryId;
    public bool IsTaskAreaVisible => !IsOverviewSelected && !IsDeskSelected && !IsSettingsSelected;
    public string DashboardDateText => DateTime.Today.ToString("dddd, dd. MMMM yyyy");
    public string AppDataDirectory => AppPaths.AppDataDirectory;
    public string DefaultAppDataDirectory => AppPaths.DefaultAppDataDirectory;
    public string DatabasePath => AppPaths.DatabasePath;
    public string TasksDirectory => AppPaths.TasksDirectory;
    public string BackupDirectory => AppPaths.BackupDirectory;
    public string LockPath => AppPaths.LockPath;
    public string StorageLocationStatus
    {
        get => _storageLocationStatus;
        set
        {
            if (_storageLocationStatus != value)
            {
                _storageLocationStatus = value;
                OnPropertyChanged(nameof(StorageLocationStatus));
            }
        }
    }
    public string AppInstanceLockStatus
    {
        get => _appInstanceLockStatus;
        set
        {
            if (_appInstanceLockStatus != value)
            {
                _appInstanceLockStatus = value;
                OnPropertyChanged(nameof(AppInstanceLockStatus));
            }
        }
    }
    public string CurrentAppVersion => _updateService.GetCurrentVersion();
    public string UpdateSource => _updateService.UpdateSource;
    public string AutoUpdateInstallStatus => _updateService.IsVelopackAvailable()
        ? "Diese laufende Instanz: installierte Version. Auto-Update aktiv."
        : "Diese laufende Instanz: Entwicklungsstart. Auto-Update ist nur in der installierten Windows-Version aktiv.";
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

    public string AppearanceMode
    {
        get => _appearanceMode;
        set => SetAppearanceMode(value, persist: true);
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
            OnPropertyChanged(nameof(AttachmentPreviewTitle));
            OnPropertyChanged(nameof(AttachmentPreviewInfo));
        }
    }

    public bool HasSelectedAttachment => SelectedAttachment is not null;
    public string PreviewImagePath => SelectedAttachment?.ThumbnailPath ?? string.Empty;
    public bool HasPreviewImage => SelectedAttachment is not null &&
                                   File.Exists(SelectedAttachment.StoredPath) &&
                                   !string.IsNullOrWhiteSpace(PreviewImagePath) &&
                                   File.Exists(PreviewImagePath);
    public bool HasPreviewPlaceholder => HasSelectedAttachment && !HasPreviewImage;
    public string AttachmentPreviewTitle => SelectedAttachment is null
        ? string.Empty
        : File.Exists(SelectedAttachment.StoredPath)
            ? SelectedAttachment.FileName
            : "Datei nicht gefunden";
    public string AttachmentPreviewInfo
    {
        get
        {
            if (SelectedAttachment is null)
            {
                return string.Empty;
            }

            if (!File.Exists(SelectedAttachment.StoredPath))
            {
                return "Datei nicht gefunden.";
            }

            var extension = SelectedAttachment.FileType.TrimStart('.').ToLowerInvariant();
            var sizeText = FormatFileSize(new FileInfo(SelectedAttachment.StoredPath).Length);
            return extension == "msg"
                ? $"Outlook-Nachricht (.msg) - extern öffnen. {sizeText}"
                : $"{SelectedAttachment.FileTypeBadge} - {sizeText}";
        }
    }
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
        _storageLocationService.ApplyConfiguredDataDirectory();
        var lockResult = _appInstanceLockService.Acquire();
        AppInstanceLockStatus = FormatAppInstanceLockStatus(lockResult);

        _repository = new BueroRepository();
        _thumbnailService = new ThumbnailService();
        _backupService = new BackupService();
        _updateService = new UpdateService();
        _settingsService = new AppSettingsService();
        _hashService = new FileHashService();

        _appSettings = _settingsService.Load();
        LoadTechnicianOptions();
        SetAppearanceMode(_appSettings.AppearanceMode, persist: false);
        InitializeComponent();
        if (!lockResult.IsAcquired)
        {
            Title = "BüroCockpit - Datenordner-Warnung";
        }

        Closed += (_, _) => _appInstanceLockService.Release();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _appInstanceLockService.Release();
        Console.CancelKeyPress += (_, _) => _appInstanceLockService.Release();
        UpdateFeedUrl = _appSettings.UpdateFeedUrl;
        _updateService.UpdateFeedUrl = UpdateFeedUrl;
        UpdateStatus = _updateService.GetUpdateStatusText();
        DataContext = this;
        Loaded += MainWindow_OnLoaded;
        DeskScrollViewer.AddHandler(
            InputElement.PointerWheelChangedEvent,
            DeskScrollViewer_OnPointerWheelChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        _repository.Initialize();
        LoadData();
    }

    private static string FormatAppInstanceLockStatus(AppInstanceLockResult lockResult)
    {
        if (lockResult.IsAcquired)
        {
            return lockResult.Message;
        }

        if (lockResult.ExistingLock is null)
        {
            return lockResult.Message;
        }

        var existing = lockResult.ExistingLock;
        return lockResult.Message;
    }


    private void TaskCard_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = Brushes.Black;
            border.BorderThickness = new Thickness(1);
        }
    }

    private void TaskCard_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(1);
        }
    }

    private void MainWindow_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_deskInitialViewApplied)
        {
            return;
        }

        Dispatcher.UIThread.Post(FitDeskToViewport, DispatcherPriority.Loaded);
    }

    private void DeskNoteHeader_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeskItem deskItem)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var noteContainer = FindDeskNoteContainer(control);
        if (noteContainer is null)
        {
            return;
        }

        _draggedDeskItem = deskItem;
        _draggedDeskContainer = noteContainer;
        _draggedDeskPointer = e.Pointer;
        _deskDragStartPointerPosition = GetDeskLogicalPointerPosition(e);
        _deskDragStartItemPosition = new Point(deskItem.X, deskItem.Y);
        _deskDragCurrentDelta = default;
        _isDraggingDeskItem = false;
        _draggedDeskContainer.RenderTransform = new TranslateTransform();
        _draggedDeskPointer.Capture(control);
        e.Handled = true;
    }

    private void DeskSurface_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control surface || IsPointerInsideDeskNote(e))
        {
            return;
        }

        if (!e.GetCurrentPoint(surface).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _deskPanPointer = e.Pointer;
        _deskPanStartPointerPosition = e.GetPosition(DeskScrollViewer);
        _deskPanStartOffset = DeskScrollViewer.Offset;
        _deskPanPointer.Capture(surface);
        e.Handled = true;
    }

    private void DeskSurface_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_deskPanPointer is null)
        {
            return;
        }

        var currentPosition = e.GetPosition(DeskScrollViewer);
        var delta = currentPosition - _deskPanStartPointerPosition;
        SetDeskOffset(_deskPanStartOffset - new Vector(delta.X, delta.Y));
        e.Handled = true;
    }

    private void DeskSurface_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_deskPanPointer is null)
        {
            return;
        }

        ClearDeskPanState();
        e.Handled = true;
    }

    private void DeskSurface_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ClearDeskPanState();
    }

    private void DeskSurface_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasLocalFiles = e.DataTransfer.TryGetFiles()?.Any(file =>
            File.Exists(file.TryGetLocalPath() ?? string.Empty)) == true;
        e.DragEffects = hasLocalFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DeskSurface_OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            e.Handled = true;
            return;
        }

        var localPaths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .ToList();

        if (localPaths.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var dropPoint = e.GetPosition(DeskScrollViewer);
        var addedCount = 0;
        for (var index = 0; index < localPaths.Count; index++)
        {
            if (AddDeskFileCardFromPath(localPaths[index]!, dropPoint, index))
            {
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            SelectedCategory = Categories.FirstOrDefault(category => category.Id == DeskCategoryId) ?? SelectedCategory;
            ApplySelectedCategoryContent();
        }

        e.Handled = true;
    }

    private void DeskNoteHeader_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedDeskItem is null || _draggedDeskContainer is null || _draggedDeskPointer is null)
        {
            return;
        }

        var currentPosition = GetDeskLogicalPointerPosition(e);
        var deltaX = currentPosition.X - _deskDragStartPointerPosition.X;
        var deltaY = currentPosition.Y - _deskDragStartPointerPosition.Y;

        if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
        {
            _isDraggingDeskItem = true;
        }

        _deskDragCurrentDelta = new Vector(deltaX, deltaY);
        if (_draggedDeskContainer.RenderTransform is TranslateTransform translateTransform)
        {
            translateTransform.X = deltaX;
            translateTransform.Y = deltaY;
        }

        e.Handled = true;
    }

    private void DeskNoteHeader_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedDeskItem is null || _draggedDeskContainer is null || _draggedDeskPointer is null)
        {
            return;
        }

        CommitDeskDrag();
        ClearDeskDragState();
        e.Handled = true;
    }

    private void DeskScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var pointerPosition = e.GetPosition(DeskScrollViewer);
        var zoomFactor = e.Delta.Y > 0 ? 1.12 : 1 / 1.12;
        SetDeskUserZoom(_deskUserZoom * zoomFactor, pointerPosition);
        e.Handled = true;
    }

    private void DeskZoomIn_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDeskUserZoom(_deskUserZoom * 1.15, GetDeskViewportCenter());
    }

    private void DeskZoomOut_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDeskUserZoom(_deskUserZoom / 1.15, GetDeskViewportCenter());
    }

    private void DeskZoomReset_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDeskUserZoom(1.0, GetDeskViewportCenter());
    }

    private void DeskFitToView_OnClick(object? sender, RoutedEventArgs e)
    {
        FitDeskToViewport();
    }

    private void DeskNoteHeader_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CommitDeskDrag();
        ClearDeskDragState();
    }

    private void DeskFileCard_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeskItem deskItem)
        {
            return;
        }

        if (!deskItem.IsFileCard || !deskItem.HasFile)
        {
            return;
        }

        e.Handled = true;
        OpenDeskFileExternal(deskItem);
    }

    private void ToggleDeskItemImportant_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        deskItem.IsImportant = !deskItem.IsImportant;
    }

    private void DeskItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingDeskItems || _isApplyingDeskDrag || sender is not DeskItem deskItem)
        {
            return;
        }

        if (e.PropertyName is null ||
            e.PropertyName == nameof(DeskItem.Text) ||
            e.PropertyName == nameof(DeskItem.FilePath) ||
            e.PropertyName == nameof(DeskItem.FileName) ||
            e.PropertyName == nameof(DeskItem.ReferencePath) ||
            e.PropertyName == nameof(DeskItem.ThumbnailPath) ||
            e.PropertyName == nameof(DeskItem.IsImportant) ||
            e.PropertyName == nameof(DeskItem.X) ||
            e.PropertyName == nameof(DeskItem.Y) ||
            e.PropertyName == nameof(DeskItem.Width) ||
            e.PropertyName == nameof(DeskItem.Height) ||
            e.PropertyName == nameof(DeskItem.Type))
        {
            deskItem.UpdatedAt = DateTime.Now;
            _repository.SaveDeskItem(deskItem);
            UpdateDeskSurfaceBounds();
        }
    }

    private void FitDeskToViewport()
    {
        if (DeskScrollViewer.Viewport.Width <= 0 || DeskScrollViewer.Viewport.Height <= 0)
        {
            Dispatcher.UIThread.Post(FitDeskToViewport, DispatcherPriority.Loaded);
            return;
        }

        // Die Start-/Komplettansicht bezieht sich bewusst auf die feste Schreibtischfläche,
        // nicht auf die aktuell vorhandenen Notizzettel. Sonst werden einzelne Zettel beim
        // Neustart automatisch zentriert/vergrößert und wirken, als hätten sie ihre Position verloren.
        var width = Math.Max(DeskSurfaceWidth, 1);
        var height = Math.Max(DeskSurfaceHeight, 1);
        var zoom = Math.Min(
            DeskScrollViewer.Viewport.Width / width,
            DeskScrollViewer.Viewport.Height / height);

        SetDeskFitZoom(zoom);
        _deskUserZoom = 1.0;
        OnPropertyChanged(nameof(DeskZoomLabel));
        OnPropertyChanged(nameof(DeskZoom));
        OnPropertyChanged(nameof(DeskZoomTransform));

        // Start immer oben links auf der logischen Schreibtischfläche.
        SetDeskOffset(new Vector(0, 0));
        _deskInitialViewApplied = true;
    }

    private void SetDeskFitZoom(double fitZoom)
    {
        var clampedFitZoom = Math.Clamp(fitZoom, DeskMinZoom, DeskMaxZoom);
        if (Math.Abs(_deskFitZoom - clampedFitZoom) < 0.0001)
        {
            return;
        }

        _deskFitZoom = clampedFitZoom;
        OnPropertyChanged(nameof(DeskZoom));
        OnPropertyChanged(nameof(DeskZoomTransform));
    }

    private void SetDeskUserZoom(double targetUserZoom, Point? focusViewportPoint)
    {
        var previousZoom = DeskZoom;
        var clampedUserZoom = Math.Clamp(targetUserZoom, DeskMinZoom, DeskMaxZoom);
        if (Math.Abs(_deskUserZoom - clampedUserZoom) < 0.0001)
        {
            return;
        }

        var viewport = DeskScrollViewer.Viewport;
        var focus = focusViewportPoint ?? new Point(viewport.Width / 2, viewport.Height / 2);
        var offset = DeskScrollViewer.Offset;
        var logicalFocus = new Point(
            (offset.X + focus.X) / previousZoom,
            (offset.Y + focus.Y) / previousZoom);

        _deskUserZoom = clampedUserZoom;
        OnPropertyChanged(nameof(DeskZoom));
        OnPropertyChanged(nameof(DeskZoomLabel));
        OnPropertyChanged(nameof(DeskZoomTransform));
        SetDeskOffset(new Vector(
            logicalFocus.X * DeskZoom - focus.X,
            logicalFocus.Y * DeskZoom - focus.Y));
    }

    private void SetDeskOffset(Vector offset)
    {
        DeskScrollViewer.Offset = new Vector(
            Math.Max(0, offset.X),
            Math.Max(0, offset.Y));
    }

    private Rect GetDeskContentBounds()
    {
        if (DeskItems.Count == 0)
        {
            return new Rect(0, 0, DeskBaseSurfaceWidth, DeskBaseSurfaceHeight);
        }

        var minX = Math.Max(0, DeskItems.Min(item => item.X) - DeskFitMargin);
        var minY = Math.Max(0, DeskItems.Min(item => item.Y) - DeskFitMargin);
        var maxX = DeskItems.Max(item => item.X + item.Width) + DeskFitMargin;
        var maxY = DeskItems.Max(item => item.Y + item.Height) + DeskFitMargin;
        return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private Point GetDeskLogicalPointerPosition(PointerEventArgs e)
    {
        var pointerPosition = e.GetPosition(DeskScrollViewer);
        var offset = DeskScrollViewer.Offset;
        return new Point(
            (offset.X + pointerPosition.X) / DeskZoom,
            (offset.Y + pointerPosition.Y) / DeskZoom);
    }

    private Point GetDeskViewportCenter()
    {
        return new Point(DeskScrollViewer.Viewport.Width / 2, DeskScrollViewer.Viewport.Height / 2);
    }

    private void CommitDeskDrag()
    {
        if (_draggedDeskItem is null || _draggedDeskContainer is null)
        {
            return;
        }

        if (!_isDraggingDeskItem &&
            Math.Abs(_deskDragCurrentDelta.X) <= 0.5 &&
            Math.Abs(_deskDragCurrentDelta.Y) <= 0.5)
        {
            return;
        }

        _isApplyingDeskDrag = true;
        try
        {
            _draggedDeskItem.X = Math.Max(0, _deskDragStartItemPosition.X + _deskDragCurrentDelta.X);
            _draggedDeskItem.Y = Math.Max(0, _deskDragStartItemPosition.Y + _deskDragCurrentDelta.Y);
        }
        finally
        {
            _isApplyingDeskDrag = false;
        }

        _draggedDeskItem.UpdatedAt = DateTime.Now;
        _repository.SaveDeskItem(_draggedDeskItem);
        UpdateDeskSurfaceBounds();
    }


    private void LoadData()
    {
        Categories.Clear();
        Categories.Add(CreateDeskCategory());
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

        LoadDeskItems();
        UpdateCategoryCounts();
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == DeskCategoryId)
            ?? Categories.FirstOrDefault(c => c.Name == OverviewCategoryName)
            ?? Categories.FirstOrDefault();
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

        if (IsDeskSelected)
        {
            ShowDesk();
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
                : AllTasks.Where(t => TaskBelongsToCategory(t, selected.Id));

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.Trim();
                tasks = tasks.Where(task => TaskMatchesSearch(task, query));
            }

            foreach (var task in tasks)
            {
                task.CategoryNameChips = GetTaskCategoryNameList(task);
                task.CategoryHint = string.Join(", ", task.CategoryNameChips);
                task.ShowCategoryHint = task.CategoryNameChips.Count > 0;
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
        var categoryName = GetTaskCategoryNames(task);
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
        else if (IsDeskSelected)
        {
            ShowDesk();
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

    private void ShowDesk()
    {
        ClearSelectedTask();
        VisibleTasks.Clear();
        TaskListCaption = "Schreibtisch";
        ClearSearchTextWithoutRefresh();
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
        if (IsUnsavedPlaceholderTask(_selectedTask))
        {
            RemovePendingNewTask(_selectedTask!);
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

        _selectedTask = null;
        SelectedAttachment = null;
        SelectedTaskCategory = null;
        Materials.Clear();
        Attachments.Clear();
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(HasSelectedTask));
        OnPropertyChanged(nameof(HasNoMaterials));
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
            category.TaskCount = category.Name == OverviewCategoryName
                ? AllTasks.Count
                : category.Id == DeskCategoryId || category.Id == SettingsCategoryId
                    ? 0
                : AllTasks.Count(t => TaskBelongsToCategory(t, category.Id));
        }
    }

    private void LoadDeskItems()
    {
        foreach (var item in DeskItems)
        {
            UnsubscribeDeskItem(item);
        }

        DeskItems.Clear();
        _isLoadingDeskItems = true;
        try
        {
            foreach (var item in _repository.GetDeskItems())
            {
                SubscribeDeskItem(item);
                DeskItems.Add(item);
            }
        }
        finally
        {
            _isLoadingDeskItems = false;
        }

        UpdateDeskSurfaceBounds();
        RefreshDeskFileCards();
    }

    private void RefreshDeskFileCards()
    {
        if (DeskItems.Count == 0)
        {
            return;
        }

        foreach (var deskItem in DeskItems.Where(item => item.IsFileCard))
        {
            EnsureDeskFilePreview(deskItem);
        }
    }

    private void EnsureDeskFilePreview(DeskItem deskItem)
    {
        if (!deskItem.IsFileCard || !deskItem.HasFile)
        {
            return;
        }

        var previewText = BuildDeskFilePreviewText(deskItem.FilePath);
        if (!string.Equals(previewText ?? string.Empty, deskItem.Text ?? string.Empty, StringComparison.Ordinal))
        {
            deskItem.Text = previewText ?? string.Empty;
        }

        var previewPath = EnsureDeskFileThumbnail(deskItem);
        if (!string.IsNullOrWhiteSpace(previewPath) &&
            !string.Equals(previewPath, deskItem.ThumbnailPath, StringComparison.OrdinalIgnoreCase))
        {
            deskItem.ThumbnailPath = previewPath;
            _repository.SaveDeskItem(deskItem);
        }
    }

    private string? EnsureDeskFileThumbnail(DeskItem deskItem)
    {
        if (!deskItem.HasFile)
        {
            return null;
        }

        var extension = Path.GetExtension(deskItem.FilePath);
        if (!DeskImageExtensions.Contains(extension) &&
            !string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var previewAttachment = new AttachmentItem
        {
            Id = deskItem.Id,
            StoredPath = deskItem.FilePath,
            ThumbnailPath = deskItem.ThumbnailPath,
            FileName = deskItem.FileName,
            FileType = Path.GetExtension(deskItem.FileName)
        };

        return _thumbnailService.EnsureThumbnail(previewAttachment);
    }

    private static string? BuildDeskFilePreviewText(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var lines = File.ReadLines(filePath).Take(4).ToList();
            if (lines.Count == 0)
            {
                return null;
            }

            var preview = string.Join(Environment.NewLine, lines).Trim();
            if (preview.Length <= 240)
            {
                return preview;
            }

            return preview[..240].TrimEnd() + "...";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read desk file preview '{filePath}': {ex}");
            return null;
        }
    }

    private static (double Width, double Height) GetDeskFileCardSize(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (200, 300);
        }

        if (DeskImageExtensions.Contains(extension))
        {
            return (235, 275);
        }

        if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            return (240, 210);
        }

        return (220, 160);
    }

    private Point GetDeskDropLogicalPosition(Point positionOnViewer, double width, double height)
    {
        var offset = DeskScrollViewer.Offset;
        var logicalX = (offset.X + positionOnViewer.X) / DeskZoom;
        var logicalY = (offset.Y + positionOnViewer.Y) / DeskZoom;
        return new Point(
            Math.Max(40, logicalX - width / 2),
            Math.Max(40, logicalY - height / 2));
    }

    private string CreateDeskFileStoredPath(string originalFileName, string deskItemId)
    {
        var deskFileDirectory = AppPaths.GetDeskFileDirectory(deskItemId);
        Directory.CreateDirectory(deskFileDirectory);
        var safeFileName = CreateStoredFileName(originalFileName);
        return Path.Combine(deskFileDirectory, safeFileName);
    }

    private void RefreshDeskFileCard(DeskItem deskItem)
    {
        EnsureDeskFilePreview(deskItem);
    }

    private void UpdateDeskSurfaceBounds()
    {
        var visibleItems = DeskItems.Where(item => item.IsNoteCard || item.IsFileCard).ToList();
        var maxX = visibleItems.Count == 0
            ? DeskSurfaceWidth
            : visibleItems.Max(item => item.X + item.Width + DeskFitMargin * 2);
        var maxY = visibleItems.Count == 0
            ? DeskSurfaceHeight
            : visibleItems.Max(item => item.Y + item.Height + DeskFitMargin * 2);

        DeskSurfaceWidth = Math.Max(DeskBaseSurfaceWidth, Math.Ceiling(maxX));
        DeskSurfaceHeight = Math.Max(DeskBaseSurfaceHeight, Math.Ceiling(maxY));
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
            EnsureTaskCategoryState(SelectedTask);
            SelectedTaskCategory = Categories.FirstOrDefault(c => c.Id == SelectedTask.CategoryId);
            RefreshTaskCategorySelections();

            foreach (var item in _repository.GetMaterials(SelectedTask.Id))
            {
                Materials.Add(item);
            }
            OnPropertyChanged(nameof(HasNoMaterials));

            foreach (var item in _repository.GetAttachments(SelectedTask.Id))
            {
                EnsureAttachmentThumbnail(item);
                Attachments.Insert(0, item);
            }
        }
        else
        {
            SelectedTaskCategory = null;
            RefreshTaskCategorySelections();
        }

        _isLoadingSelection = false;
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private void NewTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (IsUnsavedPlaceholderTask(SelectedTask))
        {
            ClearSelectedTask();
        }

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
        _tasksPendingDuplicateCheck.Add(task.Id);
        AllTasks.Insert(0, task);
        SelectedCategory = category;
        RefreshVisibleTasks();
        SelectedTask = task;
        UpdateCategoryCounts();
    }

    private static string GetDeskItemTypeForFile(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return DeskItemTypePdf;
        }

        if (DeskImageExtensions.Contains(extension))
        {
            return DeskItemTypeImage;
        }

        return DeskItemTypeFile;
    }

    private bool AddDeskFileCardFromPath(string? sourcePath, Point dropPosition, int index)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !File.Exists(sourcePath))
        {
            return false;
        }

        var now = DateTime.Now;
        var fileName = Path.GetFileName(sourcePath);
        var cardSize = GetDeskFileCardSize(sourcePath);
        var deskItem = new DeskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = GetDeskItemTypeForFile(sourcePath),
            FilePath = string.Empty,
            FileName = fileName,
            ReferencePath = sourcePath,
            ThumbnailPath = string.Empty,
            Text = string.Empty,
            Width = cardSize.Width,
            Height = cardSize.Height,
            IsImportant = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var storedPath = CreateDeskFileStoredPath(fileName, deskItem.Id);
        File.Copy(sourcePath, storedPath, overwrite: false);
        deskItem.FilePath = storedPath;
        deskItem.Text = BuildDeskFilePreviewText(storedPath) ?? string.Empty;

        var offsetPosition = new Point(dropPosition.X + index * 24, dropPosition.Y + index * 24);
        var logicalPosition = GetDeskDropLogicalPosition(offsetPosition, deskItem.Width, deskItem.Height);
        deskItem.X = logicalPosition.X;
        deskItem.Y = logicalPosition.Y;

        _repository.SaveDeskItem(deskItem);
        SubscribeDeskItem(deskItem);
        DeskItems.Add(deskItem);
        EnsureDeskFilePreview(deskItem);
        UpdateDeskSurfaceBounds();
        return true;
    }

    private void AddDeskNote_OnClick(object? sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        var offset = DeskItems.Count * 28;
        var deskItem = new DeskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DeskItemTypeNote,
            Text = string.Empty,
            X = 40 + (offset % 320),
            Y = 40 + (offset % 220),
            Width = 300,
            Height = 210,
            IsImportant = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.SaveDeskItem(deskItem);
        SubscribeDeskItem(deskItem);
        DeskItems.Add(deskItem);
        UpdateDeskSurfaceBounds();
    }

    private void DeleteDeskItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        DeleteDeskItem(deskItem);
    }

    private async void SaveTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        ApplySelectedTaskStatusRules();

        if (_tasksPendingDuplicateCheck.Contains(SelectedTask.Id) &&
            await HandleNewTaskDuplicateAsync(SelectedTask))
        {
            return;
        }

        _repository.SaveTask(SelectedTask);
        _tasksPendingDuplicateCheck.Remove(SelectedTask.Id);
        SaveCurrentMaterials();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private async void TaskCategoryCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingSelection ||
            _isUpdatingSelection ||
            _isRefreshingVisibleTasks ||
            _selectionNavigationDepth > 0 ||
            SelectedTask is null ||
            sender is not CheckBox checkBox ||
            checkBox.DataContext is not TaskCategorySelection selection)
        {
            return;
        }

        var categoryId = selection.Category.Id;
        if (!EnsureTaskCategoryState(SelectedTask))
        {
            RefreshTaskCategorySelections();
            return;
        }

        var wasAlreadyAssigned = SelectedTask.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase);

        if (checkBox.IsChecked == true)
        {
            if (!wasAlreadyAssigned)
            {
                var duplicate = FindSimilarTask(SelectedTask, categoryId);

                if (duplicate is not null)
                {
                    var dialog = new DuplicateTaskDialog(
                        duplicate.Task,
                        GetTaskCategoryNames(duplicate.Task),
                        selection.Category.Name,
                        duplicate.Score);

                    var choice = await dialog.ShowDialog<DuplicateTaskChoice>(this);

                    switch (choice)
                    {
                        case DuplicateTaskChoice.AddToCategory:
                            AddTaskToCategory(duplicate.Task, categoryId);
                            _repository.SaveTask(duplicate.Task);
                            NavigateToTask(duplicate.Task, fromGlobalSearch: false);
                            RefreshTaskCategorySelections();
                            RefreshVisibleTasks();
                            UpdateCategoryCounts();
                            return;

                        case DuplicateTaskChoice.MoveToCategory:
                            duplicate.Task.CategoryIds = [categoryId];
                            duplicate.Task.CategoryId = categoryId;
                            _repository.SaveTask(duplicate.Task);
                            NavigateToTask(duplicate.Task, fromGlobalSearch: false);
                            RefreshTaskCategorySelections();
                            RefreshVisibleTasks();
                            UpdateCategoryCounts();
                            return;

                        case DuplicateTaskChoice.Cancel:
                            RefreshTaskCategorySelections();
                            return;

                        case DuplicateTaskChoice.CreateAnyway:
                        default:
                            break;
                    }
                }

                SelectedTask.CategoryIds.Add(categoryId);
            }
        }
        else
        {
            if (SelectedTask.CategoryIds.Count <= 1 &&
                SelectedTask.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase))
            {
                RefreshTaskCategorySelections();
                return;
            }

            SelectedTask.CategoryIds.RemoveAll(id => string.Equals(id, categoryId, StringComparison.OrdinalIgnoreCase));

            if (SelectedTask.CategoryIds.Count == 0)
            {
                SelectedTask.CategoryIds.Add(categoryId);
                RefreshTaskCategorySelections();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedTask.CategoryId) ||
                string.Equals(SelectedTask.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase) ||
                !SelectedTask.CategoryIds.Contains(SelectedTask.CategoryId, StringComparer.OrdinalIgnoreCase))
            {
                SelectedTask.CategoryId = SelectedTask.CategoryIds.First();
            }
        }

        if (!EnsureTaskCategoryState(SelectedTask))
        {
            RefreshTaskCategorySelections();
            return;
        }

        SelectedTaskCategory = Categories.FirstOrDefault(c => string.Equals(c.Id, SelectedTask.CategoryId, StringComparison.OrdinalIgnoreCase));
        _repository.SaveTask(SelectedTask);
        RefreshTaskCategorySelections();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }



    private void CategorySortMode_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingVisibleTasks || SelectedCategory is not { } category)
        {
            return;
        }

        _repository.SaveCategory(category);
        RefreshVisibleTasks();
    }

    private void AutoSortTasks_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedCategory is null)
        {
            return;
        }

        AutoSortCurrentCategory();
    }

    private void AutoSortCurrentCategory()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var categoryTasks = AllTasks
            .Where(task => TaskBelongsToCategory(task, SelectedCategory.Id))
            .ToList();

        if (categoryTasks.Count == 0)
        {
            return;
        }

        var sortedTasks = SortTasksForCategory(categoryTasks, SelectedCategory.SortMode).ToList();

        for (var index = 0; index < sortedTasks.Count; index++)
        {
            sortedTasks[index].SortPosition = index + 1;
            _repository.SaveTask(sortedTasks[index]);
        }

        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private static IEnumerable<TaskItem> SortTasksForCategory(IEnumerable<TaskItem> tasks, string? sortMode)
    {
        var mode = string.IsNullOrWhiteSpace(sortMode) ? "Geändert am" : sortMode.Trim();

        return mode switch
        {
            "Termin" => tasks
                .OrderBy(task => task.DueDate.HasValue ? 0 : 1)
                .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),

            "Wiedervorlage" => tasks
                .OrderBy(task => task.FollowUpDate.HasValue ? 0 : 1)
                .ThenBy(task => task.FollowUpDate ?? DateTime.MaxValue)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),

            "Gesendet am" => tasks
                .OrderBy(task => task.SentAt.HasValue ? 0 : 1)
                .ThenBy(task => task.SentAt ?? DateTime.MaxValue)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),

            "Manuell" => tasks
                .OrderBy(task => task.SortPosition)
                .ThenBy(task => task.CreatedAt),

            _ => tasks
                .OrderByDescending(task => task.UpdatedAt)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private void DeleteTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        _repository.DeleteTask(task.Id);
        _tasksPendingDuplicateCheck.Remove(task.Id);
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

        Materials.Insert(0, item);
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
            AllowMultiple = true
        });

        ImportAttachments(files.Select(file => file.TryGetLocalPath()), "hinzugefügt");
    }

    private bool AddAttachmentFromPath(string? sourcePath)
    {
        if (SelectedTask is null || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            _repository.SaveTask(SelectedTask);
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

            return true;
        }
        catch (Exception ex)
        {
            AttachmentEditStatus = $"Anhang konnte nicht hinzugefügt werden: {Path.GetFileName(sourcePath)}";
            Debug.WriteLine($"Attachment add failed for '{sourcePath}': {ex}");
            return false;
        }
    }

    private void AttachmentDropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.TryGetFiles()?.Any() == true;
        e.DragEffects = SelectedTask is null || !hasFiles
            ? DragDropEffects.None
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void AttachmentDropZone_OnDrop(object? sender, DragEventArgs e)
    {
        if (SelectedTask is null)
        {
            AttachmentEditStatus = "Bitte zuerst eine Aufgabe auswählen.";
            e.Handled = true;
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            e.Handled = true;
            return;
        }

        ImportAttachments(files.Select(file => file.TryGetLocalPath()), "per Drag & Drop hinzugefügt", "Keine Datei hinzugefügt.");

        e.Handled = true;
    }


    private void PrintSelectedAttachments_OnClick(object? sender, RoutedEventArgs e)
    {
        var selectedAttachments = Attachments
            .Where(attachment => attachment.IsSelectedForPrint)
            .ToList();

        if (selectedAttachments.Count == 0)
        {
            AttachmentEditStatus = "Bitte zuerst mindestens einen Anhang zum Drucken auswählen.";
            return;
        }

        var printedCount = 0;
        var missingCount = 0;

        foreach (var attachment in selectedAttachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.StoredPath) || !File.Exists(attachment.StoredPath))
            {
                missingCount++;
                continue;
            }

            if (PrintAttachmentExternal(attachment))
            {
                printedCount++;
            }
        }

        AttachmentEditStatus = missingCount == 0
            ? $"{printedCount} Anhang/Anhänge an die Druckfunktion übergeben."
            : $"{printedCount} Anhang/Anhänge an die Druckfunktion übergeben. {missingCount} Datei(en) nicht gefunden.";
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

        try
        {
            _repository.DeleteAttachment(item.Id);
        }
        catch (Exception ex)
        {
            AttachmentEditStatus = $"Anhang konnte nicht entfernt werden: {item.FileName}";
            Debug.WriteLine($"Attachment delete failed for '{item.Id}': {ex}");
            return;
        }

        var wasSelected = SelectedAttachment == item;
        var thumbnailPath = item.ThumbnailPath;
        var storedPath = item.StoredPath;

        DeleteAttachmentFileIfUnreferenced(storedPath);
        DeleteAttachmentFileIfUnreferenced(thumbnailPath);

        if (wasSelected)
        {
            SelectedAttachment = null;
        }

        Attachments.Remove(item);
        AttachmentEditStatus = $"Anhang entfernt: {item.FileName}";
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

    private void DeleteAttachmentFileIfUnreferenced(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || _repository.HasAttachmentPathReference(path))
        {
            return;
        }

        TryDeleteFile(path);
    }

    private void ImportAttachments(IEnumerable<string?> sourcePaths, string successSuffix, string? noneMessage = null)
    {
        var addedCount = 0;
        var failedCount = 0;

        foreach (var sourcePath in sourcePaths)
        {
            if (AddAttachmentFromPath(sourcePath))
            {
                addedCount++;
            }
            else if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                failedCount++;
            }
        }

        if (addedCount > 0)
        {
            AttachmentEditStatus = addedCount == 1
                ? $"1 Datei {successSuffix}."
                : $"{addedCount} Dateien {successSuffix}.";

            if (failedCount > 0)
            {
                AttachmentEditStatus += $" {failedCount} Datei(en) konnten nicht hinzugefügt werden.";
            }
        }
        else
        {
            if (failedCount > 0)
            {
                AttachmentEditStatus = $"{failedCount} Datei(en) konnten nicht hinzugefügt werden.";
            }
            else if (!string.IsNullOrWhiteSpace(noneMessage))
            {
                AttachmentEditStatus = noneMessage;
            }
        }
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
        OnPropertyChanged(nameof(SelectedCategory));
        CategoryMessage = "Kategorie umbenannt.";
    }


    private void MoveCategoryUp_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedCategory(-1);
    }

    private void MoveCategoryDown_OnClick(object? sender, RoutedEventArgs e)
    {
        MoveSelectedCategory(1);
    }

    private void MoveSelectedCategory(int direction)
    {
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            CategoryMessage = "Diese Kategorie kann nicht verschoben werden.";
            return;
        }

        var movableCategories = Categories
            .Where(category => !IsSpecialCategory(category))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var currentIndex = movableCategories.FindIndex(category => category.Id == SelectedCategory.Id);
        if (currentIndex < 0)
        {
            CategoryMessage = "Kategorie wurde nicht gefunden.";
            return;
        }

        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= movableCategories.Count)
        {
            CategoryMessage = direction < 0
                ? "Kategorie ist bereits ganz oben."
                : "Kategorie ist bereits ganz unten.";
            return;
        }

        var current = movableCategories[currentIndex];
        var target = movableCategories[targetIndex];

        (current.SortOrder, target.SortOrder) = (target.SortOrder, current.SortOrder);

        _repository.SaveCategory(current);
        _repository.SaveCategory(target);

        ReorderVisibleCategories(current.Id);
        RefreshTaskCategories();

        CategoryMessage = "Kategorie-Reihenfolge geändert.";
    }

    private void ReorderVisibleCategories(string selectedCategoryId)
    {
        var deskCategory = Categories.FirstOrDefault(category => category.Id == DeskCategoryId);
        var overviewCategory = Categories.FirstOrDefault(category => category.Name == OverviewCategoryName);
        var settingsCategory = Categories.FirstOrDefault(category => category.Id == SettingsCategoryId);

        var orderedCategories = Categories
            .Where(category => !IsSpecialCategory(category))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Categories.Clear();

        if (deskCategory is not null)
        {
            Categories.Add(deskCategory);
        }

        if (overviewCategory is not null)
        {
            Categories.Add(overviewCategory);
        }

        foreach (var category in orderedCategories)
        {
            Categories.Add(category);
        }

        if (settingsCategory is not null)
        {
            Categories.Add(settingsCategory);
        }

        SelectedCategory = Categories.FirstOrDefault(category => category.Id == selectedCategoryId)
            ?? deskCategory
            ?? overviewCategory
            ?? Categories.FirstOrDefault();

        ApplySelectedCategoryContent();
        UpdateCategoryCounts();
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


    private void DateTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_dateTextFormattingActive || sender is not TextBox textBox)
        {
            return;
        }

        var rawText = textBox.Text ?? string.Empty;
        var digits = new string(rawText.Where(char.IsDigit).Take(8).ToArray());

        var formatted = digits.Length switch
        {
            <= 1 => digits,
            2 => $"{digits}.",
            <= 3 => $"{digits[..2]}.{digits[2..]}",
            4 => $"{digits[..2]}.{digits[2..]}.",
            _ => $"{digits[..2]}.{digits[2..4]}.{digits[4..]}"
        };

        if (formatted == rawText)
        {
            return;
        }

        _dateTextFormattingActive = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _dateTextFormattingActive = false;
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

    private async void CategoryRenameFromMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CategoryItem category })
        {
            return;
        }

        var newName = await ShowRenameCategoryDialog(category.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        newName = newName.Trim();
        if (string.Equals(newName, category.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedCategory = category;
        CategoryEditorName = newName;
        RenameCategory_OnClick(sender, e);
    }

    private async Task<string?> ShowRenameCategoryDialog(string currentName)
    {
        var input = new TextBox
        {
            Text = currentName,
            MinWidth = 320,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        var dialog = new Window
        {
            Title = "Kategorie umbenennen",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = new TaskCompletionSource<string?>();

        var cancelButton = new Button
        {
            Content = "Abbrechen"
        };

        var renameButton = new Button
        {
            Content = "Umbenennen"
        };

        cancelButton.Click += (_, _) =>
        {
            result.TrySetResult(null);
            dialog.Close();
        };

        renameButton.Click += (_, _) =>
        {
            result.TrySetResult(input.Text);
            dialog.Close();
        };

        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Neuer Kategoriename"
                    },
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children =
                        {
                            cancelButton,
                            renameButton
                        }
                    }
                }
            }
        };

        dialog.Closed += (_, _) => result.TrySetResult(null);

        _ = dialog.ShowDialog(this);
        input.Focus();
        input.SelectAll();

        return await result.Task;
    }

    private void CategoryDeleteFromMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CategoryItem category })
        {
            SelectedCategory = category;
            CategoryEditorName = category.Name;
            HideCategory_OnClick(sender, e);
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

    private void OpenDefaultDataFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(AppPaths.DefaultAppDataDirectory);
    }

    private void OpenBackupFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(AppPaths.BackupDirectory);
    }

    private async void PrepareStorageLocation_OnClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            StorageLocationStatus = "Ordnerauswahl ist nicht verfügbar.";
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Neuen BüroCockpit-Datenordner auswählen",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var result = _storageLocationService.PrepareCustomDataDirectory(folderPath);
        StorageLocationStatus = result.Message;
    }

    private async void MigrateStorageLocation_OnClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            StorageLocationStatus = "Ordnerauswahl ist nicht verfügbar.";
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Zielordner für BüroCockpit-Daten auswählen",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var result = _storageLocationService.MigrateToCustomDataDirectory(folderPath);
        StorageLocationStatus = result.Message;
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.BackupPath))
        {
            LastBackupPath = result.BackupPath;
            LastBackupTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            BackupStatus = "Backup vor Datenordner-Migration wurde erstellt.";
        }
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
        OnPropertyChanged(nameof(AutoUpdateInstallStatus));
        IsUpdateAvailable = false;
        UpdateStatus = string.IsNullOrWhiteSpace(_appSettings.UpdateFeedUrl)
            ? "Standard-Updatekanal wird verwendet."
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
            OnPropertyChanged(nameof(AttachmentPreviewTitle));
            OnPropertyChanged(nameof(AttachmentPreviewInfo));

            EnsureAttachmentThumbnail(SelectedAttachment);
            session.Status = "Imported";
            session.ImportedAt = DateTime.Now;
            _repository.SaveAttachmentEditSession(session);
            var moveMessage = MoveImportedEditFileToDoneFolder(session.ExportPath);
            var statusMessage = "iPad-Bearbeitung übernommen. Alte Version wurde gesichert.";
            if (!string.IsNullOrWhiteSpace(moveMessage))
            {
                statusMessage += $" {moveMessage}";
            }

            SetSelectedAttachmentEditSession(session, statusMessage);
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(HasPreviewPlaceholder));
            OnPropertyChanged(nameof(AttachmentPreviewTitle));
            OnPropertyChanged(nameof(AttachmentPreviewInfo));
        }
        catch (Exception ex)
        {
            AttachmentEditStatus = "iPad-Bearbeitung konnte nicht übernommen werden.";
            Debug.WriteLine($"Attachment import failed: {ex}");
            UpdateImportAvailability();
        }
    }


    private static bool PrintAttachmentExternal(AttachmentItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.StoredPath) || !File.Exists(item.StoredPath))
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.StoredPath,
                    Verb = "print",
                    UseShellExecute = true
                });

                return true;
            }

            OpenAttachmentExternal(item);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Attachment print failed: {ex}");
            return false;
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

    private static void OpenDeskFileExternal(DeskItem deskItem)
    {
        if (!deskItem.HasFile)
        {
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = deskItem.FilePath,
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

        var category = Categories.FirstOrDefault(c => TaskBelongsToCategory(task, c.Id))
                       ?? Categories.FirstOrDefault(c => c.Id == task.CategoryId);
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
        MergeDuplicateMaterials();
        foreach (var item in Materials.Where(m => !string.IsNullOrWhiteSpace(m.TaskId)))
        {
            _repository.SaveMaterial(item);
        }
    }

    private void MergeDuplicateMaterials()
    {
        var seen = new Dictionary<string, MaterialItem>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<MaterialItem>();

        foreach (var item in Materials.Where(m => !string.IsNullOrWhiteSpace(m.TaskId)).Reverse().ToList())
        {
            var normalizedName = NormalizeMaterialKeyPart(item.Name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            var normalizedUnit = NormalizeMaterialKeyPart(item.Unit);
            var key = $"{normalizedName}|{normalizedUnit}";
            if (seen.TryGetValue(key, out var existing))
            {
                existing.Quantity += item.Quantity;
                duplicates.Add(item);
            }
            else
            {
                item.Name = item.Name.Trim();
                item.Unit = item.Unit.Trim();
                seen[key] = item;
            }
        }

        foreach (var duplicate in duplicates)
        {
            _repository.DeleteMaterial(duplicate.Id);
            Materials.Remove(duplicate);
        }

        if (duplicates.Count > 0)
        {
            OnPropertyChanged(nameof(HasNoMaterials));
        }
    }

    private static string NormalizeMaterialKeyPart(string value)
    {
        return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private bool TaskMatchesSearch(TaskItem task, string query)
    {
        var categoryName = GetTaskCategoryNames(task);
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

        var categoryNames = GetTaskCategoryNameList(task);
        return categoryNames.Any(categoryName => Contains(categoryName, "Angebote erstellen")) ||
               categoryNames.Any(categoryName => Contains(categoryName, "Wartet auf Kunde")) ||
               categoryNames.Any(categoryName => Contains(categoryName, "Material bestellen")) ||
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

    private async Task<bool> HandleNewTaskDuplicateAsync(TaskItem newTask)
    {
        var targetCategory = GetTaskTargetCategory(newTask);
        if (targetCategory is null || !HasMeaningfulDuplicateInput(newTask))
        {
            return false;
        }

        var duplicate = FindSimilarTask(newTask, minimumScore: 30);
        if (duplicate is null)
        {
            return false;
        }

        var dialog = new DuplicateTaskDialog(
            duplicate.Task,
            GetTaskCategoryNames(duplicate.Task),
            targetCategory.Name,
            duplicate.Score);
        var choice = await dialog.ShowDialog<DuplicateTaskChoice>(this);

        switch (choice)
        {
            case DuplicateTaskChoice.AddToCategory:
                AddTaskToCategory(duplicate.Task, targetCategory.Id);
                _repository.SaveTask(duplicate.Task);
                RemovePendingNewTask(newTask);
                NavigateToTask(duplicate.Task, fromGlobalSearch: false);
                UpdateCategoryCounts();
                return true;

            case DuplicateTaskChoice.MoveToCategory:
                duplicate.Task.CategoryIds = [targetCategory.Id];
                duplicate.Task.CategoryId = targetCategory.Id;
                _repository.SaveTask(duplicate.Task);
                RemovePendingNewTask(newTask);
                NavigateToTask(duplicate.Task, fromGlobalSearch: false);
                UpdateCategoryCounts();
                return true;

            case DuplicateTaskChoice.CreateAnyway:
                return false;

            case DuplicateTaskChoice.Cancel:
            default:
                RemovePendingNewTask(newTask);
                RefreshVisibleTasks();
                UpdateCategoryCounts();
                return true;
        }
    }

    private CategoryItem? GetTaskTargetCategory(TaskItem task)
    {
        if (!string.IsNullOrWhiteSpace(task.CategoryId))
        {
            var category = Categories.FirstOrDefault(item => string.Equals(item.Id, task.CategoryId, StringComparison.OrdinalIgnoreCase));
            if (category is not null && !IsSpecialCategory(category))
            {
                return category;
            }
        }

        foreach (var categoryId in task.CategoryIds)
        {
            var category = Categories.FirstOrDefault(item => string.Equals(item.Id, categoryId, StringComparison.OrdinalIgnoreCase));
            if (category is not null && !IsSpecialCategory(category))
            {
                return category;
            }
        }

        return SelectedCategory is not null && !IsSpecialCategory(SelectedCategory)
            ? SelectedCategory
            : null;
    }

    private SimilarTaskMatch? FindSimilarTask(TaskItem newTask, string? targetCategoryId = null, int minimumScore = 70)
    {
        return AllTasks
            .Where(task => !string.Equals(task.Id, newTask.Id, StringComparison.OrdinalIgnoreCase))
            .Where(task => string.IsNullOrWhiteSpace(targetCategoryId) ||
                task.CategoryIds.Contains(targetCategoryId, StringComparer.OrdinalIgnoreCase) ||
                string.Equals(task.CategoryId, targetCategoryId, StringComparison.OrdinalIgnoreCase))
            .Select(task => new SimilarTaskMatch(task, CalculateSimilarityScore(newTask, task)))
            .Where(match => match.Score >= minimumScore)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Task.UpdatedAt)
            .FirstOrDefault();
    }

    private static int CalculateSimilarityScore(TaskItem left, TaskItem right)
    {
        var score = 0;
        var customerNameScore = CompareNormalizedText(left.CustomerName, right.CustomerName, exactScore: 45, containsScore: 30);
        var titleScore = CompareNormalizedText(left.Title, right.Title, exactScore: 45, containsScore: 30);
        score += customerNameScore;
        score += titleScore;
        score += CompareNormalizedText(left.CustomerAddress, right.CustomerAddress, exactScore: 25, containsScore: 15);
        score += CompareNormalizedText(left.Description, right.Description, exactScore: 10, containsScore: 5);

        if (customerNameScore == 0 && titleScore == 0)
        {
            return 0;
        }

        return score;
    }

    private static int CompareNormalizedText(string? left, string? right, int exactScore, int containsScore)
    {
        var normalizedLeft = NormalizeDuplicateText(left);
        var normalizedRight = NormalizeDuplicateText(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        if (IsDuplicatePlaceholder(normalizedLeft) || IsDuplicatePlaceholder(normalizedRight))
        {
            return 0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return exactScore;
        }

        if (normalizedLeft.Length >= 5 &&
            normalizedRight.Length >= 5 &&
            (normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal) ||
             normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal)))
        {
            return containsScore;
        }

        return 0;
    }

    private static bool HasMeaningfulDuplicateInput(TaskItem task)
    {
        return IsMeaningfulDuplicateValue(task.CustomerName) ||
               IsMeaningfulDuplicateValue(task.Title);
    }

    private static bool IsMeaningfulDuplicateValue(string? value)
    {
        var normalized = NormalizeDuplicateText(value);
        return normalized.Length > 0 && !IsDuplicatePlaceholder(normalized);
    }

    private static bool IsDuplicatePlaceholder(string normalizedValue)
    {
        return normalizedValue.Equals("neuer kunde", StringComparison.Ordinal) ||
               normalizedValue.Equals("neue aufgabe", StringComparison.Ordinal) ||
               normalizedValue.Equals("ohne kundenname", StringComparison.Ordinal) ||
               normalizedValue.Equals("ohne titel", StringComparison.Ordinal);
    }

    private static string NormalizeDuplicateText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private void AddTaskToCategory(TaskItem task, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return;
        }

        EnsureTaskCategoryState(task);

        task.CategoryIds = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!task.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryIds.Add(categoryId);
        }

        if (string.IsNullOrWhiteSpace(task.CategoryId))
        {
            task.CategoryId = categoryId;
        }

        EnsureTaskCategoryState(task);
    }

    private List<string> GetTaskCategoryNameList(TaskItem task)
    {
        var ids = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Concat(new[] { task.CategoryId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ids
            .Select(id => Categories.FirstOrDefault(category => string.Equals(category.Id, id, StringComparison.OrdinalIgnoreCase))?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private string GetTaskCategoryNames(TaskItem task)
    {
        var categoryIds = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Append(task.CategoryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var names = categoryIds
            .Select(id => Categories.FirstOrDefault(category => string.Equals(category.Id, id, StringComparison.OrdinalIgnoreCase))?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return names.Count == 0 ? "Keine Kategorie" : string.Join(", ", names);
    }

    private void RemovePendingNewTask(TaskItem task)
    {
        _tasksPendingDuplicateCheck.Remove(task.Id);
        _repository.DeleteTask(task.Id);
        AllTasks.Remove(task);
        ClearTaskSelectionAfterRemoval(task);
        UpdateCategoryCounts();
    }

    private bool IsUnsavedPlaceholderTask(TaskItem? task)
    {
        return task is not null &&
               _tasksPendingDuplicateCheck.Contains(task.Id) &&
               !HasMeaningfulDuplicateInput(task);
    }

    private void ClearTaskSelectionAfterRemoval(TaskItem removedTask)
    {
        if (_selectedTask?.Id != removedTask.Id)
        {
            return;
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
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private void RefreshTaskCategories()
    {
        TaskCategories.Clear();
        foreach (var category in Categories.Where(category => !IsSpecialCategory(category)))
        {
            TaskCategories.Add(category);
        }

        RefreshTaskCategorySelections();
    }

    private void RefreshTaskCategorySelections()
    {
        TaskCategorySelections.Clear();
        if (SelectedTask is null)
        {
            return;
        }

        EnsureTaskCategoryState(SelectedTask);
        foreach (var category in TaskCategories)
        {
            TaskCategorySelections.Add(new TaskCategorySelection(
                category,
                TaskBelongsToCategory(SelectedTask, category.Id)));
        }
    }

    private static bool EnsureTaskCategoryState(TaskItem task)
    {
        task.CategoryIds ??= new List<string>();
        task.CategoryIds = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(task.CategoryId) &&
            !task.CategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryIds.Insert(0, task.CategoryId);
        }

        if (task.CategoryIds.Count == 0)
        {
            task.CategoryId = string.Empty;
            return false;
        }

        if (string.IsNullOrWhiteSpace(task.CategoryId) ||
            !task.CategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryId = task.CategoryIds[0];
        }

        return !string.IsNullOrWhiteSpace(task.CategoryId);
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

    private static CategoryItem CreateDeskCategory()
    {
        return new CategoryItem
        {
            Id = DeskCategoryId,
            Name = DeskCategoryName,
            SortOrder = int.MinValue + 1,
            Color = "#F4F0DE",
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

    private static bool TaskBelongsToCategory(TaskItem task, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return false;
        }

        if (string.Equals(task.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return task.CategoryIds.Any(id => string.Equals(id, categoryId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSpecialCategory(CategoryItem category)
    {
        return category.Id == DeskCategoryId ||
               category.Id == SettingsCategoryId ||
               category.Name == OverviewCategoryName;
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

    private static string MoveImportedEditFileToDoneFolder(string exportPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(exportPath) || !File.Exists(exportPath))
            {
                return string.Empty;
            }

            var sourceDirectory = Path.GetDirectoryName(exportPath);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return string.Empty;
            }

            var doneDirectory = Path.Combine(sourceDirectory, "Erledigt");
            Directory.CreateDirectory(doneDirectory);
            var targetPath = CreateUniquePath(doneDirectory, Path.GetFileName(exportPath));
            File.Move(exportPath, targetPath);
            return "Bearbeitete Datei wurde nach Erledigt verschoben.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not move imported edit file '{exportPath}': {ex}");
            return "Bearbeitete OneDrive-Datei konnte nicht nach Erledigt verschoben werden.";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} B"
            : $"{value:0.#} {units[unitIndex]}";
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

    private void SubscribeDeskItem(DeskItem item)
    {
        item.PropertyChanged += DeskItem_OnPropertyChanged;
    }

    private void UnsubscribeDeskItem(DeskItem item)
    {
        item.PropertyChanged -= DeskItem_OnPropertyChanged;
    }

    private void DeleteDeskItem(DeskItem deskItem)
    {
        if (_draggedDeskItem == deskItem)
        {
            ClearDeskDragState();
        }

        UnsubscribeDeskItem(deskItem);
        _repository.DeleteDeskItem(deskItem.Id);
        if (deskItem.IsFileCard)
        {
            TryDeleteFile(deskItem.ThumbnailPath);
            TryDeleteFile(deskItem.FilePath);
        }
        DeskItems.Remove(deskItem);
        UpdateDeskSurfaceBounds();
    }

    private void ClearDeskDragState()
    {
        if (_draggedDeskContainer is not null)
        {
            _draggedDeskContainer.RenderTransform = null;

            if (_draggedDeskPointer is not null)
            {
                _draggedDeskPointer.Capture(null);
            }
        }

        _draggedDeskItem = null;
        _draggedDeskContainer = null;
        _draggedDeskPointer = null;
        _isDraggingDeskItem = false;
        _deskDragCurrentDelta = default;
    }

    private void ClearDeskPanState()
    {
        if (_deskPanPointer is not null)
        {
            _deskPanPointer.Capture(null);
        }

        _deskPanPointer = null;
    }

    private static Border? FindDeskNoteContainer(Control control)
    {
        if (control is Border border &&
            (Equals(border.Tag, "DeskNoteRoot") || Equals(border.Tag, "DeskFileRoot")))
        {
            return border;
        }

        return control.GetVisualAncestors()
            .OfType<Border>()
            .FirstOrDefault(border => Equals(border.Tag, "DeskNoteRoot") || Equals(border.Tag, "DeskFileRoot"));
    }

    private static bool IsPointerInsideDeskNote(PointerEventArgs e)
    {
        if (e.Source is not Visual visual)
        {
            return false;
        }

        if (visual is Border border && (Equals(border.Tag, "DeskNoteRoot") || Equals(border.Tag, "DeskFileRoot")))
        {
            return true;
        }

        return visual.GetVisualAncestors()
            .OfType<Border>()
            .Any(border => Equals(border.Tag, "DeskNoteRoot") || Equals(border.Tag, "DeskFileRoot"));
    }


    private void LoadTechnicianOptions()
    {
        TechnicianOptions.Clear();

        foreach (var name in _appSettings.TechnicianNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            TechnicianOptions.Add(name);
        }
    }

    private void SaveTechnicianOptions()
    {
        _appSettings.TechnicianNames = TechnicianOptions
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settingsService.Save(_appSettings);
        LoadTechnicianOptions();
    }

    private void AddTechnician_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = NewTechnicianName.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!TechnicianOptions.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            TechnicianOptions.Add(name);
            SaveTechnicianOptions();
        }

        NewTechnicianName = string.Empty;
    }

    private void RemoveTechnician_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string technicianName })
        {
            TechnicianOptions.Remove(technicianName);
            SaveTechnicianOptions();
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetAppearanceMode(string? appearanceMode, bool persist)
    {
        var normalizedAppearanceMode = NormalizeAppearanceMode(appearanceMode);
        _appSettings.AppearanceMode = normalizedAppearanceMode;

        if (_appearanceMode == normalizedAppearanceMode)
        {
            if (persist)
            {
                _settingsService.Save(_appSettings);
            }

            return;
        }

        _appearanceMode = normalizedAppearanceMode;
        OnPropertyChanged(nameof(AppearanceMode));

        if (Application.Current is App app)
        {
            app.ApplyAppearanceMode(normalizedAppearanceMode);
        }

        if (persist)
        {
            _settingsService.Save(_appSettings);
        }
    }

    private static string NormalizeAppearanceMode(string? appearanceMode)
    {
        return string.Equals(appearanceMode?.Trim(), LightMode, StringComparison.OrdinalIgnoreCase)
            ? LightMode
            : DarkMode;
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

public sealed class TaskCategorySelection
{
    public TaskCategorySelection(CategoryItem category, bool isSelected)
    {
        Category = category;
        IsSelected = isSelected;
    }

    public CategoryItem Category { get; }
    public string Name => Category.Name;
    public bool IsSelected { get; set; }
}

public enum DuplicateTaskChoice
{
    Cancel,
    AddToCategory,
    MoveToCategory,
    CreateAnyway
}

public sealed record SimilarTaskMatch(TaskItem Task, int Score);

public sealed class DuplicateTaskDialog : Window
{
    public DuplicateTaskDialog(TaskItem existingTask, string existingCategories, string targetCategory, int score)
    {
        Title = "Ähnlicher Auftrag gefunden";
        Width = 540;
        MinWidth = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        content.Children.Add(new TextBlock
        {
            Text = "Dieser Auftrag scheint bereits vorhanden zu sein.",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(CreateInfoText($"Bestehender Auftrag: {existingTask.CustomerName} – {existingTask.Title}"));
        content.Children.Add(CreateInfoText($"Bestehende Kategorie(n): {existingCategories}"));
        content.Children.Add(CreateInfoText($"Neue gewünschte Kategorie: {targetCategory}"));
        content.Children.Add(CreateInfoText($"Übereinstimmung: {score} Punkte"));

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        buttonPanel.Children.Add(CreateChoiceButton(
            "Zusätzlich zuordnen",
            DuplicateTaskChoice.AddToCategory));
        buttonPanel.Children.Add(CreateChoiceButton(
            "In neue Kategorie verschieben",
            DuplicateTaskChoice.MoveToCategory));
        buttonPanel.Children.Add(CreateChoiceButton(
            "Trotzdem neu erstellen",
            DuplicateTaskChoice.CreateAnyway));
        buttonPanel.Children.Add(CreateChoiceButton(
            "Abbrechen",
            DuplicateTaskChoice.Cancel));

        content.Children.Add(buttonPanel);
        Content = content;
    }

    private static TextBlock CreateInfoText(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private Button CreateChoiceButton(string text, DuplicateTaskChoice choice)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 8)
        };
        button.Click += (_, _) => Close(choice);
        return button;
    }
}
