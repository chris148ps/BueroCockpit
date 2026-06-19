using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
using Microsoft.Data.Sqlite;

namespace BueroCockpit;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private bool _dateTextFormattingActive;
    private const string DeskCategoryId = "__desk";
    private const string DeskCategoryName = "Schreibtisch";
    private const string OverviewCategoryName = "Übersicht";
    private const string TrashCategoryId = "__trash";
    private const string TrashCategoryName = "Papierkorb";
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
    private TaskUndoSnapshot? _taskUndoSnapshot;
    private bool _hasPendingTaskUndo;
    private CategoryItem? _selectedCategory;
    private TaskItem? _selectedTask;
    private CategoryItem? _selectedTaskCategory;
    private AttachmentItem? _selectedAttachment;
    private AttachmentEditSession? _selectedAttachmentEditSession;
    private DeskItem? _draggedDeskItem;
    private Control? _draggedDeskContainer;
    private IPointer? _draggedDeskPointer;
    private DeskItem? _resizedDeskItem;
    private IPointer? _resizedDeskPointer;
    private string _taskListCaption = "0 Aufgaben";
    private string _globalSearchCaption = string.Empty;
    private string _searchText = string.Empty;
    private string _dueDateText = string.Empty;
    private string _followUpDateText = string.Empty;
    private string _sentAtText = string.Empty;
    private string _dateInputMessage = string.Empty;
    private string _taskUndoMessage = string.Empty;
    private bool _isGlobalSearchEnabled;
    private string _categoryEditorName = string.Empty;
    private string _categoryMessage = string.Empty;
    private string _backupStatus = "Noch kein Backup erstellt.";
    private string _storageLocationStatus = "Speicherort nicht geändert.";
    private string _appInstanceLockStatus = "Datenordner-Zugriffsschutz noch nicht geprüft.";
    private string _deskStatus = string.Empty;
    private string _filePathCheckStatus = string.Empty;
    private string _lastBackupPath = string.Empty;
    private string _lastBackupTime = string.Empty;
    private string _updateStatus = "Noch kein Update-Kanal eingerichtet.";
    private string _updateFeedUrl = string.Empty;
    private string _appearanceMode = DarkMode;
    private bool _isUpdateAvailable;
    private bool _startupUpdateCheckCompleted;
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
    private Point _deskResizeStartPointerPosition;
    private Vector _deskResizeStartItemSize;
    private Vector _deskResizeCurrentDelta;
    private bool _isResizingDeskItem;
    private IPointer? _deskPanPointer;
    private Point _deskPanStartPointerPosition;
    private Vector _deskPanStartOffset;
    private bool _isLoadingDeskItems;
    private bool _isApplyingDeskDrag;
    private bool _isApplyingDeskResize;
    private bool _deskInitialViewApplied;
    private double _deskFitZoom = 1.0;
    private double _deskUserZoom = 1.0;
    private double _deskSurfaceWidth = 2400;
    private double _deskSurfaceHeight = 1600;
    private const double DeskBaseSurfaceWidth = 2400;
    private const double DeskBaseSurfaceHeight = 1600;
    private const double DeskNoteDefaultWidth = 300;
    private const double DeskNoteDefaultHeight = 210;
    private const double DeskFileDefaultWidth = 220;
    private const double DeskFileDefaultHeight = 180;
    private const double DeskPdfDefaultWidth = 200;
    private const double DeskPdfDefaultHeight = 300;
    private const double DeskImageDefaultWidth = 235;
    private const double DeskImageDefaultHeight = 275;
    private const double DeskTextFileDefaultWidth = 240;
    private const double DeskTextFileDefaultHeight = 210;
    private const double DeskItemMinNoteWidth = 180;
    private const double DeskItemMinNoteHeight = 120;
    private const double DeskItemMinFileWidth = 160;
    private const double DeskItemMinFileHeight = 180;
    private const double DeskItemMaxWidth = 1200;
    private const double DeskItemMaxHeight = 1600;
    private const double DeskMinZoom = 0.2;
    private const double DeskMaxZoom = 10.0;
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

    public string[] SortModeOptions { get; } = ["Manuell", "Name", "Termin", "Erstellt am", "Wiedervorlage", "Gesendet am", "Geändert am"];
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

    public string DeskZoomLabel => $"{Math.Min(Math.Round(_deskUserZoom * 100), 300):0} %";

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
            OnPropertyChanged(nameof(IsTrashSelected));
            OnPropertyChanged(nameof(IsNotTrashSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
            OnPropertyChanged(nameof(IsTaskAreaVisible));
            OnPropertyChanged(nameof(IsTrashEmpty));
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

            if (_selectedTask is not null)
            {
                if (!_hasPendingTaskUndo ||
                    _taskUndoSnapshot is null ||
                    string.Equals(_taskUndoSnapshot.Task.Id, _selectedTask.Id, StringComparison.OrdinalIgnoreCase))
                {
                    SetTaskUndoBaseline(_selectedTask);
                }
            }
            else if (!_hasPendingTaskUndo)
            {
                ClearTaskUndoState();
            }

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
    public bool HasVisibleTasks => VisibleTasks.Count > 0;
    public bool IsTrashEmpty => IsTrashSelected && !HasVisibleTasks;
    public bool HasTrashItems => AllTasks.Any(task => task.IsDeleted);
    public bool CanUndoTaskChange => _hasPendingTaskUndo && _taskUndoSnapshot is not null;
    public string UndoTaskChangeText => CanUndoTaskChange
        ? $"Rückgängig: {GetUndoTaskChangeActionText(_taskUndoSnapshot!)}"
        : "Rückgängig";
    public string UndoTaskChangeToolTip => CanUndoTaskChange
        ? $"Letzte Änderung zurücknehmen: {GetUndoTaskChangeActionText(_taskUndoSnapshot!)}"
        : "Letzte Änderung zurücknehmen";
    public string TaskUndoMessage
    {
        get => _taskUndoMessage;
        set
        {
            if (_taskUndoMessage != value)
            {
                _taskUndoMessage = value;
                OnPropertyChanged(nameof(TaskUndoMessage));
                OnPropertyChanged(nameof(HasTaskUndoMessage));
                OnPropertyChanged(nameof(IsTaskUndoPanelVisible));
            }
        }
    }
    public bool HasTaskUndoMessage => !string.IsNullOrWhiteSpace(TaskUndoMessage);
    public bool IsTaskUndoPanelVisible => CanUndoTaskChange || HasTaskUndoMessage;
    public bool IsOverviewSelected => SelectedCategory?.Name == OverviewCategoryName;
    public bool IsDeskSelected => SelectedCategory?.Id == DeskCategoryId;
    public bool IsTrashSelected => SelectedCategory?.Id == TrashCategoryId;
    public bool IsNotTrashSelected => !IsTrashSelected;
    public bool IsSettingsSelected => SelectedCategory?.Id == SettingsCategoryId;
    public bool IsTaskAreaVisible => !IsOverviewSelected && !IsDeskSelected && !IsSettingsSelected;
    public string DashboardDateText => DateTime.Today.ToString("dddd, dd. MMMM yyyy");
    public string AppDataDirectory => ResolveDisplayDirectory(AppPaths.AppDataDirectory);
    public string DefaultAppDataDirectory => ResolveDisplayDirectory(AppPaths.DefaultAppDataDirectory);
    public string DatabasePath => ResolveDisplayPath(AppPaths.DatabasePath);
    public string TasksDirectory => ResolveDisplayPath(AppPaths.TasksDirectory);
    public string BackupDirectory => ResolveDisplayPath(AppPaths.BackupDirectory);
    public string LockPath => ResolveDisplayPath(AppPaths.LockPath);
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
        : OperatingSystem.IsWindows()
            ? "Diese laufende Instanz: Entwicklungsstart. Auto-Update ist nur in der installierten Windows-Version aktiv."
            : "Diese laufende Instanz: Auto-Update wird auf dieser Plattform nicht unterstützt.";
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
    public string PreviewImagePath => SelectedAttachment is null
        ? string.Empty
        : ResolveAttachmentPath(SelectedAttachment.ThumbnailPath, SelectedAttachment.TaskId, SelectedAttachment.FileName);
    public bool HasPreviewImage => SelectedAttachment is not null &&
                                   File.Exists(ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName)) &&
                                   !string.IsNullOrWhiteSpace(PreviewImagePath) &&
                                   File.Exists(PreviewImagePath);
    public bool HasPreviewPlaceholder => HasSelectedAttachment && !HasPreviewImage;
    public string AttachmentPreviewTitle
    {
        get
        {
            if (SelectedAttachment is null)
            {
                return string.Empty;
            }

            var storedPath = ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName);
            if (File.Exists(storedPath))
            {
                return SelectedAttachment.FileName;
            }

            LogMissingAttachment(SelectedAttachment, storedPath);
            return "Datei nicht gefunden";
        }
    }
    public string AttachmentPreviewInfo
    {
        get
        {
            if (SelectedAttachment is null)
            {
                return string.Empty;
            }

            var storedPath = ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName);
            if (!File.Exists(storedPath))
            {
                return "Datei nicht gefunden.";
            }

            var extension = SelectedAttachment.FileType.TrimStart('.').ToLowerInvariant();
            var sizeText = FormatFileSize(new FileInfo(storedPath).Length);
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
    public string DeskStatus
    {
        get => _deskStatus;
        set
        {
            if (_deskStatus != value)
            {
                _deskStatus = value;
                OnPropertyChanged(nameof(DeskStatus));
                OnPropertyChanged(nameof(HasDeskStatus));
            }
        }
    }

    public bool HasDeskStatus => !string.IsNullOrWhiteSpace(DeskStatus);

    public string FilePathCheckStatus
    {
        get => _filePathCheckStatus;
        set
        {
            if (_filePathCheckStatus != value)
            {
                _filePathCheckStatus = value;
                OnPropertyChanged(nameof(FilePathCheckStatus));
                OnPropertyChanged(nameof(HasFilePathCheckStatus));
            }
        }
    }

    public bool HasFilePathCheckStatus => !string.IsNullOrWhiteSpace(FilePathCheckStatus);

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

        Closed += (_, _) =>
        {
            _appInstanceLockService.Release();
        };
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
        CleanupNavigationCategories();
        SelectStartupTaskCategory();
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

    private static void ApplyTaskCardVisual(Border border, bool isHovered)
    {
        if (border.DataContext is TaskItem task && task.IsSelected)
        {
            border.BorderBrush = new SolidColorBrush(Color.Parse("#000000"));
            border.BorderThickness = new Thickness(1);
            return;
        }

        border.BorderBrush = new SolidColorBrush(Color.Parse(isHovered ? "#000000" : "#00000000"));
        border.BorderThickness = new Thickness(1);
    }

    private void TaskCard_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            ApplyTaskCardVisual(border, true);
        }
    }

    private void TaskCard_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            ApplyTaskCardVisual(border, false);
        }
    }

    private void CleanupNavigationCategories()
    {
        var categoriesToRemove = Categories
            .Where(category =>
                string.Equals(category.Name, OverviewCategoryName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category.Name, "Übersicht", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category.Name, "Dashboard", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(category.Name, "Schreibtisch", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(category.Id, DeskCategoryId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var category in categoriesToRemove)
        {
            Categories.Remove(category);
        }

        var duplicateDeskCategories = Categories
            .Where(category => string.Equals(category.Id, DeskCategoryId, StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .ToList();

        foreach (var category in duplicateDeskCategories)
        {
            Categories.Remove(category);
        }
    }

    private void SelectStartupTaskCategory()
    {
        var startupCategory = Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufträge", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category => !IsSpecialCategory(category));

        if (startupCategory is null)
        {
            return;
        }

        SelectedCategory = startupCategory;

        if (CategoryList is not null)
        {
            CategoryList.SelectedItem = startupCategory;
        }

        ApplySelectedCategoryContent();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private async void MainWindow_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (!_startupUpdateCheckCompleted)
        {
            _startupUpdateCheckCompleted = true;
            var shouldStopStartup = await RunStartupUpdateCheckAsync();
            if (shouldStopStartup)
            {
                return;
            }
        }

        if (_deskInitialViewApplied)
        {
            return;
        }

        if (IsDeskSelected)
        {
            Dispatcher.UIThread.Post(FitDeskToViewport, DispatcherPriority.Loaded);
        }
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

        ClearDeskResizeState();
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

    private void DeskResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeskItem deskItem)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ClearDeskDragState();
        _resizedDeskItem = deskItem;
        _resizedDeskPointer = e.Pointer;
        _deskResizeStartPointerPosition = GetDeskLogicalPointerPosition(e);
        _deskResizeStartItemSize = new Vector(deskItem.Width, deskItem.Height);
        _deskResizeCurrentDelta = default;
        _isResizingDeskItem = false;
        _resizedDeskPointer.Capture(control);
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

    private void DeskResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizedDeskItem is null ||
            _resizedDeskPointer is null ||
            e.Pointer != _resizedDeskPointer)
        {
            return;
        }

        var currentPosition = GetDeskLogicalPointerPosition(e);
        var deltaX = currentPosition.X - _deskResizeStartPointerPosition.X;
        var deltaY = currentPosition.Y - _deskResizeStartPointerPosition.Y;

        if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
        {
            _isResizingDeskItem = true;
        }

        _deskResizeCurrentDelta = new Vector(deltaX, deltaY);
        ApplyDeskResizeDelta(_resizedDeskItem, _deskResizeStartItemSize, _deskResizeCurrentDelta);
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

    private void DeskResizeGrip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizedDeskItem is null || _resizedDeskPointer is null || e.Pointer != _resizedDeskPointer)
        {
            return;
        }

        CommitDeskResize();
        ClearDeskResizeState();
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

    private void DeskResizeGrip_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CommitDeskResize();
        ClearDeskResizeState();
    }

    private void DeskResizeGrip_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        e.Handled = true;
    }

    private void DeskFileCard_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeskItem deskItem)
        {
            return;
        }

        if (!deskItem.IsFileCard)
        {
            return;
        }

        var filePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            DeskStatus = $"Datei nicht gefunden: {filePath}";
            return;
        }

        e.Handled = true;
        TryOpenDeskFileExternal(deskItem, filePath);
    }

    private void OpenDeskFileExternal_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        TryOpenDeskFileExternal(deskItem);
    }

    private async void ReassignDeskFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            DeskStatus = "Dateiauswahl ist nicht verfügbar.";
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Schreibtisch-Datei neu zuordnen",
            AllowMultiple = false
        });

        var newPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(newPath))
        {
            return;
        }

        if (!File.Exists(newPath))
        {
            DeskStatus = $"Datei nicht gefunden: {newPath}";
            return;
        }

        var storedPath = ResolveDeskFileStoragePath(newPath, deskItem.Id);
        deskItem.FilePath = storedPath;
        deskItem.ReferencePath = storedPath;
        deskItem.FileName = Path.GetFileName(newPath);
        deskItem.ThumbnailPath = string.Empty;
        deskItem.ContentHash = string.Empty;
        deskItem.UpdatedAt = DateTime.Now;

        var resolvedLinkedTaskId = TryResolveDeskItemLinkedTaskIdFromPath(deskItem);
        if (!string.IsNullOrWhiteSpace(resolvedLinkedTaskId))
        {
            deskItem.LinkedTaskId = resolvedLinkedTaskId;
        }

        RefreshDeskFileCard(deskItem);
        _repository.SaveDeskItem(deskItem);
        DeskStatus = $"Datei neu zugeordnet: {newPath}";
    }

    private void TryOpenDeskFileExternal(DeskItem deskItem, string? resolvedPath = null)
    {
        var filePath = !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath)
            ? resolvedPath
            : AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            DeskStatus = $"Datei nicht gefunden: {filePath}";
            return;
        }

        if (!TryOpenExternalFile(filePath, out var errorMessage))
        {
            DeskStatus = errorMessage ?? $"Datei konnte nicht geöffnet werden: {filePath}";
        }
    }

    private void OpenDeskItemDetailView_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        var hadDeskContentHash = !string.IsNullOrWhiteSpace(deskItem.ContentHash);
        var linkedTaskId = TryResolveDeskItemLinkedTaskIdFromPath(deskItem);
        if (string.IsNullOrWhiteSpace(linkedTaskId) && deskItem.IsFileCard)
        {
            var deskContentHash = EnsureDeskItemContentHash(deskItem);
            if (!string.IsNullOrWhiteSpace(deskContentHash))
            {
                var attachmentHashIndex = BuildAttachmentHashIndex();
                linkedTaskId = TryResolveTaskIdByAttachmentHash(deskContentHash, attachmentHashIndex);
            }
        }

        if (string.IsNullOrWhiteSpace(linkedTaskId))
        {
            if (!hadDeskContentHash && !string.IsNullOrWhiteSpace(deskItem.ContentHash))
            {
                _repository.SaveDeskItem(deskItem);
            }

            return;
        }

        var task = AllTasks.FirstOrDefault(item =>
            string.Equals(item.Id, linkedTaskId, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return;
        }

        if (!string.Equals(deskItem.LinkedTaskId, linkedTaskId, StringComparison.OrdinalIgnoreCase))
        {
            deskItem.LinkedTaskId = linkedTaskId;
        }

        NavigateToTask(task, fromGlobalSearch: false);
    }

    private void ToggleDeskItemImportant_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        deskItem.IsImportant = !deskItem.IsImportant;
    }

    private void BeginDeskItemRename_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: DeskItem deskItem })
        {
            return;
        }

        deskItem.IsRenaming = true;
        Dispatcher.UIThread.Post(() => FocusDeskItemNameEditor(deskItem), DispatcherPriority.Loaded);
    }

    private void DeskItemNameEditor_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: DeskItem deskItem })
        {
            return;
        }

        deskItem.IsRenaming = false;
    }

    private void DeskItemNameEditor_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: DeskItem deskItem })
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Escape)
        {
            deskItem.IsRenaming = false;
            e.Handled = true;
        }
    }

    private void DeskItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingDeskItems || _isApplyingDeskDrag || _isApplyingDeskResize || sender is not DeskItem deskItem)
        {
            return;
        }

        if (e.PropertyName is null ||
            e.PropertyName == nameof(DeskItem.Text) ||
            e.PropertyName == nameof(DeskItem.DisplayName) ||
            e.PropertyName == nameof(DeskItem.FilePath) ||
            e.PropertyName == nameof(DeskItem.FileName) ||
            e.PropertyName == nameof(DeskItem.ReferencePath) ||
            e.PropertyName == nameof(DeskItem.LinkedTaskId) ||
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
            _draggedDeskItem.UpdatedAt = DateTime.Now;
        }
        finally
        {
            _isApplyingDeskDrag = false;
        }

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
        Categories.Add(CreateTrashCategory());
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
            IEnumerable<TaskItem> tasks;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                tasks = selected is null
                    ? AllTasks.Where(t => !t.IsDeleted)
                    : IsTrashSelected
                        ? SortTasksForCategory(AllTasks.Where(t => t.IsDeleted), selected.SortMode)
                        : SortTasksForCategory(AllTasks.Where(t => !t.IsDeleted && TaskBelongsToCategory(t, selected.Id)), selected.SortMode);
            }
            else
            {
                var query = SearchText.Trim();
                tasks = SortTasksForCategory(GetSearchMatches(query), selected?.SortMode);
            }

            foreach (var task in tasks)
            {
                UpdateTaskCategoryPresentation(task);
                VisibleTasks.Add(task);
            }

            TaskListCaption = string.IsNullOrWhiteSpace(SearchText)
                ? IsTrashSelected
                    ? (VisibleTasks.Count == 1 ? "1 gelöschte Aufgabe" : $"{VisibleTasks.Count} gelöschte Aufgaben")
                    : (VisibleTasks.Count == 1 ? "1 Aufgabe" : $"{VisibleTasks.Count} Aufgaben")
                : (VisibleTasks.Count == 1 ? "1 Treffer" : $"{VisibleTasks.Count} Treffer");
            if (IsTrashSelected && VisibleTasks.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
            {
                TaskListCaption = "Papierkorb ist leer";
            }
            OnPropertyChanged(nameof(HasVisibleTasks));
            OnPropertyChanged(nameof(IsTrashEmpty));

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
        foreach (var task in SortTasksForCategory(GetSearchMatches(query), SelectedCategory?.SortMode).Take(50))
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
        var categoryNames = GetTaskCategoryNameList(task);
        var categoryName = categoryNames.FirstOrDefault() ?? string.Empty;
        var preferredCategoryId = GetSearchPreferredCategoryId(task, query);
        var matchInfo = GetSearchMatchInfo(task, categoryNames, query);
        return new TaskSearchResult(
            task,
            categoryName,
            categoryNames,
            preferredCategoryId,
            task.CustomerName,
            task.Title,
            matchInfo,
            task.Technician,
            task.CustomerAddress,
            task.DueDate,
            task.SentAt);
    }

    private string GetSearchMatchInfo(TaskItem task, IReadOnlyList<string> categoryNames, string query)
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

        if (categoryNames.Any(categoryName => Contains(categoryName, query)))
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

    private IEnumerable<TaskItem> GetSearchMatches(string query)
    {
        return AllTasks
            .Where(task => IsTrashSelected ? task.IsDeleted : !task.IsDeleted)
            .Where(task => TaskMatchesSearch(task, query))
            .GroupBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
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

    private void TaskDetailEditor_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
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

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
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
        TaskList.SelectedItem = null;
        SelectedAttachment = null;
        SelectedTaskCategory = null;
        ClearTaskUndoState();
        Materials.Clear();
        Attachments.Clear();
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(HasSelectedTask));
        OnPropertyChanged(nameof(HasNoMaterials));
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private void SetTaskUndoBaseline(TaskItem task)
    {
        _taskUndoSnapshot = CreateTaskUndoSnapshot(task);
        _hasPendingTaskUndo = false;
        TaskUndoMessage = string.Empty;
        NotifyTaskUndoStateChanged();
    }

    private void CaptureTaskUndoState(TaskItem? task, bool preserveExistingSnapshot = false)
    {
        if (task is null)
        {
            return;
        }

        if (preserveExistingSnapshot &&
            _hasPendingTaskUndo &&
            _taskUndoSnapshot is not null &&
            string.Equals(_taskUndoSnapshot.Task.Id, task.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _taskUndoSnapshot = CreateTaskUndoSnapshot(task);
        _hasPendingTaskUndo = true;
        TaskUndoMessage = string.Empty;
        NotifyTaskUndoStateChanged();
    }

    private void ClearTaskUndoState()
    {
        _taskUndoSnapshot = null;
        _hasPendingTaskUndo = false;
        TaskUndoMessage = string.Empty;
        NotifyTaskUndoStateChanged();
    }

    private void UndoTaskChange_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!CanUndoTaskChange || _taskUndoSnapshot is null)
        {
            TaskUndoMessage = "Rückgängig ist derzeit nicht verfügbar.";
            return;
        }

        try
        {
            if (!RestoreTaskSnapshot(_taskUndoSnapshot))
            {
                ClearTaskUndoState();
                TaskUndoMessage = "Rückgängig konnte nicht ausgeführt werden.";
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UndoTaskChange failed: {ex}");
            ClearTaskUndoState();
            TaskUndoMessage = "Rückgängig konnte nicht ausgeführt werden.";
            return;
        }

        ClearTaskUndoState();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private bool RestoreTaskSnapshot(TaskUndoSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Task.Id))
        {
            return false;
        }

        var task = AllTasks.FirstOrDefault(item => string.Equals(item.Id, snapshot.Task.Id, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            task = CloneTaskItem(snapshot.Task);
            AllTasks.Insert(0, task);
        }
        else
        {
            ApplyTaskState(task, snapshot.Task);
        }

        _repository.SaveTask(task);
        SyncTaskMaterials(task.Id, snapshot.Materials);
        SyncTaskAttachments(task.Id, snapshot.Attachments);
        _tasksPendingDuplicateCheck.Remove(task.Id);

        var targetCategory = task.IsDeleted
            ? Categories.FirstOrDefault(category => category.Id == TrashCategoryId)
            : GetTaskNavigationCategory(task) ?? GetDefaultStartupCategory() ?? Categories.FirstOrDefault(category => !IsSpecialCategory(category));

        if (targetCategory is not null)
        {
            SelectCategoryAndTask(targetCategory, task);
            LoadTaskDetails();
        }
        else
        {
            RefreshVisibleTasks();
        }

        return true;
    }

    private void NotifyTaskUndoStateChanged()
    {
        OnPropertyChanged(nameof(CanUndoTaskChange));
        OnPropertyChanged(nameof(UndoTaskChangeText));
        OnPropertyChanged(nameof(UndoTaskChangeToolTip));
        OnPropertyChanged(nameof(IsTaskUndoPanelVisible));
    }

    private string GetUndoTaskChangeActionText(TaskUndoSnapshot snapshot)
    {
        var currentTask = AllTasks.FirstOrDefault(item => string.Equals(item.Id, snapshot.Task.Id, StringComparison.OrdinalIgnoreCase));
        if (currentTask is null)
        {
            return "Änderung an Auftrag";
        }

        if (snapshot.Task.IsDeleted != currentTask.IsDeleted)
        {
            return snapshot.Task.IsDeleted ? "Wiederherstellung" : "Löschung";
        }

        return "Änderung an Auftrag";
    }

    private void SyncTaskMaterials(string taskId, IReadOnlyList<MaterialItem> snapshotMaterials)
    {
        var currentMaterialIds = _repository.GetMaterials(taskId)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var snapshotMaterialIds = snapshotMaterials
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var materialId in currentMaterialIds.Where(id => !snapshotMaterialIds.Contains(id)).ToList())
        {
            _repository.DeleteMaterial(materialId);
        }

        foreach (var material in snapshotMaterials)
        {
            var copy = CloneMaterialItem(material);
            copy.TaskId = taskId;
            _repository.SaveMaterial(copy);
        }

        if (SelectedTask?.Id == taskId)
        {
            Materials.Clear();
            foreach (var material in snapshotMaterials)
            {
                var copy = CloneMaterialItem(material);
                copy.TaskId = taskId;
                Materials.Add(copy);
            }

            OnPropertyChanged(nameof(HasNoMaterials));
        }
    }

    private void SyncTaskAttachments(string taskId, IReadOnlyList<AttachmentItem> snapshotAttachments)
    {
        var currentAttachments = _repository.GetAttachments(taskId);
        var snapshotAttachmentIds = snapshotAttachments
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in currentAttachments.Where(item => !snapshotAttachmentIds.Contains(item.Id)).ToList())
        {
            _repository.DeleteAttachment(attachment.Id);
        }

        foreach (var attachment in snapshotAttachments)
        {
            var copy = CloneAttachmentItem(attachment);
            copy.TaskId = taskId;
            _repository.SaveAttachment(copy);
        }

        if (SelectedTask?.Id == taskId)
        {
            Attachments.Clear();
            foreach (var attachment in snapshotAttachments)
            {
                var copy = CloneAttachmentItem(attachment);
                copy.TaskId = taskId;
                Attachments.Insert(0, copy);
            }

            if (SelectedAttachment is not null &&
                !snapshotAttachmentIds.Contains(SelectedAttachment.Id))
            {
                SelectedAttachment = null;
            }
        }
    }

    private TaskUndoSnapshot CreateTaskUndoSnapshot(TaskItem task)
    {
        var materials = SelectedTask?.Id == task.Id
            ? Materials.Select(CloneMaterialItem).ToList()
            : _repository.GetMaterials(task.Id).Select(CloneMaterialItem).ToList();
        var attachments = SelectedTask?.Id == task.Id
            ? Attachments.Select(CloneAttachmentItem).ToList()
            : _repository.GetAttachments(task.Id).Select(CloneAttachmentItem).ToList();

        return new TaskUndoSnapshot(
            CloneTaskItem(task),
            materials,
            attachments);
    }

    private static void ApplyTaskState(TaskItem target, TaskItem source)
    {
        target.Title = source.Title;
        target.CustomerName = source.CustomerName;
        target.CustomerAddress = source.CustomerAddress;
        target.Description = source.Description;
        target.CategoryId = source.CategoryId;
        target.CategoryIds = source.CategoryIds.ToList();
        target.Status = source.Status;
        target.Priority = source.Priority;
        target.DueDate = source.DueDate;
        target.FollowUpDate = source.FollowUpDate;
        target.SentAt = source.SentAt;
        target.AssignedTo = source.AssignedTo;
        target.Technician = source.Technician;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
        target.CompletedAt = source.CompletedAt;
        target.IsDeleted = source.IsDeleted;
        target.DeletedAt = source.DeletedAt;
        target.SortPosition = source.SortPosition;
        target.CategoryHint = source.CategoryHint;
        target.CategoryNameChips = source.CategoryNameChips.ToList();
        target.ShowCategoryHint = source.ShowCategoryHint;
    }

    private static TaskItem CloneTaskItem(TaskItem source)
    {
        return source.Clone();
    }

    private static MaterialItem CloneMaterialItem(MaterialItem source)
    {
        return new MaterialItem
        {
            Id = source.Id,
            TaskId = source.TaskId,
            Quantity = source.Quantity,
            Unit = source.Unit,
            Name = source.Name,
            Status = source.Status,
            Supplier = source.Supplier,
            OrderedAt = source.OrderedAt,
            Note = source.Note
        };
    }

    private static AttachmentItem CloneAttachmentItem(AttachmentItem source)
    {
        return new AttachmentItem
        {
            Id = source.Id,
            TaskId = source.TaskId,
            FileName = source.FileName,
            StoredPath = source.StoredPath,
            ThumbnailPath = source.ThumbnailPath,
            FileType = source.FileType,
            ContentHash = source.ContentHash,
            AddedAt = source.AddedAt,
            IsSelectedForPrint = source.IsSelectedForPrint
        };
    }

    private void RefreshDashboard()
    {
        DashboardSections.Clear();
        var today = DateTime.Today;
        var activeTasks = AllTasks.Where(task => !task.IsDeleted && !IsDoneOrArchived(task)).ToList();

        DashboardSections.Add(CreateDashboardSection(
            "Heute fällig",
            activeTasks.Where(task => task.DueDate?.Date == today)));

        DashboardSections.Add(CreateDashboardSection(
            "Überfällig",
            activeTasks.Where(task => task.DueDate?.Date < today)));

        DashboardSections.Add(CreateDashboardSection(
            "Wiedervorlage heute",
            AllTasks.Where(task => !task.IsDeleted && task.FollowUpDate?.Date == today)));

        DashboardSections.Add(CreateDashboardSection(
            "Material offen",
            activeTasks.Where(HasOpenMaterial)));

        DashboardSections.Add(CreateDashboardSection(
            "Angebote / offene Büroaufgaben",
            activeTasks.Where(IsOfficeTask)));
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
            if (category.Name == OverviewCategoryName)
            {
                category.TaskCount = AllTasks.Count(task => !task.IsDeleted);
            }
            else if (category.Id == TrashCategoryId)
            {
                category.TaskCount = AllTasks.Count(task => task.IsDeleted);
            }
            else if (category.Id == DeskCategoryId || category.Id == SettingsCategoryId)
            {
                category.TaskCount = 0;
            }
            else
            {
                category.TaskCount = AllTasks.Count(task => !task.IsDeleted && TaskBelongsToCategory(task, category.Id));
            }
        }

        OnPropertyChanged(nameof(HasTrashItems));
        OnPropertyChanged(nameof(IsTrashEmpty));
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
            Dictionary<string, string?>? attachmentHashIndex = null;
            foreach (var item in _repository.GetDeskItems())
            {
                var needsSave = false;
                if (TryMigrateDeskItemFile(item, out _, out _))
                {
                    needsSave = true;
                }

                var normalizedSize = NormalizeDeskItemSize(item);
                if (Math.Abs(item.Width - normalizedSize.Width) > 0.5 ||
                    Math.Abs(item.Height - normalizedSize.Height) > 0.5)
                {
                    item.Width = normalizedSize.Width;
                    item.Height = normalizedSize.Height;
                    needsSave = true;
                }

                var resolvedLinkedTaskId = TryResolveDeskItemLinkedTaskIdFromPath(item);
                if (string.IsNullOrWhiteSpace(resolvedLinkedTaskId) && item.IsFileCard)
                {
                    var contentHashBefore = item.ContentHash;
                    var deskContentHash = EnsureDeskItemContentHash(item);
                    if (!string.Equals(contentHashBefore, item.ContentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        needsSave = true;
                    }

                    if (!string.IsNullOrWhiteSpace(deskContentHash))
                    {
                        attachmentHashIndex ??= BuildAttachmentHashIndex();
                        resolvedLinkedTaskId = TryResolveTaskIdByAttachmentHash(deskContentHash, attachmentHashIndex);
                    }
                }

                if (!string.IsNullOrWhiteSpace(resolvedLinkedTaskId) &&
                    !string.Equals(item.LinkedTaskId, resolvedLinkedTaskId, StringComparison.OrdinalIgnoreCase))
                {
                    item.LinkedTaskId = resolvedLinkedTaskId;
                    needsSave = true;
                }

                if (needsSave)
                {
                    _repository.SaveDeskItem(item);
                }

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
        var filePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
        if (!deskItem.IsFileCard || !File.Exists(filePath))
        {
            return;
        }

        var previewText = BuildDeskFilePreviewText(filePath);
        if (!string.Equals(previewText ?? string.Empty, deskItem.Text ?? string.Empty, StringComparison.Ordinal))
        {
            deskItem.Text = previewText ?? string.Empty;
        }

        var previewPath = EnsureDeskFileThumbnail(deskItem, filePath);
        if (!string.IsNullOrWhiteSpace(previewPath) &&
            !string.Equals(previewPath, deskItem.ThumbnailPath, StringComparison.OrdinalIgnoreCase))
        {
            deskItem.ThumbnailPath = previewPath;
            _repository.SaveDeskItem(deskItem);
        }
    }

    private string? EnsureDeskFileThumbnail(DeskItem deskItem, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!DeskImageExtensions.Contains(extension) &&
            !string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var previewAttachment = new AttachmentItem
        {
            Id = deskItem.Id,
            StoredPath = filePath,
            ThumbnailPath = AppPaths.ResolveDeskItemPath(deskItem.ThumbnailPath),
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

        return (DeskFileDefaultWidth, DeskFileDefaultHeight);
    }

    private static (double Width, double Height) GetDeskItemDefaultSize(DeskItem deskItem)
    {
        if (deskItem.IsNoteCard)
        {
            return (DeskNoteDefaultWidth, DeskNoteDefaultHeight);
        }

        if (deskItem.IsPdfCard)
        {
            return (DeskPdfDefaultWidth, DeskPdfDefaultHeight);
        }

        if (deskItem.IsImageCard)
        {
            return (DeskImageDefaultWidth, DeskImageDefaultHeight);
        }

        var fileName = !string.IsNullOrWhiteSpace(deskItem.FileName)
            ? deskItem.FileName
            : deskItem.FilePath;
        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            return (DeskTextFileDefaultWidth, DeskTextFileDefaultHeight);
        }

        return (DeskFileDefaultWidth, DeskFileDefaultHeight);
    }

    private static (double Width, double Height) NormalizeDeskItemSize(DeskItem deskItem)
    {
        var (defaultWidth, defaultHeight) = GetDeskItemDefaultSize(deskItem);
        var width = deskItem.Width;
        var height = deskItem.Height;

        if (width <= 0 || height <= 0)
        {
            width = defaultWidth;
            height = defaultHeight;
        }

        return ClampDeskItemSize(deskItem, width, height);
    }

    private static (double Width, double Height) ClampDeskItemSize(DeskItem deskItem, double width, double height)
    {
        var minWidth = deskItem.IsNoteCard ? DeskItemMinNoteWidth : DeskItemMinFileWidth;
        var minHeight = deskItem.IsNoteCard ? DeskItemMinNoteHeight : DeskItemMinFileHeight;

        return (
            Math.Clamp(width, minWidth, DeskItemMaxWidth),
            Math.Clamp(height, minHeight, DeskItemMaxHeight));
    }

    private void ApplyDeskResizeDelta(DeskItem deskItem, Vector startSize, Vector delta)
    {
        if (!_isResizingDeskItem)
        {
            return;
        }

        var (width, height) = deskItem.IsPdfCard
            ? GetDeskPdfResizeSize(startSize, delta)
            : (startSize.X + delta.X, startSize.Y + delta.Y);

        (width, height) = ClampDeskItemSize(deskItem, width, height);

        _isApplyingDeskResize = true;
        try
        {
            deskItem.Width = width;
            deskItem.Height = height;
        }
        finally
        {
            _isApplyingDeskResize = false;
        }

        UpdateDeskSurfaceBounds();
    }

    private static (double Width, double Height) GetDeskPdfResizeSize(Vector startSize, Vector delta)
    {
        var widthScale = startSize.X <= 0 ? 1.0 : (startSize.X + delta.X) / startSize.X;
        var heightScale = startSize.Y <= 0 ? 1.0 : (startSize.Y + delta.Y) / startSize.Y;
        var scale = Math.Max(widthScale, heightScale);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            scale = 1.0;
        }

        return (startSize.X * scale, startSize.Y * scale);
    }

    private void CommitDeskResize()
    {
        if (_resizedDeskItem is null)
        {
            return;
        }

        if (!_isResizingDeskItem &&
            Math.Abs(_deskResizeCurrentDelta.X) <= 0.5 &&
            Math.Abs(_deskResizeCurrentDelta.Y) <= 0.5)
        {
            return;
        }

        var (width, height) = ClampDeskItemSize(
            _resizedDeskItem,
            _resizedDeskItem.Width,
            _resizedDeskItem.Height);

        _isApplyingDeskResize = true;
        try
        {
            _resizedDeskItem.Width = width;
            _resizedDeskItem.Height = height;
            _resizedDeskItem.UpdatedAt = DateTime.Now;
        }
        finally
        {
            _isApplyingDeskResize = false;
        }

        _repository.SaveDeskItem(_resizedDeskItem);
        UpdateDeskSurfaceBounds();
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
        var safeFileName = CreateStoredFileName(originalFileName);
        var deskFilePath = AppPaths.GetDeskFilePath(deskItemId, safeFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(deskFilePath)!);
        return deskFilePath;
    }

    private string ResolveAttachmentStoragePath(string sourcePath, string taskId)
    {
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (AppPaths.IsPathInsideAppDataDirectory(normalizedSourcePath))
        {
            return normalizedSourcePath;
        }

        var attachmentDirectory = AppPaths.GetAttachmentDirectory(taskId);
        Directory.CreateDirectory(attachmentDirectory);

        var targetPath = Path.Combine(attachmentDirectory, CreateStoredFileName(Path.GetFileName(normalizedSourcePath)));
        File.Copy(normalizedSourcePath, targetPath, overwrite: false);
        return targetPath;
    }

    private string ResolveDeskFileStoragePath(string sourcePath, string deskItemId)
    {
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (AppPaths.IsPathInsideAppDataDirectory(normalizedSourcePath))
        {
            return normalizedSourcePath;
        }

        var targetPath = CreateDeskFileStoredPath(Path.GetFileName(normalizedSourcePath), deskItemId);
        File.Copy(normalizedSourcePath, targetPath, overwrite: false);
        return targetPath;
    }

    private static string ResolveDeskItemSourcePath(DeskItem deskItem)
    {
        var referencePath = AppPaths.ResolveDeskItemPath(deskItem.ReferencePath, deskItem.FileName);
        if (!string.IsNullOrWhiteSpace(referencePath) && File.Exists(referencePath))
        {
            return referencePath;
        }

        var filePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return filePath;
        }

        return referencePath;
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
            UpdateTaskCategoryPresentation(SelectedTask);
            RefreshTaskCategorySelections();

            foreach (var item in _repository.GetMaterials(SelectedTask.Id))
            {
                Materials.Add(item);
            }
            OnPropertyChanged(nameof(HasNoMaterials));

            foreach (var item in _repository.GetAttachments(SelectedTask.Id))
            {
                if (TryMigrateAttachmentFile(item, out _, out _))
                {
                    _repository.SaveAttachment(item);
                }

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

        var category = !IsOverviewSelected && !IsDeskSelected && !IsSettingsSelected && !IsTrashSelected
            ? SelectedCategory
            : null;
        category ??= Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben");
        category ??= Categories.FirstOrDefault(c => !IsSpecialCategory(c));
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

    private bool AddDeskFileCardFromPath(string? sourcePath, Point dropPosition, int index, string? linkedTaskId = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !File.Exists(sourcePath))
        {
            return false;
        }

        var now = DateTime.Now;
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        var fileName = Path.GetFileName(normalizedSourcePath);
        var cardSize = GetDeskFileCardSize(normalizedSourcePath);
        var deskItemId = Guid.NewGuid().ToString("N");
        var resolvedLinkedTaskId = !string.IsNullOrWhiteSpace(linkedTaskId)
            ? linkedTaskId
            : TryGetLinkedTaskIdFromPath(normalizedSourcePath);
        var stablePath = ResolveDeskFileStoragePath(normalizedSourcePath, deskItemId);
        var deskItem = new DeskItem
        {
            Id = deskItemId,
            Type = GetDeskItemTypeForFile(normalizedSourcePath),
            FilePath = stablePath,
            FileName = fileName,
            DisplayName = fileName,
            ReferencePath = stablePath,
            ThumbnailPath = string.Empty,
            Text = string.Empty,
            Width = cardSize.Width,
            Height = cardSize.Height,
            IsImportant = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        deskItem.Text = BuildDeskFilePreviewText(stablePath) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedLinkedTaskId))
        {
            deskItem.ContentHash = EnsureDeskItemContentHash(deskItem) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(resolvedLinkedTaskId) &&
            !string.IsNullOrWhiteSpace(deskItem.ContentHash))
        {
            var attachmentHashIndex = BuildAttachmentHashIndex();
            resolvedLinkedTaskId = TryResolveTaskIdByAttachmentHash(deskItem.ContentHash, attachmentHashIndex);
        }

        if (!string.IsNullOrWhiteSpace(resolvedLinkedTaskId))
        {
            deskItem.LinkedTaskId = resolvedLinkedTaskId;
        }

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
            DisplayName = "Notizzettel",
            Text = string.Empty,
            X = 40 + (offset % 320),
            Y = 40 + (offset % 220),
            Width = DeskNoteDefaultWidth,
            Height = DeskNoteDefaultHeight,
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

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
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

        var visibleCategoryIdBeforeChange = SelectedCategory?.Id;
        var wasAlreadyAssigned = SelectedTask.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase);

        if (checkBox.IsChecked == true)
        {
            if (!wasAlreadyAssigned)
            {
                if (await HandleSimilarCategoryAssignmentAsync(SelectedTask, selection.Category))
                {
                    return;
                }

                CaptureTaskUndoState(SelectedTask);
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

            CaptureTaskUndoState(SelectedTask);
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

        var currentVisibleCategoryWasRemoved =
            !string.IsNullOrWhiteSpace(visibleCategoryIdBeforeChange) &&
            !SelectedTask.CategoryIds.Contains(visibleCategoryIdBeforeChange, StringComparer.OrdinalIgnoreCase);

        var targetCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Id, SelectedTask.CategoryId, StringComparison.OrdinalIgnoreCase));

        _isUpdatingSelection = true;
        try
        {
            SelectedTaskCategory = targetCategory;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        _repository.SaveTask(SelectedTask);
        UpdateTaskCategoryPresentation(SelectedTask);
        RefreshTaskCategorySelections();

        if (currentVisibleCategoryWasRemoved)
        {
            if (targetCategory is not null)
            {
                SelectCategoryAndTask(targetCategory, SelectedTask);
                return;
            }
        }

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
            "Name" => tasks
                .OrderBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.CreatedAt),

            "Termin" => tasks
                .OrderBy(task => task.DueDate.HasValue ? 0 : 1)
                .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase),

            "Erstellt am" => tasks
                .OrderBy(task => task.CreatedAt)
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

    private async void DeleteTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        var confirmed = await ShowDeleteTaskConfirmationDialog(task);
        if (!confirmed)
        {
            return;
        }

        CaptureTaskUndoState(task);
        _repository.DeleteTask(task.Id);
        if (!task.IsDeleted)
        {
            task.IsDeleted = true;
            task.DeletedAt = DateTime.Now;
        }
        task.UpdatedAt = DateTime.Now;
        _tasksPendingDuplicateCheck.Remove(task.Id);
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private void RestoreTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null || !SelectedTask.IsDeleted)
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask);
        var task = SelectedTask;
        task.IsDeleted = false;
        task.DeletedAt = null;
        task.UpdatedAt = DateTime.Now;
        _repository.SaveTask(task);

        var targetCategory = GetTaskNavigationCategory(task) ?? GetDefaultStartupCategory() ?? Categories.FirstOrDefault(category => !IsSpecialCategory(category));
        if (targetCategory is not null)
        {
            SelectCategoryAndTask(targetCategory, task);
            LoadTaskDetails();
        }
        else
        {
            RefreshVisibleTasks();
        }

        UpdateCategoryCounts();
    }

    private async void EmptyTrash_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!IsTrashSelected || !HasTrashItems)
        {
            return;
        }

        var deletedCount = AllTasks.Count(task => task.IsDeleted);
        if (deletedCount == 0)
        {
            return;
        }

        var confirmed = await ShowEmptyTrashConfirmationDialog(deletedCount);
        if (!confirmed)
        {
            return;
        }

        if (SelectedTask?.IsDeleted == true)
        {
            ClearSelectedTask();
        }

        _repository.EmptyTrash();

        foreach (var task in AllTasks.Where(task => task.IsDeleted).ToList())
        {
            AllTasks.Remove(task);
        }

        ClearTaskUndoState();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }


    private async Task<bool> ShowDeleteTaskConfirmationDialog(TaskItem task)
    {
        var title = string.IsNullOrWhiteSpace(SelectedTask?.Title)
            ? "Dieser Auftrag"
            : SelectedTask.Title.Trim();

        var dialog = new Window
        {
            Title = "Auftrag löschen",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Auftrag in den Papierkorb verschieben?",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#111827")),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"„{title}“ wird aus den normalen Listen entfernt und in den Papierkorb verschoben. Anhänge bleiben erhalten.",
                            FontSize = 13,
                            Foreground = new SolidColorBrush(Color.Parse("#374151")),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Margin = new Thickness(0, 4, 0, 0),
                            Children =
                            {
                                CreateDialogAction("Abbrechen", false),
                                CreateDialogAction("In Papierkorb verschieben", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var cancelAction = (Border)buttonsPanel.Children[0];
        var deleteAction = (Border)buttonsPanel.Children[1];

        var result = false;

        cancelAction.PointerReleased += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        deleteAction.PointerReleased += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;

        static Border CreateDialogAction(string text, bool isDanger)
        {
            var normalBackground = isDanger ? "#DC2626" : "#F3F4F6";
            var hoverBackground = isDanger ? "#B91C1C" : "#DBEAFE";
            var normalBorder = isDanger ? "#B91C1C" : "#D1D5DB";
            var hoverBorder = isDanger ? "#991B1B" : "#93C5FD";
            IBrush foreground = isDanger ? Brushes.White : new SolidColorBrush(Color.Parse("#111827"));

            var label = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var action = new Border
            {
                MinWidth = isDanger ? 145 : 105,
                Height = 34,
                Background = new SolidColorBrush(Color.Parse(normalBackground)),
                BorderBrush = new SolidColorBrush(Color.Parse(normalBorder)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6),
                Child = label
            };

            action.PointerEntered += (_, _) =>
            {
                action.Background = new SolidColorBrush(Color.Parse(hoverBackground));
                action.BorderBrush = new SolidColorBrush(Color.Parse(hoverBorder));
                label.Foreground = foreground;
            };

            action.PointerExited += (_, _) =>
            {
                action.Background = new SolidColorBrush(Color.Parse(normalBackground));
                action.BorderBrush = new SolidColorBrush(Color.Parse(normalBorder));
                label.Foreground = foreground;
            };

            return action;
        }
    }

    private async Task<bool> ShowEmptyTrashConfirmationDialog(int deletedCount)
    {
        var dialog = new Window
        {
            Title = "Papierkorb leeren",
            Width = 430,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#F9FAFB")),
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Papierkorb endgültig leeren?",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#111827")),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = deletedCount == 1
                                ? "1 gelöschte Aufgabe wird endgültig entfernt. Anhänge bleiben als Dateien erhalten."
                                : $"{deletedCount} gelöschte Aufgaben werden endgültig entfernt. Anhänge bleiben als Dateien erhalten.",
                            FontSize = 13,
                            Foreground = new SolidColorBrush(Color.Parse("#374151")),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Margin = new Thickness(0, 4, 0, 0),
                            Children =
                            {
                                CreateDialogAction("Abbrechen", false),
                                CreateDialogAction("Papierkorb leeren", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var cancelAction = (Border)buttonsPanel.Children[0];
        var deleteAction = (Border)buttonsPanel.Children[1];

        var result = false;

        cancelAction.PointerReleased += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        deleteAction.PointerReleased += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;

        static Border CreateDialogAction(string text, bool isDanger)
        {
            var normalBackground = isDanger ? "#DC2626" : "#F3F4F6";
            var hoverBackground = isDanger ? "#B91C1C" : "#DBEAFE";
            var normalBorder = isDanger ? "#B91C1C" : "#D1D5DB";
            var hoverBorder = isDanger ? "#991B1B" : "#93C5FD";
            IBrush foreground = isDanger ? Brushes.White : new SolidColorBrush(Color.Parse("#111827"));

            var label = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var action = new Border
            {
                MinWidth = isDanger ? 145 : 105,
                Height = 34,
                Background = new SolidColorBrush(Color.Parse(normalBackground)),
                BorderBrush = new SolidColorBrush(Color.Parse(normalBorder)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6),
                Child = label
            };

            action.PointerEntered += (_, _) =>
            {
                action.Background = new SolidColorBrush(Color.Parse(hoverBackground));
                action.BorderBrush = new SolidColorBrush(Color.Parse(hoverBorder));
                label.Foreground = foreground;
            };

            action.PointerExited += (_, _) =>
            {
                action.Background = new SolidColorBrush(Color.Parse(normalBackground));
                action.BorderBrush = new SolidColorBrush(Color.Parse(normalBorder));
                label.Foreground = foreground;
            };

            return action;
        }
    }

    private void AddMaterial_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null)
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask);
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

        CaptureTaskUndoState(SelectedTask ?? AllTasks.FirstOrDefault(task => string.Equals(task.Id, item.TaskId, StringComparison.OrdinalIgnoreCase)));
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
            var normalizedSourcePath = Path.GetFullPath(sourcePath);
            var originalName = Path.GetFileName(normalizedSourcePath);
            var destinationPath = ResolveAttachmentStoragePath(normalizedSourcePath, SelectedTask.Id);

            var attachment = new AttachmentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                TaskId = SelectedTask.Id,
                FileName = originalName,
                StoredPath = AppPaths.ToStoredPath(destinationPath),
                ThumbnailPath = string.Empty,
                FileType = Path.GetExtension(originalName),
                AddedAt = DateTime.Now
            };

            attachment.ContentHash = EnsureAttachmentContentHash(attachment, persist: false) ?? string.Empty;
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
            var storedPath = ResolveAttachmentPath(attachment.StoredPath, attachment.TaskId, attachment.FileName);
            if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
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

    private void OpenAttachmentExternalFromList_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AttachmentItem item })
        {
            return;
        }

        var storedPath = ResolveAttachmentPath(item.StoredPath, item.TaskId, item.FileName);
        if (!TryOpenExternalFile(storedPath, out var errorMessage))
        {
            AttachmentEditStatus = errorMessage ?? $"Datei nicht gefunden: {storedPath}";
        }
    }

    private void OpenAttachmentExternal_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: AttachmentItem item })
        {
            return;
        }

        var storedPath = ResolveAttachmentPath(item.StoredPath, item.TaskId, item.FileName);
        if (!TryOpenExternalFile(storedPath, out var errorMessage))
        {
            AttachmentEditStatus = errorMessage ?? $"Datei nicht gefunden: {storedPath}";
        }
    }

    private async void ReassignAttachmentFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: AttachmentItem item })
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            AttachmentEditStatus = "Dateiauswahl ist nicht verfügbar.";
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Anhang neu zuordnen",
            AllowMultiple = false
        });

        var newPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(newPath))
        {
            return;
        }

        if (!File.Exists(newPath))
        {
            AttachmentEditStatus = $"Datei nicht gefunden: {newPath}";
            return;
        }

        CaptureTaskUndoState(SelectedTask ?? AllTasks.FirstOrDefault(task => string.Equals(task.Id, item.TaskId, StringComparison.OrdinalIgnoreCase)));
        var storedPath = ResolveAttachmentStoragePath(newPath, item.TaskId);
        item.StoredPath = AppPaths.ToStoredPath(storedPath);
        item.FileName = Path.GetFileName(newPath);
        item.ThumbnailPath = string.Empty;
        item.ContentHash = string.Empty;
        EnsureAttachmentThumbnail(item);
        _repository.SaveAttachment(item);

        if (SelectedAttachment?.Id == item.Id)
        {
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(HasPreviewPlaceholder));
            OnPropertyChanged(nameof(AttachmentPreviewTitle));
            OnPropertyChanged(nameof(AttachmentPreviewInfo));
        }

        AttachmentEditStatus = $"Datei neu zugeordnet: {item.FileName}";
    }

    private void PlaceAttachmentOnDesk_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AttachmentItem item })
        {
            return;
        }

        var sourcePath = ResolveAttachmentPath(item.StoredPath, item.TaskId, item.FileName);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            AttachmentEditStatus = $"Die gespeicherte Anhangdatei wurde nicht gefunden: {item.FileName}";
            return;
        }

        var placementIndex = Math.Min(DeskItems.Count(deskItem => deskItem.IsFileCard), 5);
        if (!AddDeskFileCardFromPath(sourcePath, GetDeskViewportCenter(), placementIndex, item.TaskId))
        {
            AttachmentEditStatus = $"Anhang konnte nicht auf den Schreibtisch gelegt werden: {item.FileName}";
            return;
        }

        AttachmentEditStatus = $"Anhang auf den Schreibtisch gelegt: {item.FileName}.";
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == DeskCategoryId) ?? SelectedCategory;
        ApplySelectedCategoryContent();
    }

    private void DeleteAttachment_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = sender switch
        {
            Button { Tag: AttachmentItem buttonItem } => buttonItem,
            MenuItem { Tag: AttachmentItem menuItemTag } => menuItemTag,
            MenuItem { DataContext: AttachmentItem menuItemDataContext } => menuItemDataContext,
            _ => null
        };

        if (item is null)
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask ?? AllTasks.FirstOrDefault(task => string.Equals(task.Id, item.TaskId, StringComparison.OrdinalIgnoreCase)));
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

        if (wasSelected)
        {
            SelectedAttachment = null;
        }

        Attachments.Remove(item);
        AttachmentEditStatus = $"Anhang entfernt: {item.FileName}";
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
        DeleteDataFileIfUnreferenced(path);
    }

    private void DeleteDataFileIfUnreferenced(string? path, string? taskId = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var resolvedPath = ResolveAttachmentPath(path, taskId);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !IsInsideDataFolder(resolvedPath))
            {
                return;
            }

            if (_repository.HasDataPathReference(resolvedPath))
            {
                return;
            }

            TryDeleteFile(resolvedPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not evaluate file cleanup for '{path}': {ex}");
        }
    }

    private static bool IsInsideDataFolder(string path)
    {
        return AppPaths.IsPathInsideAppDataDirectory(path);
    }

    private List<DeskItem> GetDeskItemsToDeleteForTask(string taskId)
    {
        return DeskItems
            .Where(item => ShouldDeleteDeskItemForTask(item, taskId))
            .ToList();
    }

    private static bool ShouldDeleteDeskItemForTask(DeskItem deskItem, string taskId)
    {
        return string.Equals(deskItem.LinkedTaskId, taskId, StringComparison.OrdinalIgnoreCase) ||
               ReferencesTaskAttachmentFolder(deskItem.FilePath, taskId) ||
               ReferencesTaskAttachmentFolder(deskItem.ReferencePath, taskId) ||
               ReferencesTaskAttachmentFolder(deskItem.ThumbnailPath, taskId);
    }

    private static bool ReferencesTaskAttachmentFolder(string? path, string taskId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var relativePath = AppPaths.ToStoredPath(path).Replace('\\', '/');
            return relativePath.StartsWith($"Tasks/{taskId}/Attachments/", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void DeleteDeskItemsForTask(IEnumerable<DeskItem> deskItems)
    {
        var removedAny = false;
        foreach (var deskItem in deskItems)
        {
            if (_draggedDeskItem == deskItem)
            {
                ClearDeskDragState();
            }

            if (_resizedDeskItem == deskItem)
            {
                ClearDeskResizeState();
            }

            UnsubscribeDeskItem(deskItem);
            _repository.DeleteDeskItem(deskItem.Id);
            DeskItems.Remove(deskItem);
            removedAny = true;
        }

        if (removedAny)
        {
            UpdateDeskSurfaceBounds();
        }
    }

    private void DeleteTaskFilesIfUnreferenced(string taskId, IEnumerable<AttachmentItem> attachments, IEnumerable<DeskItem> deskItems)
    {
        foreach (var attachment in attachments)
        {
            DeleteDataFileIfUnreferenced(attachment.StoredPath, attachment.TaskId);
            DeleteDataFileIfUnreferenced(attachment.ThumbnailPath, attachment.TaskId);
        }

        foreach (var deskItem in deskItems)
        {
            if (AppPaths.IsDeskFileStoragePath(deskItem.FilePath) ||
                ReferencesTaskAttachmentFolder(deskItem.FilePath, taskId))
            {
                DeleteDataFileIfUnreferenced(deskItem.FilePath);
            }

            if (AppPaths.IsDeskFileStoragePath(deskItem.ThumbnailPath) ||
                ReferencesTaskAttachmentFolder(deskItem.ThumbnailPath, taskId))
            {
                DeleteDataFileIfUnreferenced(deskItem.ThumbnailPath);
            }

            if (ReferencesTaskAttachmentFolder(deskItem.ReferencePath, taskId))
            {
                DeleteDataFileIfUnreferenced(deskItem.ReferencePath);
            }
        }

        TryDeleteEmptyDirectory(AppPaths.GetAttachmentDirectory(taskId));
        TryDeleteEmptyDirectory(AppPaths.GetAttachmentBackupDirectory(taskId));
        TryDeleteEmptyDirectory(Path.Combine(AppPaths.TasksDirectory, taskId));
    }

    private static void TryDeleteEmptyDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var resolvedPath = AppPaths.ResolveStoredPath(path);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !AppPaths.IsPathInsideAppDataDirectory(resolvedPath) || !Directory.Exists(resolvedPath))
            {
                return;
            }

            if (!Directory.EnumerateFileSystemEntries(resolvedPath).Any())
            {
                Directory.Delete(resolvedPath);
            }
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not delete directory '{path}': {ex}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not delete directory '{path}': {ex}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not delete directory '{path}': {ex}");
        }
    }

    private void ImportAttachments(IEnumerable<string?> sourcePaths, string successSuffix, string? noneMessage = null)
    {
        if (SelectedTask is not null && sourcePaths.Any(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
        {
            CaptureTaskUndoState(SelectedTask);
        }

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
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            SelectedCategory = GetDefaultStartupCategory();
        }

        ApplySelectedCategoryContent();
        UpdateCategoryCounts();

        ForceStartupTaskCategory();
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

        foreach (var category in orderedCategories)
        {
            Categories.Add(category);
        }

        if (settingsCategory is not null)
        {
            Categories.Add(settingsCategory);
        }

        var selectedCategory = Categories.FirstOrDefault(category =>
                string.Equals(category.Id, selectedCategoryId, StringComparison.OrdinalIgnoreCase))
            ?? GetDefaultStartupCategory()
            ?? Categories.FirstOrDefault();

        SelectedCategory = selectedCategory;

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

    private async void DuplicateTask_OnClick(object? sender, RoutedEventArgs e)
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
        _tasksPendingDuplicateCheck.Add(copy.Id);
        RefreshVisibleTasks();
        SelectedTask = copy;

        if (await HandleNewTaskDuplicateAsync(copy))
        {
            return;
        }

        _tasksPendingDuplicateCheck.Remove(copy.Id);
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

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
        SelectedTask.Status = status;
        ApplySelectedTaskStatusRules();
        _repository.SaveTask(SelectedTask);
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private void TechnicianCombo_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSelection ||
            _isUpdatingSelection ||
            _isRefreshingVisibleTasks ||
            _selectionNavigationDepth > 0 ||
            SelectedTask is null)
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
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

    private static string ResolveDisplayPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var appDataPath = Path.GetFullPath(AppPaths.AppDataDirectory);
            var resolvedAppDataPath = ResolveDisplayDirectory(appDataPath);

            if (!string.Equals(appDataPath, resolvedAppDataPath, StringComparison.Ordinal) &&
                (string.Equals(fullPath, appDataPath, StringComparison.Ordinal) ||
                 fullPath.StartsWith(appDataPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            {
                var relativePath = Path.GetRelativePath(appDataPath, fullPath);
                return relativePath == "."
                    ? resolvedAppDataPath
                    : Path.Combine(resolvedAppDataPath, relativePath);
            }

            var directoryPath = Directory.Exists(fullPath)
                ? fullPath
                : Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                if (directoryInfo.Exists)
                {
                    var target = directoryInfo.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is DirectoryInfo targetDirectory && targetDirectory.Exists)
                    {
                        var fileName = Directory.Exists(fullPath) ? string.Empty : Path.GetFileName(fullPath);
                        return string.IsNullOrWhiteSpace(fileName)
                            ? targetDirectory.FullName
                            : Path.Combine(targetDirectory.FullName, fileName);
                    }
                }
            }

            return fullPath;
        }
        catch
        {
            return path;
        }
    }

    private static string ResolveDisplayDirectory(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var directoryInfo = new DirectoryInfo(fullPath);

            if (directoryInfo.Exists)
            {
                var target = directoryInfo.ResolveLinkTarget(returnFinalTarget: true);
                if (target is DirectoryInfo targetDirectory && targetDirectory.Exists)
                {
                    return targetDirectory.FullName;
                }
            }

            return fullPath;
        }
        catch
        {
            return path;
        }
    }

    private static bool AreSameResolvedDirectory(string firstPath, string secondPath)
    {
        try
        {
            var firstResolvedPath = Path.GetFullPath(ResolveDisplayDirectory(firstPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var secondResolvedPath = Path.GetFullPath(ResolveDisplayDirectory(secondPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return string.Equals(firstResolvedPath, secondResolvedPath, comparison);
        }
        catch
        {
            return false;
        }
    }

    private void OpenDataFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(ResolveDisplayDirectory(AppPaths.AppDataDirectory));
    }

    private void OpenDefaultDataFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenFolder(ResolveDisplayDirectory(AppPaths.DefaultAppDataDirectory));
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

        if (AreSameResolvedDirectory(folderPath, AppPaths.AppDataDirectory))
        {
            StorageLocationStatus = "Dieser Datenordner wird bereits verwendet.";
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

    private void CheckLegacyFilePaths_OnClick(object? sender, RoutedEventArgs e)
    {
        var migratedCount = 0;
        var missingCount = 0;
        var failedCount = 0;

        foreach (var deskItem in _repository.GetDeskItems())
        {
            if (TryMigrateDeskItemFile(deskItem, out var wasMissing, out var wasFailed))
            {
                migratedCount++;
            }
            else if (wasMissing)
            {
                missingCount++;
            }
            else if (wasFailed)
            {
                failedCount++;
            }

            if (TryUpdateLoadedDeskItem(deskItem))
            {
                _repository.SaveDeskItem(deskItem);
            }
        }

        foreach (var task in _repository.GetTasks())
        {
            foreach (var attachment in _repository.GetAttachments(task.Id))
            {
                if (TryMigrateAttachmentFile(attachment, out var wasMissing, out var wasFailed))
                {
                    migratedCount++;
                }
                else if (wasMissing)
                {
                    missingCount++;
                }
                else if (wasFailed)
                {
                    failedCount++;
                }

                if (TryUpdateLoadedAttachment(attachment))
                {
                    _repository.SaveAttachment(attachment);
                }
            }
        }

        FilePathCheckStatus = BuildFilePathCheckStatus(migratedCount, missingCount, failedCount);
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

    private bool TryMigrateDeskItemFile(DeskItem deskItem, out bool wasMissing, out bool wasFailed)
    {
        wasMissing = false;
        wasFailed = false;

        if (!deskItem.IsFileCard)
        {
            return false;
        }

        try
        {
            var sourcePath = ResolveDeskItemSourcePath(deskItem);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                wasMissing = true;
                return false;
            }

            var changed = false;
            var normalizedSourcePath = Path.GetFullPath(sourcePath);
            if (AppPaths.IsPathInsideAppDataDirectory(normalizedSourcePath))
            {
                var storedSourcePath = AppPaths.ToStoredPath(normalizedSourcePath);
                if (!string.Equals(deskItem.ReferencePath, storedSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    deskItem.ReferencePath = storedSourcePath;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(deskItem.FilePath))
                {
                    var resolvedFilePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
                    if (AppPaths.IsPathInsideAppDataDirectory(resolvedFilePath))
                    {
                        var storedFilePath = AppPaths.ToStoredPath(resolvedFilePath);
                        if (!string.Equals(deskItem.FilePath, storedFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            deskItem.FilePath = storedFilePath;
                            changed = true;
                        }
                    }
                    else if (!string.Equals(deskItem.FilePath, storedSourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        deskItem.FilePath = storedSourcePath;
                        changed = true;
                    }
                }
                else
                {
                    deskItem.FilePath = storedSourcePath;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(deskItem.ThumbnailPath))
                {
                    var resolvedThumbnailPath = AppPaths.ResolveDeskItemPath(deskItem.ThumbnailPath);
                    if (File.Exists(resolvedThumbnailPath))
                    {
                        if (AppPaths.IsPathInsideAppDataDirectory(resolvedThumbnailPath))
                        {
                            var storedThumbnailPath = AppPaths.ToStoredPath(resolvedThumbnailPath);
                            if (!string.Equals(deskItem.ThumbnailPath, storedThumbnailPath, StringComparison.OrdinalIgnoreCase))
                            {
                                deskItem.ThumbnailPath = storedThumbnailPath;
                                changed = true;
                            }
                        }
                        else
                        {
                            deskItem.ThumbnailPath = string.Empty;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    deskItem.UpdatedAt = DateTime.Now;
                    RefreshDeskFileCard(deskItem);
                }

                return changed;
            }

            var targetPath = ResolveDeskFileStoragePath(normalizedSourcePath, deskItem.Id);
            var stablePath = AppPaths.ToStoredPath(targetPath);
            if (!string.Equals(deskItem.ReferencePath, stablePath, StringComparison.OrdinalIgnoreCase))
            {
                deskItem.ReferencePath = stablePath;
                changed = true;
            }

            var currentFilePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
            if (string.IsNullOrWhiteSpace(currentFilePath) ||
                !File.Exists(currentFilePath) ||
                !AppPaths.IsPathInsideAppDataDirectory(currentFilePath) ||
                AppPaths.PathsEqual(currentFilePath, normalizedSourcePath))
            {
                deskItem.FilePath = stablePath;
                changed = true;
            }

            deskItem.ThumbnailPath = string.Empty;
            deskItem.UpdatedAt = DateTime.Now;
            RefreshDeskFileCard(deskItem);
            return true;
        }
        catch (Exception ex)
        {
            wasFailed = true;
            Debug.WriteLine($"Could not migrate desk item '{deskItem.Id}': {ex}");
            return false;
        }
    }

    private bool TryMigrateAttachmentFile(AttachmentItem attachment, out bool wasMissing, out bool wasFailed)
    {
        wasMissing = false;
        wasFailed = false;

        if (string.IsNullOrWhiteSpace(attachment.StoredPath))
        {
            return false;
        }

        try
        {
            var sourcePath = ResolveAttachmentPath(attachment.StoredPath, attachment.TaskId, attachment.FileName);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                wasMissing = true;
                return false;
            }

            var changed = false;
            var normalizedSourcePath = Path.GetFullPath(sourcePath);
            if (AppPaths.IsPathInsideAppDataDirectory(normalizedSourcePath))
            {
                var storedSourcePath = AppPaths.ToStoredPath(normalizedSourcePath);
                if (!string.Equals(attachment.StoredPath, storedSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    attachment.StoredPath = storedSourcePath;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(attachment.ThumbnailPath))
                {
                    var resolvedThumbnailPath = ResolveAttachmentPath(attachment.ThumbnailPath, attachment.TaskId);
                    if (File.Exists(resolvedThumbnailPath))
                    {
                        if (AppPaths.IsPathInsideAppDataDirectory(resolvedThumbnailPath))
                        {
                            var storedThumbnailPath = AppPaths.ToStoredPath(resolvedThumbnailPath);
                            if (!string.Equals(attachment.ThumbnailPath, storedThumbnailPath, StringComparison.OrdinalIgnoreCase))
                            {
                                attachment.ThumbnailPath = storedThumbnailPath;
                                changed = true;
                            }
                        }
                        else
                        {
                            attachment.ThumbnailPath = string.Empty;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    EnsureAttachmentThumbnail(attachment);
                }

                return changed;
            }

            var targetPath = ResolveAttachmentStoragePath(normalizedSourcePath, attachment.TaskId);
            attachment.StoredPath = AppPaths.ToStoredPath(targetPath);
            attachment.ThumbnailPath = string.Empty;
            EnsureAttachmentThumbnail(attachment);
            return true;
        }
        catch (Exception ex)
        {
            wasFailed = true;
            Debug.WriteLine($"Could not migrate attachment '{attachment.Id}': {ex}");
            return false;
        }
    }

    private bool TryUpdateLoadedDeskItem(DeskItem sourceItem)
    {
        var loadedItem = DeskItems.FirstOrDefault(item => item.Id == sourceItem.Id);
        if (loadedItem is null || ReferenceEquals(loadedItem, sourceItem))
        {
            return false;
        }

        loadedItem.Type = sourceItem.Type;
        loadedItem.Text = sourceItem.Text;
        loadedItem.FilePath = sourceItem.FilePath;
        loadedItem.FileName = sourceItem.FileName;
        loadedItem.DisplayName = sourceItem.DisplayName;
        loadedItem.ReferencePath = sourceItem.ReferencePath;
        loadedItem.ThumbnailPath = sourceItem.ThumbnailPath;
        loadedItem.LinkedTaskId = sourceItem.LinkedTaskId;
        loadedItem.ContentHash = sourceItem.ContentHash;
        loadedItem.X = sourceItem.X;
        loadedItem.Y = sourceItem.Y;
        loadedItem.Width = sourceItem.Width;
        loadedItem.Height = sourceItem.Height;
        loadedItem.IsImportant = sourceItem.IsImportant;
        loadedItem.CreatedAt = sourceItem.CreatedAt;
        loadedItem.UpdatedAt = sourceItem.UpdatedAt;
        return true;
    }

    private bool TryUpdateLoadedAttachment(AttachmentItem sourceItem)
    {
        var loadedItem = Attachments.FirstOrDefault(item => item.Id == sourceItem.Id);
        if (loadedItem is null || ReferenceEquals(loadedItem, sourceItem))
        {
            return false;
        }

        loadedItem.FileName = sourceItem.FileName;
        loadedItem.StoredPath = sourceItem.StoredPath;
        loadedItem.ThumbnailPath = sourceItem.ThumbnailPath;
        loadedItem.FileType = sourceItem.FileType;
        loadedItem.ContentHash = sourceItem.ContentHash;
        loadedItem.AddedAt = sourceItem.AddedAt;
        loadedItem.TaskId = sourceItem.TaskId;
        return true;
    }

    private static string BuildFilePathCheckStatus(int migratedCount, int missingCount, int failedCount)
    {
        var status = $"Prüfung abgeschlossen: {migratedCount} Datei(en) übernommen, {missingCount} fehlende Datei(en) gefunden.";
        if (failedCount > 0)
        {
            status += $" {failedCount} Datei(en) konnten nicht verarbeitet werden.";
        }

        return status;
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
        await RunManualUpdateCheckAsync();
    }

    private async void InstallUpdate_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateStatus = "Update wird vorbereitet...";
        var started = await _updateService.DownloadAndApplyUpdateAsync();
        UpdateStatus = _updateService.GetUpdateStatusText();
        IsUpdateAvailable = started && _updateService.HasPendingUpdate;

        if (started)
        {
            Close();
        }
    }

    private async Task<bool> RunStartupUpdateCheckAsync()
    {
        UpdateStatus = "Updateprüfung läuft...";
        IsUpdateAvailable = await _updateService.CheckForUpdatesAsync();
        UpdateStatus = _updateService.GetUpdateStatusText();

        if (_updateService.LastCheckFailed)
        {
            await ShowUpdateCheckFailureDialogAsync();
            return false;
        }

        if (!IsUpdateAvailable)
        {
            return false;
        }

        await ShowRequiredUpdateDialogAsync();
        Close();

        return true;
    }

    private async Task RunManualUpdateCheckAsync()
    {
        UpdateStatus = "Updateprüfung läuft...";
        IsUpdateAvailable = await _updateService.CheckForUpdatesAsync();
        UpdateStatus = _updateService.GetUpdateStatusText();
    }

    private async Task ShowUpdateCheckFailureDialogAsync()
    {
        var okButton = new Button
        {
            Content = "OK",
            IsCancel = true,
            MinWidth = 90
        };

        var dialog = new Window
        {
            Title = "Updateprüfung fehlgeschlagen",
            Width = 520,
            MinWidth = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Updateprüfung konnte nicht durchgeführt werden.",
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Die App kann weiterverwendet werden. Bitte später erneut prüfen.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 8, 0, 0),
                            Children = { okButton }
                        }
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private async Task<RequiredUpdateDialogResult> ShowRequiredUpdateDialogAsync()
    {
        var result = RequiredUpdateDialogResult.Closed;
        var isInstalling = false;

        var statusText = new TextBlock
        {
            Text = "Es ist eine neue Version verfügbar. BüroCockpit muss aktualisiert werden, bevor weitergearbeitet werden kann.",
            TextWrapping = TextWrapping.Wrap
        };

        var installButton = new Button
        {
            Content = "Update installieren",
            Classes = { "Primary" },
            MinWidth = 150
        };

        var quitButton = new Button
        {
            Content = "Beenden",
            MinWidth = 90,
            IsCancel = true
        };

        var dialog = new Window
        {
            Title = "Update erforderlich",
            Width = 560,
            MinWidth = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Margin = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Update erforderlich",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        statusText,
                        new TextBlock
                        {
                            Text = "Ohne Installation des Updates kann die App nicht weiter normal benutzt werden.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                installButton,
                                quitButton
                            }
                        }
                    }
                }
            }
        };

        installButton.Click += async (_, _) =>
        {
            if (isInstalling)
            {
                return;
            }

            isInstalling = true;
            installButton.IsEnabled = false;
            quitButton.IsEnabled = false;
            statusText.Text = "Update wird installiert...";

            var started = await _updateService.DownloadAndApplyUpdateAsync();
            statusText.Text = _updateService.GetUpdateStatusText();

            if (started)
            {
                result = RequiredUpdateDialogResult.InstallStarted;
                dialog.Close();
                return;
            }

            isInstalling = false;
            installButton.IsEnabled = true;
            quitButton.IsEnabled = true;
        };

        quitButton.Click += (_, _) =>
        {
            result = RequiredUpdateDialogResult.Closed;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private enum RequiredUpdateDialogResult
    {
        Closed,
        InstallStarted
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

        var storedPath = ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName);
        if (!File.Exists(storedPath))
        {
            AttachmentEditStatus = "Die gespeicherte Anhangdatei wurde nicht gefunden.";
            return;
        }

        var originalHash = _hashService.ComputeSha256(storedPath);
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
            File.Copy(storedPath, exportPath, overwrite: false);

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

        var currentOriginalHash = _hashService.ComputeSha256(ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName));
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
            var storedPath = ResolveAttachmentPath(SelectedAttachment.StoredPath, SelectedAttachment.TaskId, SelectedAttachment.FileName);
            var thumbnailPath = ResolveAttachmentPath(SelectedAttachment.ThumbnailPath, SelectedAttachment.TaskId);

            if (File.Exists(storedPath))
            {
                File.Copy(storedPath, backupPath, overwrite: false);
                session.BackupPath = backupPath;
            }

            File.Copy(session.ExportPath, storedPath, overwrite: true);
            TryDeleteFile(thumbnailPath);
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
            var storedPath = ResolveAttachmentPath(item.StoredPath, item.TaskId, item.FileName);
            if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = storedPath,
                    Verb = "print",
                    UseShellExecute = true
                });

                return true;
            }

            return TryOpenExternalFile(storedPath, out _);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Attachment print failed: {ex}");
            return false;
        }
    }

    private static bool TryOpenExternalFile(string path, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errorMessage = $"Datei nicht gefunden: {path}";
            return false;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            };
            process.Start();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not open file '{path}': {ex}");
            errorMessage = $"Datei konnte nicht geöffnet werden: {path}";
            return false;
        }
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
            NavigateToTask(result.Task, fromGlobalSearch: true, result.PreferredCategoryId);
        }
    }

    private void NavigateToTask(TaskItem? task, bool fromGlobalSearch, string? preferredCategoryId = null)
    {
        if (task is null || _selectionNavigationDepth > 0)
        {
            return;
        }

        var category = GetTaskNavigationCategory(task, preferredCategoryId);
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

    private CategoryItem? GetTaskNavigationCategory(TaskItem task, string? preferredCategoryId = null)
    {
        if (task.IsDeleted)
        {
            return Categories.FirstOrDefault(category => category.Id == TrashCategoryId);
        }

        var matchingCategories = new List<CategoryItem>();

        foreach (var categoryId in GetTaskCategoryIds(task))
        {
            var category = Categories.FirstOrDefault(item =>
                string.Equals(item.Id, categoryId, StringComparison.OrdinalIgnoreCase));
            if (category is null || IsSpecialCategory(category))
            {
                continue;
            }

            if (!matchingCategories.Any(item => string.Equals(item.Id, category.Id, StringComparison.OrdinalIgnoreCase)))
            {
                matchingCategories.Add(category);
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredCategoryId))
        {
            var preferredCategory = matchingCategories.FirstOrDefault(category =>
                string.Equals(category.Id, preferredCategoryId, StringComparison.OrdinalIgnoreCase));
            if (preferredCategory is not null)
            {
                return preferredCategory;
            }
        }

        if (SelectedCategory is not null &&
            !IsSpecialCategory(SelectedCategory) &&
            matchingCategories.Any(category => string.Equals(category.Id, SelectedCategory.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return SelectedCategory;
        }

        return matchingCategories.FirstOrDefault();
    }

    private void SelectCategoryAndTask(CategoryItem category, TaskItem task)
    {
        if (_selectionNavigationDepth > 0)
        {
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
            SelectedCategory = category;
            if (CategoryList is not null)
            {
                CategoryList.SelectedItem = category;
            }

            SelectedTaskCategory = category;
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

        UpdateCategoryCounts();
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
        if (task.IsDeleted && !IsTrashSelected)
        {
            return false;
        }

        var categoryNames = GetTaskCategoryNameList(task);
        if (Contains(task.CustomerName, query) ||
            Contains(task.CustomerAddress, query) ||
            Contains(task.Title, query) ||
            Contains(task.Description, query) ||
            Contains(task.AssignedTo, query) ||
            Contains(task.Technician, query) ||
            Contains(task.Status, query) ||
            categoryNames.Any(categoryName => Contains(categoryName, query)))
        {
            return true;
        }

        return _repository.GetMaterials(task.Id).Any(material => Contains(material.Name, query)) ||
               _repository.GetAttachments(task.Id).Any(attachment => Contains(attachment.FileName, query));
    }

    private string? GetSearchPreferredCategoryId(TaskItem task, string query)
    {
        if (task.IsDeleted)
        {
            return TrashCategoryId;
        }

        foreach (var categoryId in GetTaskCategoryIds(task))
        {
            var category = Categories.FirstOrDefault(item =>
                string.Equals(item.Id, categoryId, StringComparison.OrdinalIgnoreCase));
            if (category is null || IsSpecialCategory(category))
            {
                continue;
            }

            if (Contains(category.Name, query))
            {
                return category.Id;
            }
        }

        return GetTaskNavigationCategory(task)?.Id;
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
        if (task.IsDeleted || IsDoneOrArchived(task))
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

        var duplicate = FindSimilarTaskCandidate(newTask, minimumScore: 50);
        if (duplicate is null)
        {
            return false;
        }

        var choice = await ShowSimilarTaskDialogAsync(duplicate, targetCategory.Name);

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
                MoveTaskToCategory(duplicate.Task, targetCategory.Id);
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

    private async Task<bool> HandleSimilarCategoryAssignmentAsync(TaskItem currentTask, CategoryItem targetCategory)
    {
        if (!HasMeaningfulDuplicateInput(currentTask))
        {
            return false;
        }

        var duplicate = FindSimilarTaskCandidate(currentTask, excludedCategoryId: targetCategory.Id, minimumScore: 50);
        if (duplicate is null)
        {
            return false;
        }

        var choice = await ShowSimilarTaskDialogAsync(duplicate, targetCategory.Name);

        switch (choice)
        {
            case DuplicateTaskChoice.AddToCategory:
                AddTaskToCategory(duplicate.Task, targetCategory.Id);
                _repository.SaveTask(duplicate.Task);
                SelectCategoryAndTask(targetCategory, duplicate.Task);
                RefreshTaskCategorySelections();
                return true;

            case DuplicateTaskChoice.MoveToCategory:
                MoveTaskToCategory(duplicate.Task, targetCategory.Id);
                _repository.SaveTask(duplicate.Task);
                SelectCategoryAndTask(targetCategory, duplicate.Task);
                RefreshTaskCategorySelections();
                return true;

            case DuplicateTaskChoice.Cancel:
                RefreshTaskCategorySelections();
                return true;

            case DuplicateTaskChoice.CreateAnyway:
            default:
                return false;
        }
    }

    private static readonly HashSet<string> DuplicatePlaceholderTokens =
    [
        "neu",
        "neue",
        "neuer",
        "neues",
        "kunde",
        "kundenname",
        "auftrag",
        "aufgabe",
        "angebot",
        "termin",
        "test",
        "haus",
        "leer",
        "null",
        "ohne",
        "titel"
    ];

    private async Task<DuplicateTaskChoice> ShowSimilarTaskDialogAsync(SimilarTaskMatch duplicate, string targetCategoryName)
    {
        var dialog = new DuplicateTaskDialog(
            duplicate.Headline,
            GetTaskCategoryNames(duplicate.Task),
            targetCategoryName,
            duplicate.MatchPoints);
        return await dialog.ShowDialog<DuplicateTaskChoice>(this);
    }

    private CategoryItem? GetTaskTargetCategory(TaskItem task)
    {
        foreach (var categoryId in GetTaskCategoryIds(task))
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

    private SimilarTaskMatch? FindSimilarTaskCandidate(TaskItem task, string? excludedCategoryId = null, int minimumScore = 50)
    {
        return AllTasks
            .Where(candidate => !string.Equals(candidate.Id, task.Id, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => !candidate.IsDeleted)
            .Where(candidate => string.IsNullOrWhiteSpace(excludedCategoryId) ||
                !TaskBelongsToCategory(candidate, excludedCategoryId))
            .Select(candidate =>
            {
                var similarity = CalculateTaskSimilarityScore(task, candidate);
                return new SimilarTaskMatch(
                    candidate,
                    similarity.Score,
                    similarity.IsPlausible,
                    similarity.MatchPoints,
                    FormatDuplicateTaskHeadline(candidate));
            })
            .Where(match => match.Score >= minimumScore && match.IsPlausible)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Task.UpdatedAt)
            .FirstOrDefault();
    }

    private static TaskSimilarityEvaluation CalculateTaskSimilarityScore(TaskItem left, TaskItem right)
    {
        var customerNameMatch = CompareNormalizedText("Kunde", left.CustomerName, right.CustomerName, exactScore: 55, containsScore: 35);
        var titleMatch = CompareNormalizedText("Titel", left.Title, right.Title, exactScore: 45, containsScore: 25);
        var addressMatch = CompareNormalizedText("Adresse", left.CustomerAddress, right.CustomerAddress, exactScore: 50, containsScore: 30);

        var matches = new[] { customerNameMatch, titleMatch, addressMatch }
            .Where(match => match.Score > 0)
            .ToList();
        if (matches.Count == 0)
        {
            return TaskSimilarityEvaluation.None;
        }

        var score = matches.Sum(match => match.Score);
        if (matches.Count > 1)
        {
            score += 10 * (matches.Count - 1);
        }

        var isPlausible =
            (customerNameMatch.Score > 0 && addressMatch.Score > 0) ||
            (customerNameMatch.Score > 0 && titleMatch.Score > 0) ||
            (addressMatch.Score > 0 && titleMatch.Score > 0) ||
            IsStrongSingleDuplicateMatch(customerNameMatch) ||
            IsStrongSingleDuplicateMatch(addressMatch);

        return new TaskSimilarityEvaluation(
            score,
            isPlausible,
            BuildDuplicateMatchPoints(matches, score));
    }

    private static DuplicateFieldMatch CompareNormalizedText(string fieldName, string? left, string? right, int exactScore, int containsScore)
    {
        var normalizedLeft = NormalizeDuplicateValue(left);
        var normalizedRight = NormalizeDuplicateValue(right);
        if (!IsMeaningfulNormalizedDuplicateValue(normalizedLeft) ||
            !IsMeaningfulNormalizedDuplicateValue(normalizedRight))
        {
            return DuplicateFieldMatch.None(fieldName);
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return new DuplicateFieldMatch(fieldName, exactScore, true, normalizedLeft);
        }

        if (normalizedLeft.Length >= 6 &&
            normalizedRight.Length >= 6 &&
            (normalizedLeft.Contains(normalizedRight, StringComparison.Ordinal) ||
             normalizedRight.Contains(normalizedLeft, StringComparison.Ordinal)))
        {
            return new DuplicateFieldMatch(
                fieldName,
                containsScore,
                false,
                normalizedLeft.Length <= normalizedRight.Length ? normalizedLeft : normalizedRight);
        }

        return DuplicateFieldMatch.None(fieldName);
    }

    private static bool HasMeaningfulDuplicateInput(TaskItem task)
    {
        return IsMeaningfulDuplicateValue(task.CustomerName) ||
               IsMeaningfulDuplicateValue(task.Title) ||
               IsMeaningfulDuplicateValue(task.CustomerAddress);
    }

    private static bool IsMeaningfulDuplicateValue(string? value)
    {
        return IsMeaningfulNormalizedDuplicateValue(NormalizeDuplicateValue(value));
    }

    private static bool IsMeaningfulNormalizedDuplicateValue(string normalizedValue)
    {
        return normalizedValue.Length >= 4 && !IsDuplicatePlaceholder(normalizedValue);
    }

    private static bool IsDuplicatePlaceholder(string normalizedValue)
    {
        if (normalizedValue.Length == 0)
        {
            return true;
        }

        if (normalizedValue.Equals("ohne kundenname", StringComparison.Ordinal) ||
            normalizedValue.Equals("ohne titel", StringComparison.Ordinal))
        {
            return true;
        }

        var tokens = normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 && tokens.All(token =>
            DuplicatePlaceholderTokens.Contains(token) ||
            token.All(char.IsDigit));
    }

    private static string NormalizeDuplicateValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim()
            .ToLowerInvariant()
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static bool IsStrongSingleDuplicateMatch(DuplicateFieldMatch match)
    {
        if (match.Score == 0 || !match.IsExact)
        {
            return false;
        }

        return match.FieldName switch
        {
            "Kunde" => IsStrongSingleCustomerValue(match.NormalizedValue),
            "Adresse" => IsStrongSingleAddressValue(match.NormalizedValue),
            _ => false
        };
    }

    private static bool IsStrongSingleCustomerValue(string normalizedValue)
    {
        var wordCount = CountDuplicateWords(normalizedValue);
        return normalizedValue.Length >= 10 && (wordCount >= 2 || normalizedValue.Length >= 12);
    }

    private static bool IsStrongSingleAddressValue(string normalizedValue)
    {
        return normalizedValue.Length >= 10 &&
               CountDuplicateWords(normalizedValue) >= 2 &&
               normalizedValue.Any(char.IsDigit);
    }

    private static int CountDuplicateWords(string normalizedValue)
    {
        return normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string BuildDuplicateMatchPoints(IEnumerable<DuplicateFieldMatch> matches, int score)
    {
        var parts = matches.Select(match =>
            match.IsExact
                ? $"{match.FieldName} exakt"
                : $"{match.FieldName} teilweise");
        return $"{string.Join(", ", parts)} ({score} Punkte)";
    }

    private static string FormatDuplicateTaskHeadline(TaskItem task)
    {
        var parts = new List<string>();

        if (IsMeaningfulDuplicateValue(task.CustomerName))
        {
            parts.Add(task.CustomerName.Trim());
        }

        if (IsMeaningfulDuplicateValue(task.Title))
        {
            parts.Add(task.Title.Trim());
        }

        return parts.Count switch
        {
            0 => "Unbenannter Auftrag",
            1 => parts[0],
            _ => $"{parts[0]} / {parts[1]}"
        };
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

    private static void MoveTaskToCategory(TaskItem task, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return;
        }

        task.CategoryIds = [categoryId];
        task.CategoryId = categoryId;
        EnsureTaskCategoryState(task);
    }

    private List<string> GetTaskCategoryNameList(TaskItem task)
    {
        var ids = GetTaskCategoryIds(task);

        return ids
            .Select(id => Categories.FirstOrDefault(category => string.Equals(category.Id, id, StringComparison.OrdinalIgnoreCase))?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private void UpdateTaskCategoryPresentation(TaskItem task)
    {
        var categoryNames = GetTaskCategoryNameList(task);
        task.CategoryNameChips = categoryNames;
        task.CategoryHint = string.Join(", ", categoryNames);
        task.ShowCategoryHint = categoryNames.Count > 0;
    }

    private string GetTaskCategoryNames(TaskItem task)
    {
        var categoryIds = GetTaskCategoryIds(task);
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

    private CategoryItem? GetDefaultStartupCategory()
    {
        return Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufträge", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category => !IsSpecialCategory(category))
            ?? Categories.FirstOrDefault();
    }

    private CategoryItem? GetStartupTaskCategory()
    {
        return Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufträge", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                !IsSpecialCategory(category) && category.IsVisible)
            ?? Categories.FirstOrDefault(category => !IsSpecialCategory(category));
    }

    private void ForceStartupTaskCategory()
    {
        var startupCategory = GetStartupTaskCategory();
        Console.WriteLine($"STARTDIAG: startupCategory={startupCategory?.Name} id={startupCategory?.Id}");
        Console.WriteLine($"STARTDIAG: before SelectedCategory={SelectedCategory?.Name} id={SelectedCategory?.Id} IsDeskSelected={IsDeskSelected}");

        if (startupCategory is null)
        {
            return;
        }

        SelectedCategory = startupCategory;

        if (CategoryList is not null)
        {
            CategoryList.SelectedItem = startupCategory;
        }

        ApplySelectedCategoryContent();
        RefreshVisibleTasks();
        UpdateCategoryCounts();

        Console.WriteLine($"STARTDIAG: after SelectedCategory={SelectedCategory?.Name} id={SelectedCategory?.Id} IsDeskSelected={IsDeskSelected}");
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

    private static CategoryItem CreateTrashCategory()
    {
        return new CategoryItem
        {
            Id = TrashCategoryId,
            Name = TrashCategoryName,
            SortOrder = int.MaxValue - 1,
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

    private static bool TaskBelongsToCategory(TaskItem task, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return false;
        }

        return GetTaskCategoryIds(task).Any(id => string.Equals(id, categoryId, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetTaskCategoryIds(TaskItem task)
    {
        var categoryIds = new List<string>();

        void AddCategoryId(string? categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                return;
            }

            if (categoryIds.Any(id => string.Equals(id, categoryId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            categoryIds.Add(categoryId);
        }

        AddCategoryId(task.CategoryId);
        foreach (var categoryId in task.CategoryIds)
        {
            AddCategoryId(categoryId);
        }

        return categoryIds;
    }

    private static bool IsSpecialCategory(CategoryItem category)
    {
        return category.Id == DeskCategoryId ||
               category.Id == TrashCategoryId ||
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

        EnsureTaskCategoryState(SelectedTask);
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
                if (!SelectedTask.CategoryIds.Contains(archive.Id, StringComparer.OrdinalIgnoreCase))
                {
                    SelectedTask.CategoryIds.Insert(0, archive.Id);
                }
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
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not delete file '{path}': {ex}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not delete file '{path}': {ex}");
        }
    }

    private static string ResolveAttachmentPath(string? path, string? taskId = null, string? fileName = null)
    {
        return string.IsNullOrWhiteSpace(taskId)
            ? AppPaths.ResolveStoredPath(path)
            : AppPaths.ResolveTaskAttachmentPath(taskId, path, fileName);
    }

    private static void LogMissingAttachment(AttachmentItem attachment, string resolvedPath)
    {
        Console.WriteLine(
            $"Attachment missing: FileName='{attachment.FileName}', StoredPathRaw='{attachment.StoredPath}', StoredPathResolved='{resolvedPath}', File.Exists={File.Exists(resolvedPath)}");
    }

    private static string? TryGetLinkedTaskIdFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relativePath = AppPaths.ToStoredPath(path).Replace('\\', '/');
        if (!relativePath.StartsWith("Tasks/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var attachmentsMarker = "/Attachments/";
        var attachmentsIndex = relativePath.IndexOf(attachmentsMarker, StringComparison.OrdinalIgnoreCase);
        if (attachmentsIndex <= "Tasks/".Length)
        {
            return null;
        }

        var taskId = relativePath["Tasks/".Length..attachmentsIndex];
        return string.IsNullOrWhiteSpace(taskId) ? null : taskId;
    }

    private static string? TryResolveDeskItemLinkedTaskIdFromPath(DeskItem deskItem)
    {
        return !string.IsNullOrWhiteSpace(deskItem.LinkedTaskId)
            ? deskItem.LinkedTaskId
            : TryGetLinkedTaskIdFromPath(deskItem.ReferencePath) ?? TryGetLinkedTaskIdFromPath(deskItem.FilePath);
    }

    private string? EnsureDeskItemContentHash(DeskItem deskItem)
    {
        if (!string.IsNullOrWhiteSpace(deskItem.ContentHash))
        {
            return deskItem.ContentHash;
        }

        var filePath = AppPaths.ResolveDeskItemPath(deskItem.FilePath, deskItem.FileName);
        var contentHash = _hashService.ComputeSha256(filePath);
        if (!string.IsNullOrWhiteSpace(contentHash))
        {
            deskItem.ContentHash = contentHash;
        }

        return contentHash;
    }

    private string? EnsureAttachmentContentHash(AttachmentItem attachment, bool persist)
    {
        if (!string.IsNullOrWhiteSpace(attachment.ContentHash))
        {
            return attachment.ContentHash;
        }

        var contentHash = _hashService.ComputeSha256(ResolveAttachmentPath(attachment.StoredPath, attachment.TaskId, attachment.FileName));
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        attachment.ContentHash = contentHash;
        if (persist)
        {
            _repository.SaveAttachment(attachment);
        }

        return contentHash;
    }

    private Dictionary<string, string?> BuildAttachmentHashIndex()
    {
        var hashIndex = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var task in AllTasks)
        {
            foreach (var attachment in _repository.GetAttachments(task.Id))
            {
                var contentHash = EnsureAttachmentContentHash(attachment, persist: true);
                if (string.IsNullOrWhiteSpace(contentHash))
                {
                    continue;
                }

                if (hashIndex.TryGetValue(contentHash, out var knownTaskId))
                {
                    if (knownTaskId is not null &&
                        !string.Equals(knownTaskId, attachment.TaskId, StringComparison.OrdinalIgnoreCase))
                    {
                        hashIndex[contentHash] = null;
                    }
                }
                else
                {
                    hashIndex[contentHash] = attachment.TaskId;
                }
            }
        }

        return hashIndex;
    }

    private static string? TryResolveTaskIdByAttachmentHash(string contentHash, IReadOnlyDictionary<string, string?> attachmentHashIndex)
    {
        return attachmentHashIndex.TryGetValue(contentHash, out var taskId) ? taskId : null;
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

        if (_resizedDeskItem == deskItem)
        {
            ClearDeskResizeState();
        }

        UnsubscribeDeskItem(deskItem);
        _repository.DeleteDeskItem(deskItem.Id);
        if (deskItem.IsFileCard)
        {
            DeleteDeskItemStorageFileIfUnreferenced(deskItem.ThumbnailPath);
            DeleteDeskItemStorageFileIfUnreferenced(deskItem.FilePath);
        }
        DeskItems.Remove(deskItem);
        UpdateDeskSurfaceBounds();
    }

    private void DeleteDeskItemStorageFileIfUnreferenced(string? path)
    {
        if (!AppPaths.IsDeskFileStoragePath(path))
        {
            return;
        }

        DeleteDataFileIfUnreferenced(path);
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

    private void ClearDeskResizeState()
    {
        if (_resizedDeskPointer is not null)
        {
            _resizedDeskPointer.Capture(null);
        }

        _resizedDeskItem = null;
        _resizedDeskPointer = null;
        _isResizingDeskItem = false;
        _deskResizeCurrentDelta = default;
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

    private void FocusDeskItemNameEditor(DeskItem deskItem)
    {
        var textBox = DeskSurface.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(control =>
                ReferenceEquals(control.DataContext, deskItem) &&
                control.Classes.Any(styleClass => styleClass == "DeskItemNameEditor"));

        if (textBox is null)
        {
            return;
        }

        textBox.Focus();
        textBox.SelectAll();
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

public sealed record TaskUndoSnapshot(TaskItem Task, IReadOnlyList<MaterialItem> Materials, IReadOnlyList<AttachmentItem> Attachments);

public sealed class TaskSearchResult
{
    public TaskSearchResult(
        TaskItem task,
        string categoryName,
        IReadOnlyList<string> categoryNameChips,
        string? preferredCategoryId,
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
        CategoryNameChips = new List<string>(categoryNameChips);
        PreferredCategoryId = preferredCategoryId ?? string.Empty;
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
    public IReadOnlyList<string> CategoryNameChips { get; }
    public string PreferredCategoryId { get; }
    public bool HasCategoryNameChips => CategoryNameChips.Count > 0;
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

public sealed record SimilarTaskMatch(TaskItem Task, int Score, bool IsPlausible, string MatchPoints, string Headline);

public sealed record TaskSimilarityEvaluation(int Score, bool IsPlausible, string MatchPoints)
{
    public static readonly TaskSimilarityEvaluation None = new(0, false, string.Empty);
}

public sealed record DuplicateFieldMatch(string FieldName, int Score, bool IsExact, string NormalizedValue)
{
    public static DuplicateFieldMatch None(string fieldName) => new(fieldName, 0, false, string.Empty);
}

public sealed class DuplicateTaskDialog : Window
{
    public DuplicateTaskDialog(string existingTaskHeadline, string existingCategories, string targetCategory, string matchPoints)
    {
        Title = "Ähnlicher Auftrag gefunden";
        Width = 500;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 10
        };

        content.Children.Add(new TextBlock
        {
            Text = $"Es gibt bereits einen ähnlichen Auftrag: {existingTaskHeadline}. Was soll passieren?",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(CreateInfoText($"Bestehender Auftrag: {existingTaskHeadline}"));
        content.Children.Add(CreateInfoText($"Bestehende Kategorie(n): {existingCategories}"));
        content.Children.Add(CreateInfoText($"Neue gewünschte Kategorie: {targetCategory}"));
        content.Children.Add(CreateInfoText($"Übereinstimmungspunkte: {matchPoints}"));

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        buttonPanel.Children.Add(CreateChoiceButton(
            "Bestehenden Auftrag verschieben",
            DuplicateTaskChoice.MoveToCategory));
        buttonPanel.Children.Add(CreateChoiceButton(
            "Zusätzlich zuordnen",
            DuplicateTaskChoice.AddToCategory));
        buttonPanel.Children.Add(CreateChoiceButton(
            "Neuen Auftrag trotzdem behalten",
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
            Padding = new Thickness(12, 8),
            IsCancel = choice == DuplicateTaskChoice.Cancel,
            Background = new SolidColorBrush(Color.Parse("#F8FAFC")),
            Foreground = new SolidColorBrush(Color.Parse("#111827")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CBD5E1")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };

        button.PointerEntered += (_, _) =>
        {
            button.Background = new SolidColorBrush(Color.Parse("#DBEAFE"));
            button.Foreground = new SolidColorBrush(Color.Parse("#111827"));
            button.BorderBrush = new SolidColorBrush(Color.Parse("#93C5FD"));
        };

        button.PointerExited += (_, _) =>
        {
            button.Background = new SolidColorBrush(Color.Parse("#F8FAFC"));
            button.Foreground = new SolidColorBrush(Color.Parse("#111827"));
            button.BorderBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));
        };

        button.Click += (_, _) => Close(choice);
        return button;
    }
}
