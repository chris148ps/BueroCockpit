using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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
using BueroCockpit.Services.LocalSync;
using Microsoft.Data.Sqlite;

namespace BueroCockpit;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private bool _dateTextFormattingActive;
    private const string OverviewCategoryId = "__overview";
    private const string DeskCategoryId = "__desk";
    private const string DeskCategoryName = "Schreibtisch";
    private const string OverviewCategoryName = "Übersicht";
    private const string TrashCategoryId = "__trash";
    private const string TrashCategoryName = "Papierkorb";
    private const string MobileInboxCategoryId = "__mobile_inbox";
    private const string MobileInboxCategoryName = "Mobile Eingänge";
    private const string MobileInboxImportMarkerPrefix = "MobileInboxId:";
    private const int MobileProcessedRetentionDays = 30;
    private const string OneDriveDataFolderName = "BueroCockpit_Daten";
    private const string LegacyOneDriveDataFolderName = "BueroCockpit_iPad_Bearbeitung";
    private const string CompanyOneDriveMacFolderName = "OneDrive-ElektroSchweim";
    private const string CompanyOneDriveWindowsFolderName = "OneDrive - Elektro Schweim";
    private const string SettingsCategoryId = "__settings";
    private const string SettingsCategoryName = "Einstellungen";
    private static readonly HashSet<string> RootEndCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        DeskCategoryName,
        "Offene Aufgaben",
        "Wartet auf Kunde"
    };
    private const string SortFieldDate = "Datum";
    private const string SortFieldName = "Name";
    private const string SortFieldManual = "Manuell";
    private const string SortFieldCreatedAt = "Erstellt am";
    private const string SortFieldFollowUp = "Wiedervorlage";
    private const string SortFieldSentAt = "Gesendet am";
    private const string SortFieldUpdatedAt = "Geändert am";
    private const string DeskItemTypeNote = "Note";
    private const string DeskItemTypeFile = "File";
    private const string DeskItemTypePdf = "Pdf";
    private const string DeskItemTypeImage = "Image";
    private const string CategoryDragTextPrefix = "buerocockpit-category:";
    private static readonly DataFormat<string> CategoryDragDataFormat =
        DataFormat.CreateInProcessFormat<string>("buerocockpit-category-id");
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
    private const int DefaultLocalNetworkSyncPort = AppSettingsService.DefaultLocalNetworkSyncPort;
    private readonly StorageLocationService _storageLocationService = new();
    private readonly AppInstanceLockService _appInstanceLockService = new();
    private readonly BueroRepository _repository;
    private readonly ThumbnailService _thumbnailService;
    private readonly BackupService _backupService;
    private readonly UpdateService _updateService;
    private readonly AppSettingsService _settingsService;
    private readonly LiveSettingsService _liveSettingsService;
    private readonly LocalNetworkDeviceStore _localNetworkDeviceStore = new();
    private readonly FileHashService _hashService;
    private readonly IpadSnapshotExportService _ipadSnapshotExportService;
    private readonly MobileInboxLoader _mobileInboxLoader = new();
    private readonly HashSet<string> _tasksPendingDuplicateCheck = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MobileInboxEntry> _mobileInboxTaskMap = new(StringComparer.OrdinalIgnoreCase);
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
    private string _selectedSortField = SortFieldCreatedAt;
    private bool _isSortDescending = true;
    private string _dueDateText = string.Empty;
    private string _dueTimeText = string.Empty;
    private string _followUpDateText = string.Empty;
    private string _sentAtText = string.Empty;
    private string _materialOrderedAtText = string.Empty;
    private string _dateInputMessage = string.Empty;
    private string _taskUndoMessage = string.Empty;
    private MobileInboxPreviewItem? _selectedMobileInboxPreviewItem;
    private string _mobileInboxPreviewStatus = string.Empty;
    private bool _isGlobalSearchEnabled;
    private bool _includeArchiveInSearch;
    private string _categoryEditorName = string.Empty;
    private string _categoryMessage = string.Empty;
    private string _mobileInboxCleanupStatus = string.Empty;
    private string _backupStatus = "Noch kein Backup erstellt.";
    private string _storageLocationStatus = "Speicherort nicht geändert.";
    private string _appInstanceLockStatus = "Datenordner-Zugriffsschutz noch nicht geprüft.";
    private string _deskStatus = string.Empty;
    private string _filePathCheckStatus = string.Empty;
    private string _ipadSnapshotStatus = string.Empty;
    private string _ipadLiveFileStatus = "Kein Zielordner eingerichtet";
    private string _ipadLiveFileLastSuccessfulExport = string.Empty;
    private string _ipadLiveFileLastError = string.Empty;
    private string _lastBackupPath = string.Empty;
    private string _lastBackupTime = string.Empty;
    private string _updateStatus = "Noch kein Update-Kanal eingerichtet.";
    private string _updateFeedUrl = string.Empty;
    private string _localNetworkSyncDeviceNameInput = string.Empty;
    private string _localNetworkSyncPortInput = string.Empty;
    private string _localNetworkSyncSettingsStatus = string.Empty;
    private string _localNetworkSyncTestServiceStatus = "Testdienst gestoppt";
    private string? _startupDatabaseErrorMessage;
    private LocalSyncService? _localNetworkSyncTestService;
    private string _appearanceMode = DarkMode;
    private bool _isSettingsGeneralOpen;
    private bool _isSettingsDataSyncOpen;
    private bool _isSettingsLocalNetworkSyncOpen;
    private bool _isSettingsTechniciansOpen;
    private bool _isSettingsCategoriesOpen;
    private bool _isSettingsOrdersOpen;
    private bool _isSettingsUpdatesOpen;
    private bool _isSettingsDiagnosticsOpen;
    private bool _isUpdateAvailable;
    private bool _startupUpdateCheckCompleted;
    private string _attachmentEditStatus = string.Empty;
    private bool _isLoadingSelection;
    private bool _isLoadingData;
    private bool _isUpdatingSelection;
    private bool _isRefreshingVisibleTasks;
    private bool _isSavingCategorySnapshot;
    private int _isRunningIpadSnapshotExport;
    private int _isPendingIpadSnapshotExport;
    private int _isRunningIpadLiveFileExport;
    private int _isPendingIpadLiveFileExport;
    private bool _isIpadSnapshotExportRunning;
    private CancellationTokenSource? _ipadSnapshotStatusHideCts;
    private bool _suppressTaskListSelectionChanged;
    private bool _suppressCategorySelectionChanged;
    private bool _suppressStatusSelectionChanged;
    private bool _suppressSavingDuringSelection;
    private bool _isUpdatingDateFields;
    private int _selectionNavigationDepth;
    private string? _searchSelectedTaskId;
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
    private CategoryItem? _categoryDragCandidate;
    private PointerPressedEventArgs? _categoryDragStartEvent;
    private Point _categoryDragStartPoint;
    private bool _isDraggingCategory;
    private CategoryItem? _categoryDropTarget;
    private bool _isCategoryRootDropTarget;
    private bool _deskInitialViewApplied;
    private bool _startupWindowBoundsApplied;
    private double _deskFitZoom = 1.0;
    private double _deskUserZoom = 1.0;
    private const double StartupWindowTargetMinWidth = 1500;
    private const double StartupWindowTargetMaxWidth = 1600;
    private const double StartupWindowTargetMinHeight = 900;
    private const double StartupWindowTargetMaxHeight = 950;
    private const double StartupWindowPreferredWidthFactor = 0.94;
    private const double StartupWindowPreferredHeightFactor = 0.88;
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
    private static readonly TimeSpan AutoSaveInterval = TimeSpan.FromMinutes(5);
    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = AutoSaveInterval };
    private readonly Dictionary<string, TaskItem> _dirtyTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CategoryItem> _dirtyCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MaterialItem> _dirtyMaterials = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AttachmentItem> _dirtyAttachments = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeskItem> _dirtyDeskItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deletedMaterials = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deletedAttachments = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deletedDeskItems = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasDirtyData;
    private bool _isSavingDirtyData;
    private bool _suppressRepositoryExportsDuringSave;
    private bool _repositoryDataWrittenDuringSave;
    private bool _emptyTrashRequested;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<CategoryItem> SidebarCategories { get; } = new();
    public ObservableCollection<CategoryItem> TaskCategories { get; } = new();
    public ObservableCollection<TaskCategorySelection> TaskCategorySelections { get; } = new();
    public ObservableCollection<TaskItem> AllTasks { get; } = new();
    public ObservableCollection<TaskItem> VisibleTasks { get; } = new();
    public ObservableCollection<TaskSearchResult> GlobalSearchResults { get; } = new();
    public ObservableCollection<MaterialItem> Materials { get; } = new();
    public ObservableCollection<AttachmentItem> Attachments { get; } = new();
    public ObservableCollection<MobileInboxEntry> MobileInboxEntries { get; } = new();
    public ObservableCollection<MobileInboxPreviewItem> MobileInboxPhotoPreviews { get; } = new();
    public ObservableCollection<MobileInboxPreviewItem> MobileInboxSketchPreviews { get; } = new();
    public ObservableCollection<MobileInboxPreviewItem> MobileInboxFilePreviews { get; } = new();
    public ObservableCollection<DashboardSection> DashboardSections { get; } = new();
    public ObservableCollection<TaskItem> FollowUpTasks { get; } = new();
    public ObservableCollection<DeskItem> DeskItems { get; } = new();
    public ObservableCollection<BackupListItem> BackupEntries { get; } = new();
    public ObservableCollection<LocalNetworkRememberedDeviceListItem> LocalNetworkRememberedDevices { get; } = new();

    private DashboardSection _dashboardThisWeekSection = new(
        "Diese Woche",
        "Keine Termine für diese Woche.",
        0,
        Array.Empty<TaskItem>());

    public DashboardSection DashboardThisWeekSection
    {
        get => _dashboardThisWeekSection;
        private set
        {
            if (ReferenceEquals(_dashboardThisWeekSection, value))
            {
                return;
            }

            _dashboardThisWeekSection = value;
            OnPropertyChanged(nameof(DashboardThisWeekSection));
        }
    }

    private DashboardSection _dashboardNextWeekSection = new(
        "Nächste Woche",
        "Keine Termine für nächste Woche.",
        0,
        Array.Empty<TaskItem>());

    public DashboardSection DashboardNextWeekSection
    {
        get => _dashboardNextWeekSection;
        private set
        {
            if (ReferenceEquals(_dashboardNextWeekSection, value))
            {
                return;
            }

            _dashboardNextWeekSection = value;
            OnPropertyChanged(nameof(DashboardNextWeekSection));
        }
    }

    public string[] SortFieldOptions { get; } =
    [
        SortFieldDate,
        SortFieldName,
        SortFieldCreatedAt,
        SortFieldFollowUp,
        SortFieldSentAt,
        SortFieldUpdatedAt,
        SortFieldManual
    ];

    public string SelectedSortField
    {
        get => _selectedSortField;
        set
        {
            var normalized = NormalizeSortField(value);
            if (string.Equals(_selectedSortField, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSortField = normalized;
            OnPropertyChanged(nameof(SelectedSortField));
            RefreshVisibleTasks();
        }
    }

    public string SortDirectionGlyph => _isSortDescending ? "↓" : "↑";
    public string SortDirectionTooltip => _isSortDescending
        ? "Sortierung umkehren (aktuell absteigend)"
        : "Sortierung umkehren (aktuell aufsteigend)";

    public string[] StatusOptions { get; } = ["Offen", "Wartet auf Kunde", "Material offen", "Terminiert", "Erledigt", "Archiv"];
    public ObservableCollection<string> TechnicianOptions { get; } = new();
    public ObservableCollection<TechnicianProfile> TechnicianProfiles { get; } = new();
    private TechnicianProfile? _selectedTechnicianProfile;
    private string _technicianNameInput = string.Empty;
    private string _technicianAbbreviationInput = string.Empty;
    private string _technicianEmailInput = string.Empty;
    private string _technicianPhoneInput = string.Empty;
    private string _selectedSettingsTab = "General";

    public TechnicianProfile? SelectedTechnicianProfile
    {
        get => _selectedTechnicianProfile;
        set
        {
            if (ReferenceEquals(_selectedTechnicianProfile, value)) return;
            _selectedTechnicianProfile = value;
            LoadTechnicianEditor(value);
            OnPropertyChanged(nameof(SelectedTechnicianProfile));
            OnPropertyChanged(nameof(HasSelectedTechnicianProfile));
        }
    }

    public bool HasSelectedTechnicianProfile => SelectedTechnicianProfile is not null;

    public string TechnicianNameInput { get => _technicianNameInput; set => SetTechnicianEditorValue(ref _technicianNameInput, value, nameof(TechnicianNameInput)); }
    public string TechnicianAbbreviationInput { get => _technicianAbbreviationInput; set => SetTechnicianEditorValue(ref _technicianAbbreviationInput, value, nameof(TechnicianAbbreviationInput)); }
    public string TechnicianEmailInput { get => _technicianEmailInput; set => SetTechnicianEditorValue(ref _technicianEmailInput, value, nameof(TechnicianEmailInput)); }
    public string TechnicianPhoneInput { get => _technicianPhoneInput; set => SetTechnicianEditorValue(ref _technicianPhoneInput, value, nameof(TechnicianPhoneInput)); }
    public bool IsSettingsGeneralTabSelected => _selectedSettingsTab == "General";
    public bool IsSettingsOrdersTabSelected => _selectedSettingsTab == "Orders";
    public bool IsSettingsCategoriesTabSelected => _selectedSettingsTab == "Categories";
    public bool IsSettingsTechniciansTabSelected => _selectedSettingsTab == "Technicians";
    public bool IsSettingsDisplayTabSelected => _selectedSettingsTab == "Display";
    public bool IsSettingsDataTabSelected => _selectedSettingsTab == "DataSync";
    public bool IsSettingsSyncTabSelected => _selectedSettingsTab == "LocalNetworkSync";
    public bool IsSettingsGeneralOrDisplayTabSelected => IsSettingsGeneralTabSelected || IsSettingsDisplayTabSelected;

    public string[] PriorityOptions { get; } = ["Niedrig", "Normal", "Hoch", "Dringend"];
    public string[] MaterialStatusOptions { get; } = ["benötigt", "bestellt", "vorhanden", "verbaut", "retour", "erledigt"];
    public string[] AppearanceModeOptions { get; } = [LightMode, DarkMode];

    public string DeskZoomLabel => $"{Math.Min(Math.Round(_deskUserZoom * 100), 300):0} %";

    public double DeskZoom => _deskFitZoom * _deskUserZoom;

    private string _autoSaveStatus = string.Empty;

    public string AutoSaveStatus
    {
        get => _autoSaveStatus;
        private set
        {
            if (_autoSaveStatus == value)
            {
                return;
            }

            _autoSaveStatus = value;
            OnPropertyChanged(nameof(AutoSaveStatus));
            OnPropertyChanged(nameof(HasAutoSaveStatus));
        }
    }

    public bool HasAutoSaveStatus => !string.IsNullOrWhiteSpace(AutoSaveStatus);

    public bool IsSelectedTaskOnDesk
    {
        get => SelectedTask is not null && FindAutomaticTaskDeskNote(SelectedTask.Id) is not null;
        set
        {
            if (SelectedTask is null || value == IsSelectedTaskOnDesk)
            {
                return;
            }

            if (value)
            {
                AddAutomaticTaskDeskNote(SelectedTask);
            }
            else if (FindAutomaticTaskDeskNote(SelectedTask.Id) is { } note)
            {
                DeleteDeskItem(note);
            }

            OnPropertyChanged(nameof(IsSelectedTaskOnDesk));
        }
    }

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
            OnPropertyChanged(nameof(IsMobileInboxSelected));
            OnPropertyChanged(nameof(IsNotTrashSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
            OnPropertyChanged(nameof(IsTaskAreaVisible));
            OnPropertyChanged(nameof(CanCreateTaskInSelectedCategory));
            OnPropertyChanged(nameof(HasNoVisibleMobileInboxEntries));
            OnPropertyChanged(nameof(IsTrashEmpty));
            CategoryEditorName = IsSettingsSelected ? string.Empty : _selectedCategory?.Name ?? string.Empty;
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
            OnPropertyChanged(nameof(HasNormalSelectedTask));
            OnPropertyChanged(nameof(IsSelectedTaskOnDesk));
            OnPropertyChanged(nameof(SelectedMobileInboxEntry));
            OnPropertyChanged(nameof(HasSelectedMobileInboxEntry));
            SelectedMobileInboxPreviewItem = null;
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

    public string DueTimeInputText
    {
        get => _dueTimeText;
        set
        {
            if (_dueTimeText != value)
            {
                _dueTimeText = value;
                OnPropertyChanged(nameof(DueTimeInputText));
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

    public string MaterialOrderedAtInputText
    {
        get => _materialOrderedAtText;
        set
        {
            if (_materialOrderedAtText != value)
            {
                _materialOrderedAtText = value;
                OnPropertyChanged(nameof(MaterialOrderedAtInputText));
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
    public bool HasNormalSelectedTask => SelectedTask is not null && !IsMobileInboxTask(SelectedTask);
    public bool HasSelectedMobileInboxEntry => SelectedMobileInboxEntry is not null;
    public bool HasVisibleTasks => VisibleTasks.Count > 0;
    public bool HasNoVisibleMobileInboxEntries =>
        IsMobileInboxSelected &&
        MobileInboxEntries.Count == 0 &&
        VisibleTasks.Count == 0 &&
        string.IsNullOrWhiteSpace(SearchText);
    public bool IsTrashEmpty => IsTrashSelected && !HasVisibleTasks;
    public bool HasTrashItems => AllTasks.Any(task => task.IsDeleted);
    public bool HasFollowUpTasks => FollowUpTasks.Count > 0;
    public int FollowUpTaskCount => FollowUpTasks.Count;
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
    public bool IsOverviewSelected => SelectedCategory?.Id == OverviewCategoryId;
    public bool IsDeskSelected => SelectedCategory?.Id == DeskCategoryId;
    public bool IsTrashSelected => SelectedCategory?.Id == TrashCategoryId;
    public bool IsMobileInboxSelected => SelectedCategory?.Id == MobileInboxCategoryId;
    public bool IsNotTrashSelected => !IsTrashSelected;
    public bool IsSettingsSelected => SelectedCategory?.Id == SettingsCategoryId;
    public bool IsTaskAreaVisible => !IsOverviewSelected && !IsDeskSelected && !IsSettingsSelected;
    public bool CanCreateTaskInSelectedCategory => IsTaskAreaVisible &&
                                                   !IsTrashSelected &&
                                                   !IsMobileInboxSelected &&
                                                   SelectedCategory is not null &&
                                                   IsSelectableTaskCategory(SelectedCategory);
    public bool HasArchiveCategory => Categories.Any(IsArchiveCategory);
    public bool IsCategoryRootDropTarget => _isCategoryRootDropTarget;
    public IBrush CategoryRootDropBackground => _isCategoryRootDropTarget
        ? ResourceBrush("AccentSoftBrush")
        : Brushes.Transparent;
    public IBrush CategoryRootDropBorderBrush => _isCategoryRootDropTarget
        ? ResourceBrush("AccentBrush")
        : Brushes.Transparent;
    public MobileInboxEntry? SelectedMobileInboxEntry =>
        SelectedTask is null ? null : GetMobileInboxEntry(SelectedTask);
    public bool HasMobileInboxPhotoPreviews => MobileInboxPhotoPreviews.Count > 0;
    public bool HasMobileInboxSketchPreviews => MobileInboxSketchPreviews.Count > 0;
    public bool HasMobileInboxFilePreviews => MobileInboxFilePreviews.Count > 0;
    public bool HasNoMobileInboxPhotoPreviews => !HasMobileInboxPhotoPreviews;
    public bool HasNoMobileInboxSketchPreviews => !HasMobileInboxSketchPreviews;
    public bool HasNoMobileInboxFilePreviews => !HasMobileInboxFilePreviews;
    public MobileInboxPreviewItem? SelectedMobileInboxPreviewItem
    {
        get => _selectedMobileInboxPreviewItem;
        set
        {
            if (_selectedMobileInboxPreviewItem == value)
            {
                return;
            }

            _selectedMobileInboxPreviewItem = value;
            MobileInboxPreviewStatus = string.Empty;
            OnPropertyChanged(nameof(SelectedMobileInboxPreviewItem));
            OnPropertyChanged(nameof(HasSelectedMobileInboxPreviewItem));
            OnPropertyChanged(nameof(MobileInboxPreviewDetailPath));
            OnPropertyChanged(nameof(HasMobileInboxPreviewDetailImage));
            OnPropertyChanged(nameof(HasMobileInboxPreviewDetailMessage));
            OnPropertyChanged(nameof(MobileInboxPreviewDetailMessage));
            OnPropertyChanged(nameof(CanOpenSelectedMobileInboxPreview));
        }
    }
    public bool HasSelectedMobileInboxPreviewItem => SelectedMobileInboxPreviewItem is not null;
    public string MobileInboxPreviewDetailPath => SelectedMobileInboxPreviewItem?.EffectiveDetailPath ?? string.Empty;
    public bool HasMobileInboxPreviewDetailImage => SelectedMobileInboxPreviewItem?.HasDetailImage == true;
    public bool HasMobileInboxPreviewDetailMessage => SelectedMobileInboxPreviewItem is not null && !HasMobileInboxPreviewDetailImage;
    public string MobileInboxPreviewDetailMessage => SelectedMobileInboxPreviewItem?.DetailStatusText ?? string.Empty;
    public bool CanOpenSelectedMobileInboxPreview => SelectedMobileInboxPreviewItem?.DetailExists == true;
    public string MobileInboxPreviewStatus
    {
        get => _mobileInboxPreviewStatus;
        set
        {
            if (_mobileInboxPreviewStatus != value)
            {
                _mobileInboxPreviewStatus = value;
                OnPropertyChanged(nameof(MobileInboxPreviewStatus));
                OnPropertyChanged(nameof(HasMobileInboxPreviewStatus));
            }
        }
    }
    public bool HasMobileInboxPreviewStatus => !string.IsNullOrWhiteSpace(MobileInboxPreviewStatus);
    public string MobileInboxCleanupStatus
    {
        get => _mobileInboxCleanupStatus;
        set
        {
            if (_mobileInboxCleanupStatus != value)
            {
                _mobileInboxCleanupStatus = value;
                OnPropertyChanged(nameof(MobileInboxCleanupStatus));
                OnPropertyChanged(nameof(HasMobileInboxCleanupStatus));
            }
        }
    }
    public bool HasMobileInboxCleanupStatus => !string.IsNullOrWhiteSpace(MobileInboxCleanupStatus);
    public string DashboardDateText => $"Termine für die aktuelle und nächste Woche ab {GetWeekStart(DateTime.Today):dd.MM.yyyy}";
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

    public string LocalNetworkSyncDeviceNameInput
    {
        get => _localNetworkSyncDeviceNameInput;
        set
        {
            if (_localNetworkSyncDeviceNameInput != value)
            {
                _localNetworkSyncDeviceNameInput = value;
                OnPropertyChanged(nameof(LocalNetworkSyncDeviceNameInput));
            }
        }
    }

    public string LocalNetworkSyncPortInput
    {
        get => _localNetworkSyncPortInput;
        set
        {
            if (_localNetworkSyncPortInput != value)
            {
                _localNetworkSyncPortInput = value;
                OnPropertyChanged(nameof(LocalNetworkSyncPortInput));
            }
        }
    }

    public string LocalNetworkSyncSettingsStatus
    {
        get => _localNetworkSyncSettingsStatus;
        set
        {
            if (_localNetworkSyncSettingsStatus != value)
            {
                _localNetworkSyncSettingsStatus = value;
                OnPropertyChanged(nameof(LocalNetworkSyncSettingsStatus));
            }
        }
    }

    public string LocalNetworkSyncTestServiceStatus
    {
        get => _localNetworkSyncTestServiceStatus;
        set
        {
            if (_localNetworkSyncTestServiceStatus != value)
            {
                _localNetworkSyncTestServiceStatus = value;
                OnPropertyChanged(nameof(LocalNetworkSyncTestServiceStatus));
            }
        }
    }

    public string AppearanceMode
    {
        get => _appearanceMode;
        set => SetAppearanceMode(value, persist: true);
    }

    public bool ShowDesktopSetting
    {
        get => _appSettings.ShowDesktop;
        set
        {
            if (_appSettings.ShowDesktop == value)
            {
                return;
            }

            _appSettings.ShowDesktop = value;
            _settingsService.Save(_appSettings);

            if (!value && IsDeskSelected)
            {
                SelectedCategory = Categories.FirstOrDefault(category => category.Id == OverviewCategoryId);
            }

            var categoryToPreserve = SelectedCategory;
            var wasSuppressingCategorySelectionChanged = _suppressCategorySelectionChanged;
            _suppressCategorySelectionChanged = true;
            try
            {
                RefreshCategoryDependentViews();
                if (categoryToPreserve is not null)
                {
                    SelectedCategory = categoryToPreserve;
                    if (CategoryList is not null)
                    {
                        CategoryList.SelectedItem = categoryToPreserve;
                    }
                }
            }
            finally
            {
                _suppressCategorySelectionChanged = wasSuppressingCategorySelectionChanged;
            }

            ApplySelectedCategoryContent();
            OnPropertyChanged(nameof(ShowDesktopSetting));
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
    public string OneDriveEditDirectory => ResolveOneDriveEditDirectory(_appSettings.OneDriveEditDirectory);
    public bool HasOneDriveEditDirectory => !string.IsNullOrWhiteSpace(OneDriveEditDirectory);
    public bool HasNoOneDriveEditDirectory => !HasOneDriveEditDirectory;
    public string IpadSyncRootDirectory => HasOneDriveEditDirectory
        ? ResolveDisplayDirectory(IpadSnapshotExportService.ResolveSyncRootDirectory(OneDriveEditDirectory))
        : string.Empty;
    public bool HasIpadSyncRootDirectory => !string.IsNullOrWhiteSpace(IpadSyncRootDirectory);
    public bool HasNoIpadSyncRootDirectory => !HasIpadSyncRootDirectory;
    public string IpadLiveFileTargetFolder => ResolveIpadLiveFileTargetFolder(_appSettings.IpadLiveFileTargetPath);
    public string IpadLiveFileTargetPath => BuildIpadLiveFileTargetPath(IpadLiveFileTargetFolder);
    public bool HasIpadLiveFileTargetPath => !string.IsNullOrWhiteSpace(IpadLiveFileTargetPath);
    public bool HasNoIpadLiveFileTargetPath => !HasIpadLiveFileTargetPath;
    public string LocalNetworkSyncStatusText => "Lokaler Netzwerk-Sync vorbereitet. Testdienst startet nur manuell.";
    public string LocalNetworkSyncBonjourStatusText => LocalBonjourService.GetAvailabilityStatus().DisplayText;
    public string LocalNetworkSyncDeviceNameText => string.IsNullOrWhiteSpace(_appSettings.LocalNetworkSyncDeviceName)
        ? "nicht festgelegt"
        : _appSettings.LocalNetworkSyncDeviceName.Trim();
    public string LocalNetworkSyncPortText => _appSettings.LocalNetworkSyncPort > 0
        ? _appSettings.LocalNetworkSyncPort.ToString(CultureInfo.InvariantCulture)
        : "nicht festgelegt";
    public string LocalNetworkSyncTestServiceAddressText => BuildLocalNetworkSyncTestServiceAddressText();
    public string LocalNetworkSyncHintText => "Das iPad findet diesen Desktop automatisch per Bonjour, falls verfügbar. Auf Windows ist Bonjour/mDNS dafür erforderlich; ohne Bonjour die LAN-Adresse manuell auf dem iPad eintragen. Der Testdienst liefert nur Statusantworten; Sync und Datenübertragung bleiben deaktiviert.";
    public bool HasLocalNetworkRememberedDevices => LocalNetworkRememberedDevices.Count > 0;
    public bool HasNoLocalNetworkRememberedDevices => !HasLocalNetworkRememberedDevices;
    public bool IsSettingsGeneralOpen => _isSettingsGeneralOpen;
    public bool IsSettingsDataSyncOpen => _isSettingsDataSyncOpen;
    public bool IsSettingsLocalNetworkSyncOpen => _isSettingsLocalNetworkSyncOpen;
    public bool IsSettingsTechniciansOpen => _isSettingsTechniciansOpen;
    public bool IsSettingsCategoriesOpen => _isSettingsCategoriesOpen;
    public bool IsSettingsOrdersOpen => _isSettingsOrdersOpen;
    public bool IsSettingsUpdatesOpen => _isSettingsUpdatesOpen;
    public bool IsSettingsDiagnosticsOpen => _isSettingsDiagnosticsOpen;
    public string SettingsGeneralToggleText => IsSettingsGeneralOpen ? "-" : "+";
    public string SettingsDataSyncToggleText => IsSettingsDataSyncOpen ? "-" : "+";
    public string SettingsLocalNetworkSyncToggleText => IsSettingsLocalNetworkSyncOpen ? "-" : "+";
    public string SettingsTechniciansToggleText => IsSettingsTechniciansOpen ? "-" : "+";
    public string SettingsCategoriesToggleText => IsSettingsCategoriesOpen ? "-" : "+";
    public string SettingsOrdersToggleText => IsSettingsOrdersOpen ? "-" : "+";
    public string SettingsUpdatesToggleText => IsSettingsUpdatesOpen ? "-" : "+";
    public string SettingsDiagnosticsToggleText => IsSettingsDiagnosticsOpen ? "-" : "+";
    public string IpadLiveFileLastSuccessfulExport
    {
        get => _ipadLiveFileLastSuccessfulExport;
        set
        {
            if (_ipadLiveFileLastSuccessfulExport != value)
            {
                _ipadLiveFileLastSuccessfulExport = value;
                OnPropertyChanged(nameof(IpadLiveFileLastSuccessfulExport));
                OnPropertyChanged(nameof(HasIpadLiveFileLastSuccessfulExport));
            }
        }
    }

    public bool HasIpadLiveFileLastSuccessfulExport => !string.IsNullOrWhiteSpace(IpadLiveFileLastSuccessfulExport);
    public string IpadLiveFileLastError
    {
        get => _ipadLiveFileLastError;
        set
        {
            if (_ipadLiveFileLastError != value)
            {
                _ipadLiveFileLastError = value;
                OnPropertyChanged(nameof(IpadLiveFileLastError));
                OnPropertyChanged(nameof(HasIpadLiveFileLastError));
            }
        }
    }

    public bool HasIpadLiveFileLastError => !string.IsNullOrWhiteSpace(IpadLiveFileLastError);
    public string IpadLiveFileStatus
    {
        get => _ipadLiveFileStatus;
        set
        {
            if (_ipadLiveFileStatus != value)
            {
                _ipadLiveFileStatus = value;
                OnPropertyChanged(nameof(IpadLiveFileStatus));
            }
        }
    }

    public string IpadSnapshotStatus
    {
        get => _ipadSnapshotStatus;
        set
        {
            if (_ipadSnapshotStatus != value)
            {
                _ipadSnapshotStatus = value;
                OnPropertyChanged(nameof(IpadSnapshotStatus));
                OnPropertyChanged(nameof(HasIpadSnapshotStatus));
                OnPropertyChanged(nameof(HasSuccessfulIpadSnapshotStatus));
                OnPropertyChanged(nameof(HasFailedIpadSnapshotStatus));
            }
        }
    }

    public bool HasIpadSnapshotStatus => !string.IsNullOrWhiteSpace(IpadSnapshotStatus);
    public bool HasSuccessfulIpadSnapshotStatus =>
        HasIpadSnapshotStatus &&
        !IsIpadSnapshotExportRunning &&
        IpadSnapshotStatus.Contains("erfolgreich", StringComparison.OrdinalIgnoreCase);
    public bool HasFailedIpadSnapshotStatus =>
        HasIpadSnapshotStatus &&
        !IsIpadSnapshotExportRunning &&
        !HasSuccessfulIpadSnapshotStatus;
    public bool IsIpadSnapshotExportRunning
    {
        get => _isIpadSnapshotExportRunning;
        private set
        {
            if (_isIpadSnapshotExportRunning != value)
            {
                _isIpadSnapshotExportRunning = value;
                OnPropertyChanged(nameof(IsIpadSnapshotExportRunning));
                OnPropertyChanged(nameof(HasSuccessfulIpadSnapshotStatus));
                OnPropertyChanged(nameof(HasFailedIpadSnapshotStatus));
            }
        }
    }
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

            var selectedTaskIdBeforeSearchReset = !string.IsNullOrWhiteSpace(_searchText) &&
                                                  string.IsNullOrWhiteSpace(value)
                ? _searchSelectedTaskId ?? SelectedTask?.Id
                : null;

            if (string.IsNullOrWhiteSpace(_searchText) && !string.IsNullOrWhiteSpace(value))
            {
                _searchSelectedTaskId = null;
            }

            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            RefreshVisibleTasks();

            if (!string.IsNullOrWhiteSpace(selectedTaskIdBeforeSearchReset))
            {
                var visibleSelectedTask = VisibleTasks.FirstOrDefault(task => task.Id == selectedTaskIdBeforeSearchReset);
                if (visibleSelectedTask is not null)
                {
                    SetSelectedTaskDuringRefresh(visibleSelectedTask);
                }

                _searchSelectedTaskId = null;
            }

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

    public bool IncludeArchiveInSearch
    {
        get => _includeArchiveInSearch;
        set
        {
            if (_includeArchiveInSearch == value)
            {
                return;
            }

            _includeArchiveInSearch = value;
            OnPropertyChanged(nameof(IncludeArchiveInSearch));
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
    public bool HasBackupEntries => BackupEntries.Count > 0;
    public bool HasNoBackupEntries => !HasBackupEntries;
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
        _liveSettingsService = new LiveSettingsService();
        _hashService = new FileHashService();
        _ipadSnapshotExportService = new IpadSnapshotExportService();

        _appSettings = _settingsService.Load();
        EnsureLocalNetworkSyncLocalSettings();
        EnsureLocalNetworkSyncDefaultPort();
        LoadLocalNetworkRememberedDevices();
        RefreshLocalNetworkSyncEditorFields();
        NormalizeConfiguredOneDriveEditDirectory();
        LoadTechnicianOptions();
        SetAppearanceMode(_appSettings.AppearanceMode, persist: false);
        InitializeComponent();
        _autoSaveTimer.Tick += AutoSaveTimer_OnTick;
        if (!lockResult.IsAcquired)
        {
            Title = "BüroCockpit - Datenordner-Warnung";
        }

        Closing += MainWindow_OnClosing;
        Closed += (_, _) =>
        {
            _autoSaveTimer.Stop();
            StopLocalNetworkSyncTestService();
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

        try
        {
            _repository.Initialize();
        }
        catch (DatabaseStartupException ex)
        {
            HandleStartupDatabaseError(ex.DiagnosticMessage, ex);
            return;
        }

        _repository.DataWritten += Repository_OnDataWritten;
        LoadData();
        LoadBackupEntries();
        CleanupNavigationCategories();
        SelectStartupTaskCategory();
    }

    protected override void OnOpened(EventArgs e)
    {
        ApplyResponsiveStartupBounds();
        base.OnOpened(e);
    }

    private async void AutoSaveTimer_OnTick(object? sender, EventArgs e)
    {
        await SaveNowAsync("auto-save");
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _autoSaveTimer.Stop();
        SaveCurrentMaterials();
        if (!SaveNow("app-closing"))
        {
            e.Cancel = true;
        }
    }

    private void MarkDataDirty(string reason)
    {
        if (_isLoadingData || _isLoadingSelection)
        {
            return;
        }

        _hasDirtyData = true;
        AutoSaveStatus = "Ungespeicherte Änderungen";
        Console.WriteLine($"MarkDataDirty: {DateTime.Now:O} reason={reason}");
        if (!_autoSaveTimer.IsEnabled)
        {
            _autoSaveTimer.Start();
        }
    }

    private void MarkTaskDirty(TaskItem? task)
    {
        if (task is null || task.IsMobileInboxCard)
        {
            return;
        }

        _dirtyTasks[task.Id] = task;
        MarkDataDirty($"task:{task.Id}");
    }

    private void MarkCategoryDirty(CategoryItem? category, string? reason = null)
    {
        if (category is null || IsSpecialCategory(category))
        {
            return;
        }

        _dirtyCategories[category.Id] = category;
        MarkDataDirty(string.IsNullOrWhiteSpace(reason) ? $"category:{category.Id}" : $"{reason}:{category.Id}");
    }

    private void MarkMaterialDirty(MaterialItem? material)
    {
        if (material is null || string.IsNullOrWhiteSpace(material.TaskId))
        {
            return;
        }

        _deletedMaterials.Remove(material.Id);
        _dirtyMaterials[material.Id] = material;
        MarkDataDirty($"material:{material.Id}");
    }

    private void MarkAttachmentDirty(AttachmentItem? attachment)
    {
        if (attachment is null || string.IsNullOrWhiteSpace(attachment.Id))
        {
            return;
        }

        _deletedAttachments.Remove(attachment.Id);
        _dirtyAttachments[attachment.Id] = attachment;
        MarkDataDirty($"attachment:{attachment.Id}");
    }

    private void MarkDeskItemDirty(DeskItem? deskItem)
    {
        if (deskItem is null || string.IsNullOrWhiteSpace(deskItem.Id))
        {
            return;
        }

        _deletedDeskItems.Remove(deskItem.Id);
        _dirtyDeskItems[deskItem.Id] = deskItem;
        MarkDataDirty($"desk-item:{deskItem.Id}");
    }

    private Task SaveNowAsync(string reason)
    {
        SaveNow(reason);
        return Task.CompletedTask;
    }

    private bool SaveNow(string reason)
    {
        if (_isSavingDirtyData)
        {
            return true;
        }

        if (!_hasDirtyData &&
            _dirtyTasks.Count == 0 &&
            _dirtyCategories.Count == 0 &&
            _dirtyMaterials.Count == 0 &&
            _dirtyAttachments.Count == 0 &&
            _dirtyDeskItems.Count == 0 &&
            _deletedMaterials.Count == 0 &&
            _deletedAttachments.Count == 0 &&
            _deletedDeskItems.Count == 0 &&
            !_emptyTrashRequested)
        {
            _autoSaveTimer.Stop();
            return true;
        }

        Console.WriteLine($"SaveNow: {DateTime.Now:O} reason={reason}");
        LogSaveNowIpadSyncTarget(reason);
        _isSavingDirtyData = true;
        _suppressRepositoryExportsDuringSave = true;
        _repositoryDataWrittenDuringSave = false;
        try
        {
            if (_emptyTrashRequested)
            {
                _repository.EmptyTrash();
            }

            foreach (var materialId in _deletedMaterials.ToList())
            {
                _dirtyMaterials.Remove(materialId);
                _repository.DeleteMaterial(materialId);
            }

            foreach (var attachmentId in _deletedAttachments.ToList())
            {
                _dirtyAttachments.Remove(attachmentId);
                _repository.DeleteAttachment(attachmentId);
            }

            foreach (var deskItemId in _deletedDeskItems.ToList())
            {
                _dirtyDeskItems.Remove(deskItemId);
                _repository.DeleteDeskItem(deskItemId);
            }

            foreach (var category in _dirtyCategories.Values.ToList())
            {
                _repository.SaveCategory(category);
            }

            foreach (var task in _dirtyTasks.Values.ToList())
            {
                _repository.SaveTask(task);
            }

            foreach (var material in _dirtyMaterials.Values.ToList())
            {
                _repository.SaveMaterial(material);
            }

            foreach (var attachment in _dirtyAttachments.Values.ToList())
            {
                _repository.SaveAttachment(attachment);
            }

            foreach (var deskItem in _dirtyDeskItems.Values.ToList())
            {
                _repository.SaveDeskItem(deskItem);
            }

            _localNetworkSyncTestService?.MarkLocalChange();

            _dirtyCategories.Clear();
            _dirtyTasks.Clear();
            _dirtyMaterials.Clear();
            _dirtyAttachments.Clear();
            _dirtyDeskItems.Clear();
            _deletedMaterials.Clear();
            _deletedAttachments.Clear();
            _deletedDeskItems.Clear();
            _emptyTrashRequested = false;
            _hasDirtyData = false;
            _autoSaveTimer.Stop();
            AutoSaveStatus = "Gespeichert";
            TriggerRepositoryExportsAfterSave(reason);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save failed ({reason}): {ex}");
            AutoSaveStatus = "Speichern fehlgeschlagen. Die Änderung bleibt vorgemerkt und wird erneut versucht.";
            return false;
        }
        finally
        {
            _suppressRepositoryExportsDuringSave = false;
            _isSavingDirtyData = false;
        }
    }

    private void TriggerRepositoryExportsAfterSave(string reason)
    {
        if (!_repositoryDataWrittenDuringSave)
        {
            return;
        }

        var exportReason = $"save:{reason}";
        try
        {
            if (ShouldWaitForRepositoryExports(reason))
            {
                ExportIpadTargetsNow(exportReason);
            }
            else
            {
                TriggerIpadSnapshotExport(exportReason);
                TriggerIpadLiveFileExport(exportReason);
            }
        }
        finally
        {
            _repositoryDataWrittenDuringSave = false;
        }
    }

    private static bool ShouldWaitForRepositoryExports(string reason)
    {
        return string.Equals(reason, "app-closing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reason, "manual-save", StringComparison.OrdinalIgnoreCase);
    }

    private void ExportIpadTargetsNow(string reason)
    {
        _ipadSnapshotExportService.LogDiagnostic($"Legacy iPad file export disabled ({reason}): local network sync is the target path.");
    }

    private void LogSaveNowIpadSyncTarget(string reason)
    {
        var oneDriveEditDirectory = OneDriveEditDirectory;
        if (string.IsNullOrWhiteSpace(oneDriveEditDirectory))
        {
            Console.WriteLine($"SaveNow target missing: {DateTime.Now:O} reason={reason} syncRoot=<not configured>");
            _ipadSnapshotExportService.LogDiagnostic($"SaveNow target missing ({reason}): no OneDrive edit directory configured.");
            return;
        }

        var syncRoot = IpadSnapshotExportService.ResolveSyncRootDirectory(oneDriveEditDirectory);
        var liveFile = IpadSnapshotExportService.ResolveLivePackagePath(oneDriveEditDirectory);
        if (string.IsNullOrWhiteSpace(syncRoot) || string.IsNullOrWhiteSpace(liveFile))
        {
            Console.WriteLine($"SaveNow target invalid: {DateTime.Now:O} reason={reason} oneDriveEditDirectory={oneDriveEditDirectory}");
            _ipadSnapshotExportService.LogDiagnostic($"SaveNow target invalid ({reason}): oneDriveEditDirectory={oneDriveEditDirectory}");
            return;
        }

        Console.WriteLine($"SaveNow target syncRoot={syncRoot}");
        Console.WriteLine($"SaveNow target liveFile={liveFile}");
        _ipadSnapshotExportService.LogDiagnostic($"SaveNow target ({reason}) syncRoot={syncRoot}");
        _ipadSnapshotExportService.LogDiagnostic($"SaveNow target ({reason}) liveFile={liveFile}");
    }

    private void SubscribeCategoryItem(CategoryItem category)
    {
        category.PropertyChanged -= CategoryItem_OnPropertyChanged;
        category.PropertyChanged += CategoryItem_OnPropertyChanged;
    }

    private void SubscribeTaskItem(TaskItem task)
    {
        task.PropertyChanged -= TaskItem_OnPropertyChanged;
        task.PropertyChanged += TaskItem_OnPropertyChanged;
    }

    private void SubscribeMaterialItem(MaterialItem material)
    {
        material.PropertyChanged -= MaterialItem_OnPropertyChanged;
        material.PropertyChanged += MaterialItem_OnPropertyChanged;
    }

    private void CategoryItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not CategoryItem category || !IsCategoryDataProperty(e.PropertyName))
        {
            return;
        }

        MarkCategoryDirty(category);
    }

    private void TaskItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TaskItem task || !IsTaskDataProperty(e.PropertyName))
        {
            return;
        }

        MarkTaskDirty(task);
    }

    private void MaterialItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MaterialItem material || !IsMaterialDataProperty(e.PropertyName))
        {
            return;
        }

        MarkMaterialDirty(material);
    }

    private static bool IsCategoryDataProperty(string? propertyName)
    {
        return propertyName is
            nameof(CategoryItem.Name) or
            nameof(CategoryItem.ParentId) or
            nameof(CategoryItem.SortOrder) or
            nameof(CategoryItem.Color) or
            nameof(CategoryItem.IsVisible);
    }

    private static bool IsTaskDataProperty(string? propertyName)
    {
        return propertyName is
            nameof(TaskItem.Title) or
            nameof(TaskItem.CustomerName) or
            nameof(TaskItem.CustomerAddress) or
            nameof(TaskItem.CustomerEmail) or
            nameof(TaskItem.CustomerPhone) or
            nameof(TaskItem.Description) or
            nameof(TaskItem.CategoryId) or
            nameof(TaskItem.Status) or
            nameof(TaskItem.Priority) or
            nameof(TaskItem.DueDate) or
            nameof(TaskItem.FollowUpDate) or
            nameof(TaskItem.SentAt) or
            nameof(TaskItem.MaterialOrderedAt) or
            nameof(TaskItem.AssignedTo) or
            nameof(TaskItem.Technician) or
            nameof(TaskItem.CompletedAt) or
            nameof(TaskItem.IsDeleted) or
            nameof(TaskItem.DeletedAt) or
            nameof(TaskItem.SortPosition);
    }

    private static bool IsMaterialDataProperty(string? propertyName)
    {
        return propertyName is
            nameof(MaterialItem.Quantity) or
            nameof(MaterialItem.Unit) or
            nameof(MaterialItem.Name) or
            nameof(MaterialItem.Status) or
            nameof(MaterialItem.Supplier) or
            nameof(MaterialItem.OrderedAt) or
            nameof(MaterialItem.Note);
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

    internal static IBrush ResourceBrush(string key)
    {
        return Application.Current?.Resources[key] as IBrush ?? Brushes.Transparent;
    }

    private void CleanupNavigationCategories()
    {
        var categoriesToRemove = Categories
            .Where(category =>
                !string.Equals(category.Id, OverviewCategoryId, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(category.Name, OverviewCategoryName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category.Name, "Übersicht", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category.Name, "Dashboard", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(category.Name, "Schreibtisch", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(category.Id, DeskCategoryId, StringComparison.OrdinalIgnoreCase))))
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
        if (!string.IsNullOrWhiteSpace(_startupDatabaseErrorMessage))
        {
            await ShowStartupDatabaseErrorDialogAsync(_startupDatabaseErrorMessage);
            return;
        }

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

    private void HandleStartupDatabaseError(string diagnosticMessage, Exception exception)
    {
        _startupDatabaseErrorMessage = diagnosticMessage;
        Title = "BüroCockpit - Datenbankfehler";
        StorageLocationStatus = "Datenbank konnte nicht geöffnet werden. Details siehe Fehlermeldung.";
        Debug.WriteLine($"Database startup failed: {exception}");
    }

    private async Task ShowStartupDatabaseErrorDialogAsync(string diagnosticMessage)
    {
        var okAction = CreateStartupDatabaseDialogAction("OK");
        var dialog = new Window
        {
            Title = "Datenbank konnte nicht geöffnet werden",
            Width = 720,
            Height = 560,
            MinWidth = 560,
            MinHeight = 420,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        new StackPanel
                        {
                            [DockPanel.DockProperty] = Dock.Top,
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "BüroCockpit konnte die Datenbank nicht öffnen.",
                                    FontSize = 18,
                                    FontWeight = FontWeight.Bold,
                                    Foreground = ResourceBrush("TextPrimaryBrush"),
                                    TextWrapping = TextWrapping.Wrap
                                },
                                new TextBlock
                                {
                                    Text = "Der Datenordner muss lokal verfügbar und beschreibbar sein. Es wurde keine Reparatur, Kopie oder Migration ausgeführt.",
                                    FontSize = 13,
                                    Foreground = ResourceBrush("TextSecondaryBrush"),
                                    TextWrapping = TextWrapping.Wrap
                                }
                            }
                        },
                        new Border
                        {
                            [DockPanel.DockProperty] = Dock.Bottom,
                            Margin = new Thickness(0, 12, 0, 0),
                            Child = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Children =
                                {
                                    okAction
                                }
                            }
                        },
                        new ScrollViewer
                        {
                            Margin = new Thickness(0, 12, 0, 0),
                            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            Content = new TextBlock
                            {
                                Text = diagnosticMessage,
                                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                                FontSize = 12,
                                Foreground = ResourceBrush("TextPrimaryBrush"),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            }
        };

        okAction.PointerReleased += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private static Border CreateStartupDatabaseDialogAction(string text)
    {
        var normalBackground = ResourceBrush("AccentBrush");
        var hoverBackground = ResourceBrush("AccentHoverBrush");
        var border = new Border
        {
            Background = normalBackground,
            BorderBrush = ResourceBrush("AccentBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 8),
            Child = new TextBlock
            {
                Text = text,
                Foreground = ResourceBrush("TextOnAccentBrush"),
                FontSize = 13,
                FontWeight = FontWeight.SemiBold
            }
        };

        border.PointerEntered += (_, _) => border.Background = hoverBackground;
        border.PointerExited += (_, _) => border.Background = normalBackground;
        return border;
    }

    private void ApplyResponsiveStartupBounds()
    {
        if (_startupWindowBoundsApplied)
        {
            return;
        }

        var screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen is null)
        {
            Dispatcher.UIThread.Post(ApplyResponsiveStartupBounds, DispatcherPriority.Loaded);
            return;
        }

        var desktopScaling = DesktopScaling;
        if (desktopScaling <= 0)
        {
            desktopScaling = 1.0;
        }

        var workingArea = screen.WorkingArea;
        var workingAreaLogical = workingArea.ToRect(desktopScaling);
        var desiredWidth = GetResponsiveStartupWidth(workingAreaLogical.Width);
        var desiredHeight = GetResponsiveStartupHeight(workingAreaLogical.Height);

        Width = desiredWidth;
        Height = desiredHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var desiredWidthPixels = (int)Math.Round(desiredWidth * desktopScaling);
        var desiredHeightPixels = (int)Math.Round(desiredHeight * desktopScaling);
        var left = workingArea.X + Math.Max(0, (workingArea.Width - desiredWidthPixels) / 2);
        var top = workingArea.Y + Math.Max(0, (workingArea.Height - desiredHeightPixels) / 2);
        Position = new PixelPoint(left, top);

        _startupWindowBoundsApplied = true;
    }

    private static double GetResponsiveStartupWidth(double workingAreaWidth)
    {
        if (workingAreaWidth <= 0)
        {
            return 1280;
        }

        if (workingAreaWidth < StartupWindowTargetMinWidth)
        {
            return workingAreaWidth;
        }

        var preferredWidth = workingAreaWidth * StartupWindowPreferredWidthFactor;
        return Math.Min(workingAreaWidth, Math.Clamp(preferredWidth, StartupWindowTargetMinWidth, StartupWindowTargetMaxWidth));
    }

    private static double GetResponsiveStartupHeight(double workingAreaHeight)
    {
        if (workingAreaHeight <= 0)
        {
            return 780;
        }

        if (workingAreaHeight < StartupWindowTargetMinHeight)
        {
            return workingAreaHeight;
        }

        var preferredHeight = workingAreaHeight * StartupWindowPreferredHeightFactor;
        return Math.Min(workingAreaHeight, Math.Clamp(preferredHeight, StartupWindowTargetMinHeight, StartupWindowTargetMaxHeight));
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
        var zoomFactor = e.Delta.Y > 0 ? 1.08 : 1 / 1.08;
        SetDeskUserZoom(_deskUserZoom * zoomFactor, pointerPosition);
        e.Handled = true;
    }

    private void DeskZoomIn_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDeskUserZoom(_deskUserZoom * 1.08, GetDeskViewportCenter());
    }

    private void DeskZoomOut_OnClick(object? sender, RoutedEventArgs e)
    {
        SetDeskUserZoom(_deskUserZoom / 1.08, GetDeskViewportCenter());
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
        MarkDeskItemDirty(deskItem);
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
                MarkDeskItemDirty(deskItem);
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
            MarkDeskItemDirty(deskItem);
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

        MarkDeskItemDirty(_draggedDeskItem);
        UpdateDeskSurfaceBounds();
    }


    private void LoadData(string? selectedCategoryId = null, string? selectedTaskId = null)
    {
        _isLoadingData = true;
        try
        {
            Categories.Clear();
            Categories.Add(CreateOverviewCategory());
            Categories.Add(CreateDeskCategory());
            foreach (var category in _repository.GetCategories())
            {
                if (IsSpecialCategory(category) ||
                    string.Equals(category.Name, "Dashboard", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(category.Name, "Schreibtisch", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(category.Id, DeskCategoryId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                category.SortMode = NormalizeCategorySortMode(category.SortMode);
                SubscribeCategoryItem(category);
                Categories.Add(category);
            }
            Categories.Add(CreateTrashCategory());
            Categories.Add(CreateSettingsCategory());
            RebuildCategoryTreeViews();
            RefreshTaskCategories();

            AllTasks.Clear();
            foreach (var task in _repository.GetTasks())
            {
                SubscribeTaskItem(task);
                AllTasks.Add(task);
            }
            LoadMobileInboxEntries();

            LoadDeskItems();
            UpdateCategoryCounts();
            SelectedCategory = !string.IsNullOrWhiteSpace(selectedCategoryId)
                ? Categories.FirstOrDefault(c => string.Equals(c.Id, selectedCategoryId, StringComparison.OrdinalIgnoreCase))
                : null;
            if (SelectedCategory?.Id == DeskCategoryId && !ShowDesktopSetting)
            {
                SelectedCategory = null;
            }

            SelectedCategory ??= ShowDesktopSetting
                ? Categories.FirstOrDefault(c => c.Id == DeskCategoryId)
                : null;
            SelectedCategory ??= Categories.FirstOrDefault(c => c.Id == OverviewCategoryId)
                ?? Categories.FirstOrDefault();
            ApplySelectedCategoryContent();

            if (!string.IsNullOrWhiteSpace(selectedTaskId) &&
                SelectedCategory is not null &&
                !IsSpecialCategory(SelectedCategory))
            {
                var selectedTask = AllTasks.FirstOrDefault(task => string.Equals(task.Id, selectedTaskId, StringComparison.OrdinalIgnoreCase));
                if (selectedTask is not null)
                {
                    SelectCategoryAndTask(SelectedCategory, selectedTask);
                }
            }
        }
        finally
        {
            _isLoadingData = false;
        }
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

            if (IsMobileInboxSelected)
            {
                tasks = GetMobileInboxTasks(SearchText);
            }
            else if (string.IsNullOrWhiteSpace(SearchText))
            {
                tasks = selected is null
                    ? AllTasks.Where(t => !t.IsDeleted)
                    : IsTrashSelected
                        ? SortTrashTasks(AllTasks.Where(t => t.IsDeleted))
                        : SortTasksForCategory(AllTasks.Where(t => !t.IsDeleted && TaskBelongsToSelectedCategory(t, selected)));
            }
            else
            {
                var query = SearchText.Trim();
                tasks = SortTasksForCategory(GetSearchMatches(query));
            }

            foreach (var task in tasks)
            {
                UpdateTaskCategoryPresentation(task);
                VisibleTasks.Add(task);
            }

            TaskListCaption = string.IsNullOrWhiteSpace(SearchText)
                ? IsMobileInboxSelected
                    ? (VisibleTasks.Count == 1 ? "1 mobiler Eingang" : $"{VisibleTasks.Count} mobile Eingänge")
                    : IsTrashSelected
                    ? (VisibleTasks.Count == 1 ? "1 gelöschte Aufgabe" : $"{VisibleTasks.Count} gelöschte Aufgaben")
                    : (VisibleTasks.Count == 1 ? "1 Aufgabe" : $"{VisibleTasks.Count} Aufgaben")
                : (VisibleTasks.Count == 1 ? "1 Treffer" : $"{VisibleTasks.Count} Treffer");
            if (IsTrashSelected && VisibleTasks.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
            {
                TaskListCaption = "Papierkorb ist leer";
            }
            if (IsMobileInboxSelected && VisibleTasks.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
            {
                TaskListCaption = "Keine mobilen Eingänge";
            }
            OnPropertyChanged(nameof(HasVisibleTasks));
            OnPropertyChanged(nameof(IsTrashEmpty));
            OnPropertyChanged(nameof(HasNoVisibleMobileInboxEntries));

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
        foreach (var task in SortTasksForCategory(GetSearchMatches(query)).Take(50))
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
        var isArchived = IsArchivedForSearch(task);
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
            task.SentAt,
            isArchived);
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
            .Where(task => IsTrashSelected || IncludeArchiveInSearch || !IsArchivedForSearch(task))
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
            DueTimeInputText = FormatOptionalTime(SelectedTask?.DueDate);
            FollowUpDateInputText = FormatDateShort(SelectedTask?.FollowUpDate);
            SentAtInputText = FormatDateShort(SelectedTask?.SentAt);
            MaterialOrderedAtInputText = FormatDateShort(SelectedTask?.MaterialOrderedAt);
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
                    var existingTime = SelectedTask.DueDate?.TimeOfDay ?? TimeSpan.Zero;
                    SelectedTask.DueDate = value?.Date.Add(existingTime);
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

    private void ApplyMaterialOrderedAtText()
    {
        ApplyDateText(
            MaterialOrderedAtInputText,
            "Material bestellt am",
            () => SelectedTask?.MaterialOrderedAt,
            value =>
            {
                if (SelectedTask is not null)
                {
                    SelectedTask.MaterialOrderedAt = value;
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
        SaveTaskAndQueueIpadSnapshot(SelectedTask);
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
    }

    private static string FormatDateShort(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) ?? string.Empty;
    }

    private static string FormatOptionalTime(DateTime? value)
    {
        return value.HasValue && value.Value.TimeOfDay != TimeSpan.Zero
            ? value.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private void ApplyDueTimeText()
    {
        if (_isUpdatingDateFields || SelectedTask is null)
        {
            return;
        }

        var input = DueTimeInputText.Trim();
        if (string.IsNullOrEmpty(input))
        {
            if (SelectedTask.DueDate.HasValue && SelectedTask.DueDate.Value.TimeOfDay != TimeSpan.Zero)
            {
                CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
                SelectedTask.DueDate = SelectedTask.DueDate.Value.Date;
                SaveTaskAndQueueIpadSnapshot(SelectedTask);
            }

            DateInputMessage = string.Empty;
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        var formats = new[] { "HH:mm", "H:mm" };
        if (!DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
        {
            DateInputMessage = "Uhrzeit: Bitte als HH:MM eingeben.";
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        if (!SelectedTask.DueDate.HasValue)
        {
            DateInputMessage = "Uhrzeit: Bitte zuerst ein Termindatum eingeben.";
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        var combined = SelectedTask.DueDate.Value.Date.Add(parsedTime.TimeOfDay);
        if (SelectedTask.DueDate.Value == combined)
        {
            DateInputMessage = string.Empty;
            UpdateDateTextFieldsFromSelectedTask();
            return;
        }

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
        SelectedTask.DueDate = combined;
        SaveTaskAndQueueIpadSnapshot(SelectedTask);
        DateInputMessage = string.Empty;
        UpdateDateTextFieldsFromSelectedTask();
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
        TaskListCaption = "Anstehende Termine";
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
        SelectSettingsTab("General");
        LoadBackupEntries();
    }

    private void SettingsTab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab })
        {
            SelectSettingsTab(tab);
        }
    }

    private void SelectSettingsTab(string tab)
    {
        _selectedSettingsTab = tab;
        ResetSettingsSections();
        switch (tab)
        {
            case "General":
            case "Display":
                SetSettingsSectionOpen(ref _isSettingsGeneralOpen, true, nameof(IsSettingsGeneralOpen), nameof(SettingsGeneralToggleText));
                break;
            case "Orders":
                SetSettingsSectionOpen(ref _isSettingsOrdersOpen, true, nameof(IsSettingsOrdersOpen), nameof(SettingsOrdersToggleText));
                break;
            case "Categories":
                SetSettingsSectionOpen(ref _isSettingsCategoriesOpen, true, nameof(IsSettingsCategoriesOpen), nameof(SettingsCategoriesToggleText));
                break;
            case "Technicians":
                SetSettingsSectionOpen(ref _isSettingsTechniciansOpen, true, nameof(IsSettingsTechniciansOpen), nameof(SettingsTechniciansToggleText));
                break;
            case "DataSync":
                SetSettingsSectionOpen(ref _isSettingsDataSyncOpen, true, nameof(IsSettingsDataSyncOpen), nameof(SettingsDataSyncToggleText));
                break;
            case "LocalNetworkSync":
                SetSettingsSectionOpen(ref _isSettingsLocalNetworkSyncOpen, true, nameof(IsSettingsLocalNetworkSyncOpen), nameof(SettingsLocalNetworkSyncToggleText));
                break;
        }
        OnPropertyChanged(nameof(IsSettingsGeneralTabSelected));
        OnPropertyChanged(nameof(IsSettingsOrdersTabSelected));
        OnPropertyChanged(nameof(IsSettingsCategoriesTabSelected));
        OnPropertyChanged(nameof(IsSettingsTechniciansTabSelected));
        OnPropertyChanged(nameof(IsSettingsDisplayTabSelected));
        OnPropertyChanged(nameof(IsSettingsDataTabSelected));
        OnPropertyChanged(nameof(IsSettingsSyncTabSelected));
        OnPropertyChanged(nameof(IsSettingsGeneralOrDisplayTabSelected));

        if (tab == "Technicians")
        {
            LoadTechnicianOptions();
        }
    }

    private void SettingsSectionHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string section)
        {
            return;
        }

        switch (section)
        {
            case "General":
                SetSettingsSectionOpen(ref _isSettingsGeneralOpen, !IsSettingsGeneralOpen, nameof(IsSettingsGeneralOpen), nameof(SettingsGeneralToggleText));
                break;
            case "DataSync":
                SetSettingsSectionOpen(ref _isSettingsDataSyncOpen, !IsSettingsDataSyncOpen, nameof(IsSettingsDataSyncOpen), nameof(SettingsDataSyncToggleText));
                break;
            case "LocalNetworkSync":
                SetSettingsSectionOpen(ref _isSettingsLocalNetworkSyncOpen, !IsSettingsLocalNetworkSyncOpen, nameof(IsSettingsLocalNetworkSyncOpen), nameof(SettingsLocalNetworkSyncToggleText));
                break;
            case "Technicians":
                var openTechnicians = !IsSettingsTechniciansOpen;
                SetSettingsSectionOpen(ref _isSettingsTechniciansOpen, openTechnicians, nameof(IsSettingsTechniciansOpen), nameof(SettingsTechniciansToggleText));
                if (openTechnicians)
                {
                    LoadTechnicianOptions();
                }
                break;
            case "Categories":
                SetSettingsSectionOpen(ref _isSettingsCategoriesOpen, !IsSettingsCategoriesOpen, nameof(IsSettingsCategoriesOpen), nameof(SettingsCategoriesToggleText));
                break;
            case "Orders":
                SetSettingsSectionOpen(ref _isSettingsOrdersOpen, !IsSettingsOrdersOpen, nameof(IsSettingsOrdersOpen), nameof(SettingsOrdersToggleText));
                break;
            case "Updates":
                SetSettingsSectionOpen(ref _isSettingsUpdatesOpen, !IsSettingsUpdatesOpen, nameof(IsSettingsUpdatesOpen), nameof(SettingsUpdatesToggleText));
                break;
            case "Diagnostics":
                SetSettingsSectionOpen(ref _isSettingsDiagnosticsOpen, !IsSettingsDiagnosticsOpen, nameof(IsSettingsDiagnosticsOpen), nameof(SettingsDiagnosticsToggleText));
                break;
        }
    }

    private void ResetSettingsSections()
    {
        SetSettingsSectionOpen(ref _isSettingsGeneralOpen, false, nameof(IsSettingsGeneralOpen), nameof(SettingsGeneralToggleText));
        SetSettingsSectionOpen(ref _isSettingsDataSyncOpen, false, nameof(IsSettingsDataSyncOpen), nameof(SettingsDataSyncToggleText));
        SetSettingsSectionOpen(ref _isSettingsLocalNetworkSyncOpen, false, nameof(IsSettingsLocalNetworkSyncOpen), nameof(SettingsLocalNetworkSyncToggleText));
        SetSettingsSectionOpen(ref _isSettingsTechniciansOpen, false, nameof(IsSettingsTechniciansOpen), nameof(SettingsTechniciansToggleText));
        SetSettingsSectionOpen(ref _isSettingsCategoriesOpen, false, nameof(IsSettingsCategoriesOpen), nameof(SettingsCategoriesToggleText));
        SetSettingsSectionOpen(ref _isSettingsOrdersOpen, false, nameof(IsSettingsOrdersOpen), nameof(SettingsOrdersToggleText));
        SetSettingsSectionOpen(ref _isSettingsUpdatesOpen, false, nameof(IsSettingsUpdatesOpen), nameof(SettingsUpdatesToggleText));
        SetSettingsSectionOpen(ref _isSettingsDiagnosticsOpen, false, nameof(IsSettingsDiagnosticsOpen), nameof(SettingsDiagnosticsToggleText));
    }

    private void SetSettingsSectionOpen(ref bool field, bool value, string propertyName, string togglePropertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(togglePropertyName);
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

        SaveTaskAndQueueIpadSnapshot(task);
        if (!SaveNow("undo-task-restore"))
        {
            return false;
        }

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
            _dirtyMaterials.Remove(materialId);
            _deletedMaterials.Add(materialId);
            MarkDataDirty($"material-delete:{materialId}");
        }

        foreach (var material in snapshotMaterials)
        {
            var copy = CloneMaterialItem(material);
            copy.TaskId = taskId;
            MarkMaterialDirty(copy);
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
            DeleteAttachmentAndQueueIpadSnapshot(attachment.Id);
        }

        foreach (var attachment in snapshotAttachments)
        {
            var copy = CloneAttachmentItem(attachment);
            copy.TaskId = taskId;
            SaveAttachmentAndQueueIpadSnapshot(copy);
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
        target.CustomerEmail = source.CustomerEmail;
        target.CustomerPhone = source.CustomerPhone;
        target.Description = source.Description;
        target.CategoryId = source.CategoryId;
        target.CategoryIds = source.CategoryIds.ToList();
        target.Status = source.Status;
        target.Priority = source.Priority;
        target.DueDate = source.DueDate;
        target.FollowUpDate = source.FollowUpDate;
        target.SentAt = source.SentAt;
        target.MaterialOrderedAt = source.MaterialOrderedAt;
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
        RefreshFollowUpTasks();
        var weekStart = GetWeekStart(DateTime.Today);

        DashboardThisWeekSection = CreateDashboardSection(
            "Diese Woche",
            "Keine Termine für diese Woche.",
            GetOverviewTasks(weekStart, weekStart.AddDays(7)));
        DashboardNextWeekSection = CreateDashboardSection(
            "Nächste Woche",
            "Keine Termine für nächste Woche.",
            GetOverviewTasks(weekStart.AddDays(7), weekStart.AddDays(14)));

        DashboardSections.Clear();
        DashboardSections.Add(DashboardThisWeekSection);
        DashboardSections.Add(DashboardNextWeekSection);
    }

    private void RefreshFollowUpTasks()
    {
        FollowUpTasks.Clear();

        foreach (var task in GetFollowUpTasks())
        {
            UpdateTaskCategoryPresentation(task);
            FollowUpTasks.Add(task);
        }

        OnPropertyChanged(nameof(HasFollowUpTasks));
        OnPropertyChanged(nameof(FollowUpTaskCount));
    }

    private static DashboardSection CreateDashboardSection(string title, string emptyText, IEnumerable<TaskItem> tasks)
    {
        var ordered = tasks
            .OrderBy(task => task.DueDate ?? DateTime.MaxValue)
            .ThenBy(task => task.CustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DashboardSection(title, emptyText, ordered.Count, ordered);
    }

    private IEnumerable<TaskItem> GetFollowUpTasks()
    {
        var today = DateTime.Today;

        return AllTasks
            .Where(task =>
                !task.IsDeleted &&
                task.FollowUpDate.HasValue &&
                task.FollowUpDate.Value.Date == today)
            .OrderBy(task => GetFollowUpSortGroup(task))
            .ThenBy(task => task.FollowUpDate!.Value.Date)
            .ThenBy(task => task.CustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetFollowUpSortGroup(TaskItem task)
    {
        if (!task.FollowUpDate.HasValue)
        {
            return 3;
        }

        var reminderDate = task.FollowUpDate.Value.Date;
        var today = DateTime.Today;
        if (reminderDate < today)
        {
            return 0;
        }

        if (reminderDate == today)
        {
            return 1;
        }

        return 2;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var dayOffset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-dayOffset);
    }

    private IEnumerable<TaskItem> GetOverviewTasks(DateTime startInclusive, DateTime endExclusive)
    {
        return AllTasks
            .Where(task =>
                !task.IsDeleted &&
                !IsDoneOrArchived(task) &&
                !IsInArchiveCategory(task) &&
                task.DueDate.HasValue &&
                task.DueDate.Value.Date >= startInclusive.Date &&
                task.DueDate.Value.Date < endExclusive.Date)
            .Select(task =>
            {
                UpdateTaskCategoryPresentation(task);
                return task;
            })
            .OrderBy(task => task.DueDate ?? DateTime.MaxValue)
            .ThenBy(task => task.CustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateCategoryCounts()
    {
        var weekStart = GetWeekStart(DateTime.Today);
        var overviewCount = AllTasks.Count(task =>
            !task.IsDeleted &&
            !IsDoneOrArchived(task) &&
            !IsInArchiveCategory(task) &&
            task.DueDate.HasValue &&
            task.DueDate.Value.Date >= weekStart &&
            task.DueDate.Value.Date < weekStart.AddDays(14));

        foreach (var category in Categories)
        {
            if (category.Id == OverviewCategoryId)
            {
                category.TaskCount = overviewCount;
            }
            else if (category.Id == TrashCategoryId)
            {
                category.TaskCount = AllTasks.Count(task => task.IsDeleted);
            }
            else if (category.Id == MobileInboxCategoryId)
            {
                category.TaskCount = MobileInboxEntries.Count;
            }
            else if (category.Id == DeskCategoryId || category.Id == SettingsCategoryId)
            {
                category.TaskCount = 0;
            }
            else
            {
                category.TaskCount = AllTasks.Count(task =>
                    !task.IsDeleted &&
                    (category.HasChildren
                        ? TaskBelongsToCategoryOrDescendant(task, category)
                        : TaskBelongsToSelectedCategory(task, category)));
            }
        }

        OnPropertyChanged(nameof(HasTrashItems));
        OnPropertyChanged(nameof(IsTrashEmpty));
        OnPropertyChanged(nameof(HasArchiveCategory));
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
                var normalizedSize = NormalizeDeskItemSize(item);
                if (Math.Abs(item.Width - normalizedSize.Width) > 0.5 ||
                    Math.Abs(item.Height - normalizedSize.Height) > 0.5)
                {
                    item.Width = normalizedSize.Width;
                    item.Height = normalizedSize.Height;
                }

                var resolvedLinkedTaskId = TryResolveDeskItemLinkedTaskIdFromPath(item);
                if (string.IsNullOrWhiteSpace(resolvedLinkedTaskId) && item.IsFileCard)
                {
                    var deskContentHash = EnsureDeskItemContentHash(item);

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
            MarkDeskItemDirty(deskItem);
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

        MarkDeskItemDirty(_resizedDeskItem);
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
        MobileInboxPhotoPreviews.Clear();
        MobileInboxSketchPreviews.Clear();
        MobileInboxFilePreviews.Clear();
        OnPropertyChanged(nameof(HasNoMaterials));

        var mobileInboxEntry = SelectedMobileInboxEntry;
        if (mobileInboxEntry is not null)
        {
            SelectedTaskCategory = Categories.FirstOrDefault(c => c.Id == MobileInboxCategoryId);
            foreach (var preview in mobileInboxEntry.PhotoPreviews)
            {
                MobileInboxPhotoPreviews.Add(preview);
            }

            foreach (var preview in mobileInboxEntry.SketchPreviews)
            {
                MobileInboxSketchPreviews.Add(preview);
            }

            foreach (var preview in mobileInboxEntry.FilePreviews)
            {
                MobileInboxFilePreviews.Add(preview);
            }

            RefreshTaskCategorySelections();
            OnPropertyChanged(nameof(HasMobileInboxPhotoPreviews));
            OnPropertyChanged(nameof(HasMobileInboxSketchPreviews));
            OnPropertyChanged(nameof(HasMobileInboxFilePreviews));
            OnPropertyChanged(nameof(HasNoMobileInboxPhotoPreviews));
            OnPropertyChanged(nameof(HasNoMobileInboxSketchPreviews));
            OnPropertyChanged(nameof(HasNoMobileInboxFilePreviews));
        }
        else if (SelectedTask is not null)
        {
            EnsureTaskCategoryState(SelectedTask);
            SelectedTaskCategory = Categories.FirstOrDefault(c => c.Id == SelectedTask.CategoryId);
            UpdateTaskCategoryPresentation(SelectedTask);
            RefreshTaskCategorySelections();

            foreach (var item in _repository.GetMaterials(SelectedTask.Id))
            {
                SubscribeMaterialItem(item);
                Materials.Add(item);
            }
            OnPropertyChanged(nameof(HasNoMaterials));

            foreach (var item in _repository.GetAttachments(SelectedTask.Id))
            {
                Attachments.Insert(0, item);
            }
        }
        else
        {
            SelectedTaskCategory = null;
            RefreshTaskCategorySelections();
        }

        OnPropertyChanged(nameof(HasMobileInboxPhotoPreviews));
        OnPropertyChanged(nameof(HasMobileInboxSketchPreviews));
        OnPropertyChanged(nameof(HasMobileInboxFilePreviews));
        OnPropertyChanged(nameof(HasNoMobileInboxPhotoPreviews));
        OnPropertyChanged(nameof(HasNoMobileInboxSketchPreviews));
        OnPropertyChanged(nameof(HasNoMobileInboxFilePreviews));
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

        var category = !IsOverviewSelected && !IsDeskSelected && !IsSettingsSelected && !IsTrashSelected &&
            SelectedCategory is not null &&
            IsSelectableTaskCategory(SelectedCategory)
            ? SelectedCategory
            : null;
        category ??= Categories.FirstOrDefault(c =>
            IsSelectableTaskCategory(c) &&
            string.Equals(c.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase));
        category ??= Categories.FirstOrDefault(IsSelectableTaskCategory);
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
            SortPosition = _repository.GetTopTaskSortPosition(category.Id),
            CreatedAt = now,
            UpdatedAt = now
        };

        SubscribeTaskItem(task);
        SaveTaskAndQueueIpadSnapshot(task);
        _tasksPendingDuplicateCheck.Add(task.Id);
        AllTasks.Insert(0, task);

        ClearSearchTextWithoutRefresh();
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

        MarkDeskItemDirty(deskItem);
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

        MarkDeskItemDirty(deskItem);
        SubscribeDeskItem(deskItem);
        DeskItems.Add(deskItem);
        UpdateDeskSurfaceBounds();
    }

    private DeskItem? FindAutomaticTaskDeskNote(string taskId)
    {
        return DeskItems.FirstOrDefault(item =>
            item.IsNoteCard &&
            string.Equals(item.LinkedTaskId, taskId, StringComparison.OrdinalIgnoreCase));
    }

    private void AddAutomaticTaskDeskNote(TaskItem task)
    {
        if (FindAutomaticTaskDeskNote(task.Id) is not null)
        {
            return;
        }

        var now = DateTime.Now;
        var offset = DeskItems.Count * 28;
        var deskItem = new DeskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DeskItemTypeNote,
            DisplayName = string.IsNullOrWhiteSpace(task.CustomerName) ? "Auftrag" : task.CustomerName.Trim(),
            Text = string.IsNullOrWhiteSpace(task.Title) ? "Auftrag" : task.Title.Trim(),
            LinkedTaskId = task.Id,
            X = 40 + (offset % 320),
            Y = 40 + (offset % 220),
            Width = DeskNoteDefaultWidth,
            Height = DeskNoteDefaultHeight,
            CreatedAt = now,
            UpdatedAt = now
        };

        MarkDeskItemDirty(deskItem);
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

    private async void ManualSave_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveManuallyAsync(validateSelectedTask: true);
    }

    private async void SaveTask_OnClick(object? sender, RoutedEventArgs e)
    {
        await SaveManuallyAsync(validateSelectedTask: true);
    }

    private async Task SaveManuallyAsync(bool validateSelectedTask)
    {
        if (SelectedTask is null)
        {
            SaveCurrentMaterials();
            await SaveNowAsync("manual-save");
            return;
        }

        CaptureTaskUndoState(SelectedTask, preserveExistingSnapshot: true);
        ApplySelectedTaskStatusRules();

        if (validateSelectedTask &&
            _tasksPendingDuplicateCheck.Contains(SelectedTask.Id) &&
            await HandleNewTaskDuplicateAsync(SelectedTask))
        {
            return;
        }

        SaveTaskAndQueueIpadSnapshot(SelectedTask);
        _tasksPendingDuplicateCheck.Remove(SelectedTask.Id);
        SaveCurrentMaterials();
        await SaveNowAsync("manual-save");
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
            checkBox.DataContext is not TaskCategorySelection selection ||
            !selection.IsSelectable)
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

        var primaryCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Id, SelectedTask.CategoryId, StringComparison.OrdinalIgnoreCase));
        var visibleCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Id, visibleCategoryIdBeforeChange, StringComparison.OrdinalIgnoreCase) &&
            SelectedTask.CategoryIds.Contains(category.Id, StringComparer.OrdinalIgnoreCase));
        var targetCategory = visibleCategory ?? primaryCategory;

        _isUpdatingSelection = true;
        try
        {
            SelectedTaskCategory = targetCategory;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        SaveTaskAndQueueIpadSnapshot(SelectedTask);
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



    private void ReverseSortDirection_OnClick(object? sender, RoutedEventArgs e)
    {
        _isSortDescending = !_isSortDescending;
        OnPropertyChanged(nameof(SortDirectionGlyph));
        OnPropertyChanged(nameof(SortDirectionTooltip));
        RefreshVisibleTasks();
    }

    private static int GetNameSortGroup(TaskItem task)
    {
        if (!string.IsNullOrWhiteSpace(task.CustomerName))
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(task.AssignedTo))
        {
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(task.Title))
        {
            return 2;
        }

        return 3;
    }

    private static string GetNameSortValue(TaskItem task)
    {
        return GetNameSortGroup(task) switch
        {
            0 => task.CustomerName.Trim(),
            1 => task.AssignedTo.Trim(),
            2 => task.Title.Trim(),
            _ => task.Id
        };
    }

    private static string NormalizeCategorySortMode(string? sortMode)
    {
        return string.IsNullOrWhiteSpace(sortMode)
            ? "Erstellt am"
            : sortMode.Trim() switch
            {
                "Name" or "Name A-Z" or "Kunde" or "Kunde A-Z" => "Name: A -> Z",
                "Termin" => "Datum: alt -> neu",
                var value => value
            };
    }

    private static string NormalizeSortField(string? sortField)
    {
        return sortField?.Trim() switch
        {
            SortFieldDate => SortFieldDate,
            SortFieldName => SortFieldName,
            SortFieldManual => SortFieldManual,
            SortFieldCreatedAt => SortFieldCreatedAt,
            SortFieldFollowUp => SortFieldFollowUp,
            SortFieldSentAt => SortFieldSentAt,
            SortFieldUpdatedAt => SortFieldUpdatedAt,
            "Datum: alt -> neu" or "Datum: neu -> alt" or "Termin" => SortFieldDate,
            "Name: A -> Z" or "Name: Z -> A" or "Name A-Z" or "Kunde" or "Kunde A-Z" => SortFieldName,
            _ => SortFieldCreatedAt
        };
    }

    private IEnumerable<TaskItem> SortTasksForCategory(IEnumerable<TaskItem> tasks)
    {
        var field = NormalizeSortField(SelectedSortField);

        return field switch
        {
            SortFieldName => _isSortDescending
                ? tasks
                    .OrderBy(GetNameSortGroup)
                    .ThenByDescending(GetNameSortValue, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(GetNameSortGroup)
                    .ThenBy(GetNameSortValue, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldDate => _isSortDescending
                ? tasks
                    .OrderBy(task => task.DueDate.HasValue ? 0 : 1)
                    .ThenByDescending(task => task.DueDate ?? DateTime.MinValue)
                    .ThenBy(GetNameSortValue, StringComparer.CurrentCultureIgnoreCase)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(task => task.DueDate.HasValue ? 0 : 1)
                    .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
                    .ThenBy(GetNameSortValue, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldFollowUp => _isSortDescending
                ? tasks
                    .Where(task => task.FollowUpDate.HasValue)
                    .OrderByDescending(task => task.FollowUpDate!.Value.Date)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .Where(task => task.FollowUpDate.HasValue)
                    .OrderBy(task => GetFollowUpSortGroup(task))
                    .ThenBy(task => task.FollowUpDate!.Value.Date)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldSentAt => _isSortDescending
                ? tasks
                    .OrderBy(task => task.SentAt.HasValue ? 0 : 1)
                    .ThenByDescending(task => task.SentAt ?? DateTime.MinValue)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(task => task.SentAt.HasValue ? 0 : 1)
                    .ThenBy(task => task.SentAt ?? DateTime.MaxValue)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldCreatedAt => _isSortDescending
                ? tasks
                    .OrderByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldUpdatedAt => _isSortDescending
                ? tasks
                    .OrderByDescending(task => task.UpdatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(task => task.UpdatedAt)
                    .ThenBy(task => task.SortPosition)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            SortFieldManual => _isSortDescending
                ? tasks
                    .OrderByDescending(task => task.SortPosition)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                : tasks
                    .OrderBy(task => task.SortPosition)
                    .ThenByDescending(task => task.CreatedAt)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase),

            _ => tasks
                .OrderByDescending(task => task.CreatedAt)
                .ThenBy(task => task.SortPosition)
                .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IEnumerable<TaskItem> SortTrashTasks(IEnumerable<TaskItem> tasks)
    {
        return tasks
            .OrderBy(task => task.DeletedAt.HasValue ? 0 : 1)
            .ThenByDescending(task => task.DeletedAt ?? DateTime.MinValue)
            .ThenByDescending(task => task.CreatedAt)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Id, StringComparer.OrdinalIgnoreCase);
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

        if (!await EnsureSafetyBackupBeforeRiskyActionAsync("den Auftrag in den Papierkorb zu verschieben"))
        {
            return;
        }

        CaptureTaskUndoState(task);
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

    private async void RestoreTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTask is null || !SelectedTask.IsDeleted)
        {
            return;
        }

        if (!await EnsureSafetyBackupBeforeRiskyActionAsync("den Auftrag wiederherzustellen"))
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask);
        var task = SelectedTask;
        task.IsDeleted = false;
        task.DeletedAt = null;
        task.UpdatedAt = DateTime.Now;
        SaveTaskAndQueueIpadSnapshot(task);

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

        if (!await EnsureSafetyBackupBeforeRiskyActionAsync("den Papierkorb zu leeren"))
        {
            return;
        }

        if (SelectedTask?.IsDeleted == true)
        {
            ClearSelectedTask();
        }

        EmptyTrashAndQueueIpadSnapshot();

        foreach (var task in AllTasks.Where(task => task.IsDeleted).ToList())
        {
            AllTasks.Remove(task);
        }

        ClearTaskUndoState();
        RefreshVisibleTasks();
        UpdateCategoryCounts();
    }

    private async Task<bool> EnsureSafetyBackupBeforeRiskyActionAsync(string actionDescription)
    {
        try
        {
            var result = await Task.Run(() => _backupService.CreateBackup());
            LastBackupPath = result.BackupPath;
            LastBackupTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            BackupStatus = "Sicherung vor der Aktion erstellt.";
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Safety backup failed before {actionDescription}: {ex}");
            var continueWithoutBackup = await ShowBackupFailureConfirmationDialogAsync(actionDescription);
            if (!continueWithoutBackup)
            {
                BackupStatus = "Sicherung vor der Aktion fehlgeschlagen. Aktion abgebrochen.";
                return false;
            }

            BackupStatus = "Sicherung vor der Aktion fehlgeschlagen. Aktion wird trotzdem ausgeführt.";
            return true;
        }
    }

    private async Task<bool> ShowBackupFailureConfirmationDialogAsync(string actionDescription)
    {
        var dialog = new Window
        {
            Title = "Datensicherung fehlgeschlagen",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Datensicherung vor der Aktion konnte nicht erstellt werden.",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"Vor dem Schritt „{actionDescription}“ ist die automatische Sicherung fehlgeschlagen. Ohne Sicherung fortfahren?",
                            FontSize = 13,
                            Foreground = ResourceBrush("TextSecondaryBrush"),
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
                                CreateDialogAction("Trotzdem fortfahren", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var cancelAction = (Border)buttonsPanel.Children[0];
        var proceedAction = (Border)buttonsPanel.Children[1];

        var result = false;

        cancelAction.PointerReleased += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        proceedAction.PointerReleased += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;

        static Border CreateDialogAction(string text, bool isDanger)
        {
            var normalBackground = ResourceBrush(isDanger ? "DangerBrush" : "SurfaceElevatedBrush");
            var hoverBackground = ResourceBrush(isDanger ? "ButtonDangerHoverBackgroundBrush" : "HoverBackgroundBrush");
            var normalBorder = ResourceBrush(isDanger ? "DangerBrush" : "BorderBrushDark");
            var hoverBorder = ResourceBrush(isDanger ? "ButtonDangerHoverBorderBrush" : "BorderBrushStrong");
            var foreground = ResourceBrush(isDanger ? "ButtonDangerHoverForegroundBrush" : "TextPrimaryBrush");

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
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6),
                Child = label
            };

            action.PointerEntered += (_, _) =>
            {
                action.Background = hoverBackground;
                action.BorderBrush = hoverBorder;
                label.Foreground = foreground;
            };

            action.PointerExited += (_, _) =>
            {
                action.Background = normalBackground;
                action.BorderBrush = normalBorder;
                label.Foreground = foreground;
            };

            return action;
        }
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
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
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
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"„{title}“ wird aus den normalen Listen entfernt und in den Papierkorb verschoben. Anhänge bleiben erhalten.",
                            FontSize = 13,
                            Foreground = ResourceBrush("TextSecondaryBrush"),
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
            var normalBackground = ResourceBrush(isDanger ? "DangerBrush" : "SurfaceElevatedBrush");
            var hoverBackground = ResourceBrush(isDanger ? "ButtonDangerHoverBackgroundBrush" : "HoverBackgroundBrush");
            var normalBorder = ResourceBrush(isDanger ? "DangerBrush" : "BorderBrushDark");
            var hoverBorder = ResourceBrush(isDanger ? "ButtonDangerHoverBorderBrush" : "BorderBrushStrong");
            var foreground = ResourceBrush(isDanger ? "ButtonDangerHoverForegroundBrush" : "TextPrimaryBrush");

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
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6),
                Child = label
            };

            action.PointerEntered += (_, _) =>
            {
                action.Background = hoverBackground;
                action.BorderBrush = hoverBorder;
                label.Foreground = foreground;
            };

            action.PointerExited += (_, _) =>
            {
                action.Background = normalBackground;
                action.BorderBrush = normalBorder;
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
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
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
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = deletedCount == 1
                                ? "1 gelöschte Aufgabe wird endgültig entfernt. Anhänge bleiben als Dateien erhalten."
                                : $"{deletedCount} gelöschte Aufgaben werden endgültig entfernt. Anhänge bleiben als Dateien erhalten.",
                            FontSize = 13,
                            Foreground = ResourceBrush("TextSecondaryBrush"),
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
            var normalBackground = ResourceBrush(isDanger ? "DangerBrush" : "SurfaceElevatedBrush");
            var hoverBackground = ResourceBrush(isDanger ? "ButtonDangerHoverBackgroundBrush" : "HoverBackgroundBrush");
            var normalBorder = ResourceBrush(isDanger ? "DangerBrush" : "BorderBrushDark");
            var hoverBorder = ResourceBrush(isDanger ? "ButtonDangerHoverBorderBrush" : "BorderBrushStrong");
            var foreground = ResourceBrush(isDanger ? "ButtonDangerHoverForegroundBrush" : "TextPrimaryBrush");

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
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 6),
                Child = label
            };

            action.PointerEntered += (_, _) =>
            {
                action.Background = hoverBackground;
                action.BorderBrush = hoverBorder;
                label.Foreground = foreground;
            };

            action.PointerExited += (_, _) =>
            {
                action.Background = normalBackground;
                action.BorderBrush = normalBorder;
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

        SubscribeMaterialItem(item);
        Materials.Insert(0, item);
        OnPropertyChanged(nameof(HasNoMaterials));
        MarkMaterialDirty(item);
    }

    private void DeleteMaterial_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MaterialItem item })
        {
            return;
        }

        CaptureTaskUndoState(SelectedTask ?? AllTasks.FirstOrDefault(task => string.Equals(task.Id, item.TaskId, StringComparison.OrdinalIgnoreCase)));
        _dirtyMaterials.Remove(item.Id);
        _deletedMaterials.Add(item.Id);
        MarkDataDirty($"material-delete:{item.Id}");
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
            SaveAttachmentAndQueueIpadSnapshot(attachment);
            SaveTaskAndQueueIpadSnapshot(SelectedTask);
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
        SaveAttachmentAndQueueIpadSnapshot(item);

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
            DeleteAttachmentAndQueueIpadSnapshot(item.Id);
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
            _dirtyDeskItems.Remove(deskItem.Id);
            _deletedDeskItems.Add(deskItem.Id);
            MarkDataDirty($"desk-item-delete:{deskItem.Id}");
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

    private CategoryItem? FindCategoryByName(string name, string? excludedCategoryId = null)
    {
        var normalizedName = NormalizeCategoryName(name);
        return Categories.FirstOrDefault(category =>
            !string.Equals(category.Id, excludedCategoryId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeCategoryName(category.Name), normalizedName, StringComparison.CurrentCultureIgnoreCase));
    }

    private static string NormalizeCategoryName(string? name)
    {
        return string.Join(' ', (name ?? string.Empty)
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private void AddCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        var name = NormalizeCategoryName(CategoryEditorName);
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryMessage = "Bitte einen Kategorienamen eingeben.";
            return;
        }

        if (FindCategoryByName(name) is not null)
        {
            CategoryMessage = "Diese Kategorie gibt es bereits.";
            return;
        }

        try
        {
            var category = new CategoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                SortOrder = GetNextVisibleCategorySortOrder(),
                SortMode = "Erstellt am",
                Color = "#F2F3F5",
                IsVisible = true
            };

            SubscribeCategoryItem(category);
            InsertBeforeSettings(category);
            RefreshCategoryDependentViews();
            UpdateCategoryCounts();

            SaveCategoryAndQueueIpadSnapshot(category, "Kategorie erstellt");
            CategoryEditorName = string.Empty;
            CategoryMessage = "Kategorie angelegt.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Category could not be created: {ex}");
            CategoryMessage = "Kategorie konnte nicht angelegt werden.";
        }
    }

    private int GetNextVisibleCategorySortOrder()
    {
        var maxSortOrder = Categories
            .Where(category => !IsSpecialCategory(category))
            .Select(category => category.SortOrder)
            .DefaultIfEmpty(-1)
            .Max();

        return maxSortOrder + 1;
    }

    private void RenameCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            CategoryMessage = "Diese Kategorie kann nicht umbenannt werden.";
            return;
        }

        var name = NormalizeCategoryName(CategoryEditorName);
        RenameCategory(SelectedCategory, name);
    }

    private void RenameCategory(CategoryItem category, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            CategoryMessage = "Bitte einen Kategorienamen eingeben.";
            return;
        }

        if (FindCategoryByName(name, category.Id) is not null)
        {
            CategoryMessage = "Diese Kategorie gibt es bereits.";
            return;
        }

        category.Name = name;
        SaveCategoryAndQueueIpadSnapshot(category, "Kategorie umbenannt");
        OnPropertyChanged(nameof(SelectedCategory));
        RefreshCategoryDependentViews();
        ApplySelectedCategoryContent();
        UpdateCategoryCounts();
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

        SaveCategoryAndQueueIpadSnapshot(current);
        SaveCategoryAndQueueIpadSnapshot(target);

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

    private void CategorySettingsItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not CategoryItem category ||
            !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed ||
            !CanDragCategory(category))
        {
            _categoryDragCandidate = null;
            _categoryDragStartEvent = null;
            return;
        }

        _categoryDragCandidate = category;
        _categoryDragStartEvent = e;
        _categoryDragStartPoint = e.GetPosition(this);
    }

    private async void CategorySettingsItem_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_categoryDragCandidate is null || _categoryDragStartEvent is null || _isDraggingCategory)
        {
            return;
        }

        if (sender is not Control control || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            _categoryDragCandidate = null;
            _categoryDragStartEvent = null;
            return;
        }

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _categoryDragStartPoint.X) < 6 &&
            Math.Abs(point.Y - _categoryDragStartPoint.Y) < 6)
        {
            return;
        }

        var category = _categoryDragCandidate;
        var dragData = CreateCategoryDragData(category);
        if (dragData is null)
        {
            _categoryDragCandidate = null;
            _categoryDragStartEvent = null;
            return;
        }

        _isDraggingCategory = true;
        try
        {
            await DragDrop.DoDragDropAsync(_categoryDragStartEvent, dragData, DragDropEffects.Move);
        }
        finally
        {
            _isDraggingCategory = false;
            _categoryDragCandidate = null;
            _categoryDragStartEvent = null;
            ClearCategoryDropVisuals();
        }
    }

    private void CategorySettingsItem_OnDragOver(object? sender, DragEventArgs e)
    {
        ClearCategoryRootDropVisual();

        if (TryGetDraggedCategory(e, out var dragged) &&
            sender is Control { DataContext: CategoryItem target } control &&
            CanDropCategoryOnTarget(dragged, target))
        {
            SetCategoryDropVisual(target, GetCategoryDropVisualState(control, e));
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            ClearCategoryDropVisuals();
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CategorySettingsItem_OnDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDraggedCategory(e, out var dragged))
        {
            ClearCategoryDropVisuals();
            e.Handled = true;
            return;
        }

        if (sender is not Control { DataContext: CategoryItem target } ||
            !CanDropCategoryOnTarget(dragged, target))
        {
            ClearCategoryDropVisuals();
            CategoryMessage = "Kategorie konnte nicht verschoben werden.";
            e.Handled = true;
            return;
        }

        var targetControl = (Control)sender;
        var dropState = GetCategoryDropVisualState(targetControl, e);
        switch (dropState)
        {
            case CategoryDropVisualState.Before:
                MoveCategoryNextToTarget(dragged, target, beforeTarget: true);
                break;
            case CategoryDropVisualState.After:
                MoveCategoryNextToTarget(dragged, target, beforeTarget: false);
                break;
            default:
                MoveCategoryToParent(dragged, target.Id);
                break;
        }

        ClearCategoryDropVisuals();
        e.Handled = true;
    }

    private void CategoryRootDropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        ClearCategoryItemDropVisual();
        if (TryGetDraggedCategory(e, out var dragged) && CanMoveCategoryToRoot(dragged))
        {
            SetCategoryRootDropVisual(true);
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            SetCategoryRootDropVisual(false);
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void CategoryRootDropZone_OnDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDraggedCategory(e, out var dragged))
        {
            ClearCategoryDropVisuals();
            e.Handled = true;
            return;
        }

        if (!CanMoveCategoryToRoot(dragged))
        {
            ClearCategoryDropVisuals();
            CategoryMessage = "Kategorie konnte nicht zur Hauptkategorie gemacht werden.";
            e.Handled = true;
            return;
        }

        MoveCategoryToParent(dragged, parentId: null);
        ClearCategoryDropVisuals();
        e.Handled = true;
    }

    private void CategoryDropZone_OnDragLeave(object? sender, RoutedEventArgs e)
    {
        ClearCategoryDropVisuals();
    }

    private static CategoryDropVisualState GetCategoryDropVisualState(Control targetControl, DragEventArgs e)
    {
        var height = targetControl.Bounds.Height;
        if (height <= 0)
        {
            return CategoryDropVisualState.Inside;
        }

        var relativeY = Math.Clamp(e.GetPosition(targetControl).Y / height, 0, 1);
        return relativeY switch
        {
            < 0.30 => CategoryDropVisualState.Before,
            > 0.70 => CategoryDropVisualState.After,
            _ => CategoryDropVisualState.Inside
        };
    }

    private void SetCategoryDropVisual(CategoryItem target, CategoryDropVisualState state)
    {
        if (_categoryDropTarget is not null &&
            !string.Equals(_categoryDropTarget.Id, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            _categoryDropTarget.DropVisualState = CategoryDropVisualState.None;
        }

        _categoryDropTarget = target;
        target.DropVisualState = state;
    }

    private void SetCategoryRootDropVisual(bool isDropTarget)
    {
        if (_isCategoryRootDropTarget == isDropTarget)
        {
            return;
        }

        _isCategoryRootDropTarget = isDropTarget;
        OnPropertyChanged(nameof(IsCategoryRootDropTarget));
        OnPropertyChanged(nameof(CategoryRootDropBackground));
        OnPropertyChanged(nameof(CategoryRootDropBorderBrush));
    }

    private void ClearCategoryItemDropVisual()
    {
        if (_categoryDropTarget is not null)
        {
            _categoryDropTarget.DropVisualState = CategoryDropVisualState.None;
            _categoryDropTarget = null;
        }
    }

    private void ClearCategoryRootDropVisual()
    {
        SetCategoryRootDropVisual(false);
    }

    private void ClearCategoryDropVisuals()
    {
        ClearCategoryItemDropVisual();
        ClearCategoryRootDropVisual();
    }

    private bool TryGetDraggedCategory(DragEventArgs e, out CategoryItem category)
    {
        category = null!;
        var categoryId = TryGetDraggedCategoryId(e.DataTransfer);
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return false;
        }

        category = Categories.FirstOrDefault(item => string.Equals(item.Id, categoryId, StringComparison.OrdinalIgnoreCase))!;
        return category is not null && CanDragCategory(category);
    }

    private static DataTransfer? CreateCategoryDragData(CategoryItem category)
    {
        if (!CanDragCategory(category))
        {
            return null;
        }

        var categoryId = category.Id.Trim();
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return null;
        }

        var item = new DataTransferItem();
        item.Set(CategoryDragDataFormat, categoryId);
        item.SetText($"{CategoryDragTextPrefix}{categoryId}");

        var data = new DataTransfer();
        data.Add(item);
        return data.Items.Count > 0 ? data : null;
    }

    private static string? TryGetDraggedCategoryId(IDataTransfer dataTransfer)
    {
        var categoryId = dataTransfer.TryGetValue(CategoryDragDataFormat);
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            return categoryId.Trim();
        }

        var text = dataTransfer.TryGetText();
        if (string.IsNullOrWhiteSpace(text) ||
            !text.StartsWith(CategoryDragTextPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        categoryId = text[CategoryDragTextPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(categoryId) ? null : categoryId;
    }

    private static bool CanDragCategory(CategoryItem category)
    {
        return !string.IsNullOrWhiteSpace(category.Id) &&
               !IsSpecialCategory(category) &&
               !IsArchiveCategory(category) &&
               !IsLegacyMobileApprovalCategory(category.Name);
    }

    private bool CanDropCategoryOnTarget(CategoryItem dragged, CategoryItem target)
    {
        if (!CanDragCategory(dragged) ||
            !CanDragCategory(target) ||
            IsSpecialCategory(dragged) ||
            IsSpecialCategory(target) ||
            IsArchiveCategory(dragged) ||
            IsArchiveCategory(target) ||
            IsLegacyMobileApprovalCategory(dragged.Name) ||
            IsLegacyMobileApprovalCategory(target.Name) ||
            string.Equals(dragged.Id, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsDescendantOf(target, dragged.Id);
    }

    private bool CanMoveCategoryToRoot(CategoryItem category)
    {
        return CanDragCategory(category) &&
               !IsSpecialCategory(category) &&
               !IsArchiveCategory(category) &&
               !IsLegacyMobileApprovalCategory(category.Name) &&
               !string.IsNullOrWhiteSpace(category.ParentId);
    }

    private bool IsDescendantOf(CategoryItem category, string possibleAncestorId)
    {
        var current = category;
        while (!string.IsNullOrWhiteSpace(current.ParentId))
        {
            if (string.Equals(current.ParentId, possibleAncestorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var parent = Categories.FirstOrDefault(item => string.Equals(item.Id, current.ParentId, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    private void MoveCategoryWithinParent(CategoryItem dragged, CategoryItem target, bool beforeTarget)
    {
        var siblings = GetCategorySiblings(dragged.ParentId)
            .Where(category => !string.Equals(category.Id, dragged.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var targetIndex = siblings.FindIndex(category => string.Equals(category.Id, target.Id, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0)
        {
            CategoryMessage = "Zielkategorie wurde nicht gefunden.";
            return;
        }

        var insertIndex = beforeTarget ? targetIndex : targetIndex + 1;
        siblings.Insert(insertIndex, dragged);
        NormalizeCategorySortOrder(siblings);
        PersistCategoryTreeChange(siblings, "Kategorie-Reihenfolge geändert.");
    }

    private void MoveCategoryNextToTarget(CategoryItem dragged, CategoryItem target, bool beforeTarget)
    {
        var targetParentId = target.ParentId;
        var changed = new List<CategoryItem>();

        if (!string.Equals(dragged.ParentId, targetParentId, StringComparison.OrdinalIgnoreCase))
        {
            var oldSiblings = GetCategorySiblings(dragged.ParentId)
                .Where(category => !string.Equals(category.Id, dragged.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            NormalizeCategorySortOrder(oldSiblings);
            changed.AddRange(oldSiblings);
            dragged.ParentId = targetParentId;
        }

        var newSiblings = GetCategorySiblings(targetParentId)
            .Where(category => !string.Equals(category.Id, dragged.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var targetIndex = newSiblings.FindIndex(category => string.Equals(category.Id, target.Id, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0)
        {
            CategoryMessage = "Zielkategorie wurde nicht gefunden.";
            return;
        }

        var insertIndex = beforeTarget ? targetIndex : targetIndex + 1;
        newSiblings.Insert(insertIndex, dragged);
        NormalizeCategorySortOrder(newSiblings);
        changed.AddRange(newSiblings);

        PersistCategoryTreeChange(changed, beforeTarget
            ? "Kategorie wurde davor einsortiert."
            : "Kategorie wurde danach einsortiert.");
    }

    private void MoveCategoryToParent(CategoryItem dragged, string? parentId)
    {
        if (string.Equals(dragged.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
        {
            CategoryMessage = "Kategorie ist bereits an dieser Stelle.";
            return;
        }

        var changed = new List<CategoryItem>();
        var oldSiblings = GetCategorySiblings(dragged.ParentId)
            .Where(category => !string.Equals(category.Id, dragged.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        NormalizeCategorySortOrder(oldSiblings);
        changed.AddRange(oldSiblings);

        dragged.ParentId = parentId;
        dragged.IsExpanded = false;
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var parent = Categories.FirstOrDefault(category => string.Equals(category.Id, parentId, StringComparison.OrdinalIgnoreCase));
            if (parent is not null)
            {
                parent.IsExpanded = true;
            }
        }

        var newSiblings = GetCategorySiblings(parentId)
            .Where(category => !string.Equals(category.Id, dragged.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        newSiblings.Add(dragged);
        NormalizeCategorySortOrder(newSiblings);
        changed.AddRange(newSiblings);

        PersistCategoryTreeChange(changed, parentId is null
            ? "Kategorie ist jetzt Hauptkategorie."
            : "Kategorie wurde untergeordnet.");
    }

    private List<CategoryItem> GetCategorySiblings(string? parentId)
    {
        return Categories
            .Where(category =>
                !IsSpecialCategory(category) &&
                !IsArchiveCategory(category) &&
                !IsLegacyMobileApprovalCategory(category.Name) &&
                string.Equals(category.ParentId ?? string.Empty, parentId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void NormalizeCategorySortOrder(IReadOnlyList<CategoryItem> categories)
    {
        for (var index = 0; index < categories.Count; index++)
        {
            categories[index].SortOrder = index;
        }
    }

    private void PersistCategoryTreeChange(IEnumerable<CategoryItem> categories, string message)
    {
        var changed = categories
            .Where(category => !IsSpecialCategory(category))
            .GroupBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (var category in changed)
        {
            SaveCategoryAndQueueIpadSnapshot(category, "Kategoriebaum geändert");
        }

        RefreshCategoryDependentViews();
        ApplySelectedCategoryContent();
        UpdateCategoryCounts();
        CategoryMessage = message;
    }

    private void HideCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedCategory is null || IsSpecialCategory(SelectedCategory))
        {
            CategoryMessage = "Diese Kategorie kann nicht entfernt werden.";
            return;
        }

        RemoveCategory(SelectedCategory);
    }

    private async void OpenArchiveCategory_OnClick(object? sender, RoutedEventArgs e)
    {
        await ShowArchiveDialogAsync();
    }

    private async Task ShowArchiveDialogAsync()
    {
        var archivedTasks = AllTasks
            .Where(task => !task.IsDeleted && IsArchivedForSearch(task))
            .OrderByDescending(task => task.CompletedAt ?? task.UpdatedAt)
            .ThenBy(task => task.CustomerName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var content = new StackPanel
        {
            Spacing = 8
        };

        if (archivedTasks.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Keine archivierten Aufträge vorhanden.",
                Foreground = ResourceBrush("TextTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        var dialog = new Window
        {
            Title = "Archiv",
            Width = 640,
            MinWidth = 520,
            MaxHeight = 560,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Archiv",
                            FontSize = 20,
                            FontWeight = FontWeight.Bold,
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            Margin = new Thickness(0, 0, 0, 12)
                        },
                        new ScrollViewer
                        {
                            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            Content = content
                        }
                    }
                }
            }
        };

        if (archivedTasks.Count > 0)
        {
            content.Children.Clear();
            foreach (var task in archivedTasks)
            {
                content.Children.Add(CreateArchiveTaskRow(task, dialog));
            }
        }

        DockPanel.SetDock(((DockPanel)((Border)dialog.Content!).Child!).Children[0], Dock.Top);
        await dialog.ShowDialog(this);
    }

    private Control CreateArchiveTaskRow(TaskItem task, Window? dialog)
    {
        var details = new List<string>();
        var categoryNames = GetTaskCategoryNameList(task);
        if (categoryNames.Count > 0)
        {
            details.Add($"Kategorie: {string.Join(", ", categoryNames)}");
        }

        if (task.CompletedAt.HasValue)
        {
            details.Add($"Archivdatum: {FormatDateShort(task.CompletedAt)}");
        }

        if (task.DueDate.HasValue)
        {
            details.Add($"Termin: {FormatArchiveDateTime(task.DueDate)}");
        }

        if (task.FollowUpDate.HasValue)
        {
            details.Add($"Wiedervorlage: {FormatDateShort(task.FollowUpDate)}");
        }

        var openButton = new Button
        {
            Content = "Öffnen",
            Padding = new Thickness(10, 5),
            MinWidth = 78
        };
        openButton.Classes.Add("Secondary");
        openButton.Click += (_, _) =>
        {
            dialog?.Close();
            OpenArchivedTask(task);
        };

        var restoreButton = new Button
        {
            Content = "Zurück in Offene Aufgaben",
            Padding = new Thickness(10, 5)
        };
        restoreButton.Classes.Add("Secondary");
        restoreButton.Click += (_, _) =>
        {
            RestoreArchivedTaskToOpenTasks(task);
            dialog?.Close();
        };

        var row = new Border
        {
            Background = ResourceBrush("CardBackgroundBrush"),
            BorderBrush = ResourceBrush("BorderBrushDark"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = FormatArchiveTaskTitle(task),
                                FontSize = 14,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = ResourceBrush("TextPrimaryBrush"),
                                TextWrapping = TextWrapping.Wrap
                            },
                            new TextBlock
                            {
                                Text = details.Count == 0 ? "Keine weiteren Angaben." : string.Join(" · ", details),
                                FontSize = 12,
                                Foreground = ResourceBrush("TextSecondaryBrush"),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            openButton,
                            restoreButton
                        }
                    }
                }
            }
        };

        Grid.SetColumn((Control)((Grid)row.Child!).Children[1], 1);
        row.DoubleTapped += (_, _) =>
        {
            dialog?.Close();
            OpenArchivedTask(task);
        };
        return row;
    }

    private void OpenArchivedTask(TaskItem task)
    {
        NavigateToTask(task, fromGlobalSearch: false);
        if (SelectedTask?.Id == task.Id)
        {
            LoadTaskDetails();
        }
    }

    private void RestoreArchivedTaskToOpenTasks(TaskItem task)
    {
        var openCategory = Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(IsSelectableTaskCategory);
        if (openCategory is null)
        {
            CategoryMessage = "Offene Aufgaben wurde nicht gefunden.";
            return;
        }

        CaptureTaskUndoState(task, preserveExistingSnapshot: true);
        EnsureTaskCategoryState(task);
        task.Status = "Offen";
        task.CompletedAt = null;
        task.CategoryId = openCategory.Id;
        task.CategoryIds.RemoveAll(categoryId =>
        {
            var category = Categories.FirstOrDefault(item =>
                string.Equals(item.Id, categoryId, StringComparison.OrdinalIgnoreCase));
            return category is not null && IsArchiveCategory(category);
        });
        task.CategoryIds.RemoveAll(categoryId =>
            string.Equals(categoryId, openCategory.Id, StringComparison.OrdinalIgnoreCase));
        task.CategoryIds.Insert(0, openCategory.Id);
        task.UpdatedAt = DateTime.Now;

        SaveTaskAndQueueIpadSnapshot(task);
        UpdateTaskCategoryPresentation(task);
        RefreshTaskCategories();
        SelectCategoryAndTask(openCategory, task);
        LoadTaskDetails();
        RefreshGlobalSearchResults();
        UpdateCategoryCounts();
        CategoryMessage = "Auftrag wurde in Offene Aufgaben zurückgeholt.";
    }

    private static string FormatArchiveTaskTitle(TaskItem task)
    {
        var customer = task.CustomerName?.Trim();
        var title = task.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(customer) && !string.IsNullOrWhiteSpace(title))
        {
            return $"{customer} - {title}";
        }

        return !string.IsNullOrWhiteSpace(customer)
            ? customer
            : !string.IsNullOrWhiteSpace(title)
                ? title
                : "Ohne Titel";
    }

    private static string FormatArchiveDateTime(DateTime? value)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return value.Value.TimeOfDay == TimeSpan.Zero
            ? FormatDateShort(value)
            : value.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));
    }

    private void RemoveCategory(CategoryItem category)
    {
        if (IsSpecialCategory(category))
        {
            CategoryMessage = "Diese Kategorie kann nicht entfernt werden.";
            return;
        }

        if (AllTasks.Any(task => !task.IsDeleted && TaskBelongsToCategory(task, category.Id)))
        {
            CategoryMessage = "Kategorie enthält noch Aufgaben und wurde nicht entfernt.";
            return;
        }

        category.IsVisible = false;
        SaveCategoryAndQueueIpadSnapshot(category, "Kategorie entfernt");
        Categories.Remove(category);
        RefreshCategoryDependentViews();
        if (SelectedCategory is null ||
            string.Equals(SelectedCategory.Id, category.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedCategory = GetDefaultStartupCategory() ?? Categories.FirstOrDefault();
            ApplySelectedCategoryContent();
        }

        UpdateCategoryCounts();
        CategoryMessage = "Kategorie entfernt.";
    }

    private void RefreshCategoryDependentViews()
    {
        RebuildCategoryTreeViews();
        RefreshTaskCategories();
        foreach (var task in AllTasks)
        {
            UpdateTaskCategoryPresentation(task);
        }

        RefreshGlobalSearchResults();
    }

    private void ToggleCategoryExpanded_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: CategoryItem category } ||
            !category.HasChildren ||
            !e.GetCurrentPoint((Control)sender).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ToggleCategoryExpanded(category);
        e.Handled = true;
    }

    private void CategoryItemFrame_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is StyledElement source && source.Classes.Contains("CategoryExpandGlyph"))
        {
            return;
        }

        if (sender is not Control { DataContext: CategoryItem category } ||
            !category.HasChildren ||
            !string.IsNullOrWhiteSpace(category.ParentId))
        {
            return;
        }

        _categoryDragCandidate = null;
        _categoryDragStartEvent = null;
        ToggleCategoryExpanded(category);
        e.Handled = true;
    }

    private void ToggleCategoryExpanded(CategoryItem category)
    {
        if (!category.HasChildren)
        {
            return;
        }

        var selectedCategory = SelectedCategory;
        var wasSuppressingCategorySelection = _suppressCategorySelectionChanged;

        _suppressCategorySelectionChanged = true;
        try
        {
            category.IsExpanded = !category.IsExpanded;
            RebuildCategoryTreeViews();

            if (selectedCategory is not null && SidebarCategories.Contains(selectedCategory))
            {
                SelectedCategory = selectedCategory;
                if (CategoryList is not null)
                {
                    CategoryList.SelectedItem = selectedCategory;
                }
            }
        }
        finally
        {
            _suppressCategorySelectionChanged = wasSuppressingCategorySelection;
        }

        ApplySelectedCategoryContent();
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
            CustomerEmail = source.CustomerEmail,
            CustomerPhone = source.CustomerPhone,
            Title = source.Title.StartsWith("Kopie - ", StringComparison.OrdinalIgnoreCase) ? source.Title : $"Kopie - {source.Title}",
            Description = source.Description,
            CategoryId = source.CategoryId,
            Status = source.Status,
            Priority = source.Priority,
            DueDate = source.DueDate,
            FollowUpDate = source.FollowUpDate,
            SentAt = source.SentAt,
            MaterialOrderedAt = source.MaterialOrderedAt,
            AssignedTo = source.AssignedTo,
            Technician = source.Technician,
            SortPosition = _repository.GetTopTaskSortPosition(source.CategoryId),
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = source.Status == "Erledigt" ? now : null
        };

        SaveTaskAndQueueIpadSnapshot(copy);
        foreach (var material in Materials.Where(item => string.Equals(item.TaskId, source.Id, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var materialCopy = new MaterialItem
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
            };
            MarkMaterialDirty(materialCopy);
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
        SaveTaskAndQueueIpadSnapshot(SelectedTask);
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
        if (sender is not Control { DataContext: TaskItem task } control ||
            !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        NavigateToTask(task, fromGlobalSearch: false);
    }

    private void ConfirmFollowUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: TaskItem task } || !task.FollowUpDate.HasValue)
        {
            return;
        }

        var isSelectedTask = SelectedTask?.Id == task.Id;
        if (isSelectedTask)
        {
            CaptureTaskUndoState(task, preserveExistingSnapshot: true);
        }

        task.FollowUpDate = null;
        SaveTaskAndQueueIpadSnapshot(task);
        RefreshDashboard();

        if (!IsOverviewSelected)
        {
            RefreshVisibleTasks();
        }

        if (isSelectedTask)
        {
            UpdateDateTextFieldsFromSelectedTask();
        }
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

    private void DueTimeTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyDueTimeText();
    }

    private void FollowUpDateTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyFollowUpDateText();
    }

    private void SentAtTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplySentAtText();
    }

    private void MaterialOrderedAtTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        ApplyMaterialOrderedAtText();
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
        if ((e.Key == Key.Back || e.Key == Key.Delete) &&
            sender is TextBox textBox &&
            !string.Equals(textBox.Tag as string, "DueTime", StringComparison.Ordinal))
        {
            textBox.Text = string.Empty;
            textBox.CaretIndex = 0;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        switch ((sender as Control)?.Tag as string)
        {
            case "DueDate":
                ApplyDueDateText();
                break;
            case "DueTime":
                ApplyDueTimeText();
                break;
            case "FollowUpDate":
                ApplyFollowUpDateText();
                break;
            case "SentAt":
                ApplySentAtText();
                break;
            case "MaterialOrderedAt":
                ApplyMaterialOrderedAtText();
                break;
        }

        e.Handled = true;
    }

    private async void CategoryRenameFromMenu_OnClick(object? sender, RoutedEventArgs e)
    {
        await RenameCategoryFromSenderAsync(sender, e);
    }

    private async void CategoryRenameFromSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        await RenameCategoryFromSenderAsync(sender, e);
    }

    private async Task RenameCategoryFromSenderAsync(object? sender, RoutedEventArgs e)
    {
        if (GetCategoryFromSender(sender) is not { } category)
        {
            return;
        }

        if (IsSpecialCategory(category))
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

        RenameCategory(category, NormalizeCategoryName(newName));
    }

    private void CategoryRemoveFromSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        if (GetCategoryFromSender(sender) is not { } category)
        {
            return;
        }

        RemoveCategory(category);
    }

    private static CategoryItem? GetCategoryFromSender(object? sender)
    {
        return sender switch
        {
            Control { DataContext: CategoryItem category } => category,
            _ => null
        };
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
        if (GetCategoryFromSender(sender) is { } category && !IsSpecialCategory(category))
        {
            SelectedCategory = category;
            CategoryEditorName = category.Name;
            RemoveCategory(category);
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

    private static string ResolveOneDriveEditDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TryGetDefaultOneDriveEditDirectory(createIfMissing: false, out var defaultDirectory)
                ? defaultDirectory
                : string.Empty;
        }

        var trimmedPath = Environment.ExpandEnvironmentVariables(path.Trim());
        if (!OperatingSystem.IsMacOS())
        {
            return GetOneDriveEditWorkDirectory(trimmedPath);
        }

        // Windows bleibt das produktive Zielsystem; auf macOS nur Legacy-Windows-Pfade
        // fuer lokale Entwicklung und Tests auf den passenden CloudStorage-Pfad umbiegen.
        if (!IsWindowsStylePath(trimmedPath))
        {
            return GetOneDriveEditWorkDirectory(trimmedPath);
        }

        if (!IsLegacyOneDriveEditDirectory(trimmedPath))
        {
            return GetOneDriveEditWorkDirectory(trimmedPath);
        }

        return GetOneDriveEditWorkDirectory(GetReplacementOneDriveEditDirectory(trimmedPath));
    }

    private void NormalizeConfiguredOneDriveEditDirectory()
    {
        var originalPath = _appSettings.OneDriveEditDirectory;
        if (TryGetMigratedOneDriveEditDirectory(originalPath, out var migratedPath, out var message))
        {
            _appSettings.OneDriveEditDirectory = migratedPath;
            _settingsService.Save(_appSettings);
            ReportOneDriveDataFolderMessage(message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            ReportOneDriveDataFolderMessage(message);
        }
    }

    private void ReportOneDriveDataFolderMessage(string message)
    {
        Console.WriteLine(message);
        _ipadSnapshotExportService.LogDiagnostic(message);
    }

    private static bool TryGetMigratedOneDriveEditDirectory(string? configuredPath, out string migratedPath, out string message)
    {
        migratedPath = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!TryGetDefaultOneDriveEditDirectory(createIfMissing: false, out var defaultDirectory))
            {
                return false;
            }

            migratedPath = EnsureTrailingDirectorySeparator(defaultDirectory);
            message = $"zentraler Standard-Datenordner erkannt: {migratedPath}";
            return true;
        }

        var trimmedPath = GetOneDriveEditWorkDirectory(Environment.ExpandEnvironmentVariables(configuredPath.Trim()));
        if (!IsLegacyOneDriveEditDirectory(trimmedPath))
        {
            return false;
        }

        var replacementPath = GetReplacementOneDriveEditDirectory(trimmedPath);
        var oldDirectory = ResolveDisplayDirectory(GetLegacyOneDriveEditDirectoryForCurrentPlatform(trimmedPath));
        var newDirectory = ResolveDisplayDirectory(replacementPath);
        var newExists = Directory.Exists(newDirectory);
        var oldExists = Directory.Exists(oldDirectory);

        if (newExists || CanSafelyCreateNewDataDirectory(oldDirectory, newDirectory, oldExists))
        {
            if (!newExists)
            {
                Directory.CreateDirectory(newDirectory);
            }

            migratedPath = EnsureTrailingDirectorySeparator(replacementPath);
            message =
                $"alter Datenordner erkannt: {oldDirectory}{Environment.NewLine}" +
                $"neuer Datenordner: {newDirectory}{Environment.NewLine}" +
                "lokale Einstellung wurde auf den neuen zentralen Datenordner umgestellt.";
            return true;
        }

        message =
            $"alter Datenordner erkannt: {oldDirectory}{Environment.NewLine}" +
            $"neuer Datenordner: {newDirectory}{Environment.NewLine}" +
            "automatische Umstellung nicht sicher: bitte scripts/migrate-data-folder.sh --dry-run ausfuehren.";
        return false;
    }

    private static bool CanSafelyCreateNewDataDirectory(string oldDirectory, string newDirectory, bool oldExists)
    {
        if (Directory.Exists(newDirectory))
        {
            return true;
        }

        var parentDirectory = Path.GetDirectoryName(newDirectory);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            return false;
        }

        return !oldExists || !ContainsSyncData(oldDirectory);
    }

    private static bool ContainsSyncData(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var syncDirectory = Path.Combine(directory, "Sync");
        return File.Exists(Path.Combine(syncDirectory, "live.bclive")) ||
               File.Exists(Path.Combine(syncDirectory, "live", "tasks.json")) ||
               File.Exists(Path.Combine(syncDirectory, "live", "categories.json")) ||
               File.Exists(Path.Combine(syncDirectory, "live", "metadata.json")) ||
               File.Exists(Path.Combine(syncDirectory, "snapshots", "latest.bcsnapshot"));
    }

    private static string GetPersistedOneDriveEditDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = GetOneDriveEditWorkDirectory(Environment.ExpandEnvironmentVariables(path.Trim()));
        if (!OperatingSystem.IsMacOS())
        {
            return trimmedPath;
        }

        // Windows bleibt das produktive Zielsystem; wenn auf macOS der bekannte
        // lokale CloudStorage-Testpfad gewaehlt wird, bleibt in der gemeinsamen
        // settings.json trotzdem der produktive Windows-Pfad erhalten.
        return IsKnownMacDevelopmentOneDriveEditDirectory(trimmedPath)
            ? GetProductiveWindowsOneDriveEditDirectory()
            : trimmedPath;
    }

    private static string GetOneDriveEditWorkDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var candidate = path.Trim();
        if (IsIpadLiveFilePath(candidate))
        {
            candidate = GetIpadLiveFileDirectory(candidate);
        }

        var syncRoot = IpadSnapshotExportService.ResolveSyncRootDirectory(candidate);
        if (string.IsNullOrWhiteSpace(syncRoot) || !AreSameResolvedDirectory(candidate, syncRoot))
        {
            return candidate;
        }

        var parentDirectory = GetIpadLiveFileDirectory(candidate);
        return string.IsNullOrWhiteSpace(parentDirectory) ? candidate : parentDirectory;
    }

    private static string ResolveIpadLiveFileTargetFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = Environment.ExpandEnvironmentVariables(path.Trim());
        return IsIpadLiveFilePath(trimmedPath)
            ? GetIpadLiveFileDirectory(trimmedPath)
            : trimmedPath;
    }

    private static string BuildIpadLiveFileTargetPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        return Path.Combine(folderPath, "live.bclive");
    }

    private static bool IsIpadLiveFilePath(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        return string.Equals(Path.GetFileName(normalizedPath), "live.bclive", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetIpadLiveFileDirectory(string path)
    {
        if (path.Contains('\\', StringComparison.Ordinal) && !path.Contains('/', StringComparison.Ordinal))
        {
            var separatorIndex = path.LastIndexOf('\\');
            return separatorIndex > 0 ? path[..separatorIndex] : string.Empty;
        }

        return Path.GetDirectoryName(path) ?? string.Empty;
    }

    private static IReadOnlyList<string> GetRecommendedIpadLiveFolders()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return Array.Empty<string>();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new[]
            {
                Path.Combine(GetMacDevelopmentOneDriveEditDirectory(), "Sync")
            };
        }

        var folders = new List<string>();
        foreach (var candidate in GetDefaultOneDriveEditDirectoryCandidates())
        {
            folders.Add(Path.Combine(candidate, "Sync"));
        }

        return folders;
    }

    private static string GetMacDevelopmentOneDriveEditDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "CloudStorage",
            CompanyOneDriveMacFolderName,
            "Dokumente",
            OneDriveDataFolderName);
    }

    private static string GetLegacyMacDevelopmentOneDriveEditDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "CloudStorage",
            CompanyOneDriveMacFolderName,
            "Dokumente",
            LegacyOneDriveDataFolderName);
    }

    private static string GetProductiveWindowsOneDriveEditDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(@"C:\Users\Installation", CompanyOneDriveWindowsFolderName, "Dokumente", OneDriveDataFolderName);
        }

        return Path.Combine(userProfile, CompanyOneDriveWindowsFolderName, "Dokumente", OneDriveDataFolderName);
    }

    private static bool TryGetDefaultOneDriveEditDirectory(bool createIfMissing, out string directory)
    {
        directory = string.Empty;
        foreach (var candidate in GetDefaultOneDriveEditDirectoryCandidates())
        {
            if (Directory.Exists(candidate))
            {
                directory = candidate;
                return true;
            }

            var parentDirectory = Path.GetDirectoryName(candidate);
            if (!createIfMissing || string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                continue;
            }

            Directory.CreateDirectory(candidate);
            directory = candidate;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> GetDefaultOneDriveEditDirectoryCandidates()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return Array.Empty<string>();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new[]
            {
                GetMacDevelopmentOneDriveEditDirectory()
            };
        }

        if (OperatingSystem.IsWindows())
        {
            var candidates = new List<string>();
            var oneDriveCommercial = Environment.GetEnvironmentVariable("OneDriveCommercial");
            if (!string.IsNullOrWhiteSpace(oneDriveCommercial))
            {
                candidates.Add(Path.Combine(oneDriveCommercial, "Dokumente", OneDriveDataFolderName));
                candidates.Add(Path.Combine(oneDriveCommercial, "Documents", OneDriveDataFolderName));
            }

            candidates.Add(Path.Combine(userProfile, CompanyOneDriveWindowsFolderName, "Dokumente", OneDriveDataFolderName));
            candidates.Add(Path.Combine(userProfile, CompanyOneDriveWindowsFolderName, "Documents", OneDriveDataFolderName));
            return candidates;
        }

        return Array.Empty<string>();
    }

    private static bool IsWindowsStylePath(string path)
    {
        return (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') ||
               path.Contains('\\', StringComparison.Ordinal) ||
               path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static bool IsLegacyOneDriveEditDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        return string.Equals(GetPortableFileName(normalized), LegacyOneDriveDataFolderName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPortableFileName(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }

    private static bool IsKnownMacDevelopmentOneDriveEditDirectory(string path)
    {
        return AreSameResolvedDirectory(path, GetMacDevelopmentOneDriveEditDirectory());
    }

    private static string ReplaceLegacyDataFolderName(string path)
    {
        var workDirectory = GetOneDriveEditWorkDirectory(path);
        var normalized = workDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderName = GetPortableFileName(normalized);
        if (!string.Equals(folderName, LegacyOneDriveDataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return workDirectory;
        }

        var parentDirectory = GetIpadLiveFileDirectory(normalized);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return OneDriveDataFolderName;
        }

        var separator = workDirectory.Contains('\\', StringComparison.Ordinal) && !workDirectory.Contains('/', StringComparison.Ordinal)
            ? "\\"
            : Path.DirectorySeparatorChar.ToString();
        return parentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\') + separator + OneDriveDataFolderName;
    }

    private static string GetReplacementOneDriveEditDirectory(string path)
    {
        if (OperatingSystem.IsMacOS() && IsWindowsStylePath(path))
        {
            return GetMacDevelopmentOneDriveEditDirectory();
        }

        return ReplaceLegacyDataFolderName(path);
    }

    private static string GetLegacyOneDriveEditDirectoryForCurrentPlatform(string path)
    {
        if (OperatingSystem.IsMacOS() && IsWindowsStylePath(path))
        {
            return GetLegacyMacDevelopmentOneDriveEditDirectory();
        }

        return GetOneDriveEditWorkDirectory(path);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.EndsWith(Path.DirectorySeparatorChar) ||
            path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
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
                MarkDeskItemDirty(deskItem);
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
                    MarkAttachmentDirty(attachment);
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

    private void LocalNetworkSyncDeviceName_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            LocalNetworkSyncDeviceNameInput = textBox.Text ?? string.Empty;
        }
    }

    private void LocalNetworkSyncPort_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            LocalNetworkSyncPortInput = textBox.Text ?? string.Empty;
        }
    }

    private void SaveLocalNetworkSyncSettings_OnClick(object? sender, RoutedEventArgs e)
    {
        var trimmedDeviceName = LocalNetworkSyncDeviceNameInput.Trim();
        var trimmedPort = LocalNetworkSyncPortInput.Trim();
        if (!TryParseLocalNetworkSyncPort(trimmedPort, out var port))
        {
            LocalNetworkSyncSettingsStatus = "Port ungültig. Erlaubt sind leer, 0 oder 1024 bis 65535.";
            return;
        }

        _appSettings.LocalNetworkSyncEnabled = false;
        _appSettings.LocalNetworkSyncDeviceName = trimmedDeviceName;
        _appSettings.LocalNetworkSyncPort = port <= 0 ? DefaultLocalNetworkSyncPort : port;
        EnsureLocalNetworkSyncLocalSettings(saveIfChanged: false);
        _settingsService.Save(_appSettings);
        RefreshLocalNetworkSyncEditorFields();
        RefreshLocalNetworkSyncDisplayProperties();
        LocalNetworkSyncSettingsStatus = "Lokale Netzwerk-Sync-Einstellungen gespeichert. Der Testdienst startet nur manuell; es wird keine Datenübertragung aktiviert.";
    }

    private async void StartLocalNetworkSyncTestService_OnClick(object? sender, RoutedEventArgs e)
    {
        var port = EnsureLocalNetworkSyncDefaultPort();
        if (port < 1024 || port > 65535)
        {
            LocalNetworkSyncTestServiceStatus = "Testdienst kann nicht starten: Port ungültig.";
            return;
        }

        await StopLocalNetworkSyncTestServiceAsync();

        _localNetworkSyncTestService = new LocalSyncService(new LocalSyncOptions
        {
            Enabled = true,
            Port = port,
            DeviceName = _appSettings.LocalNetworkSyncDeviceName,
            DeviceId = _appSettings.LocalNetworkSyncDeviceId,
            AppVersion = _updateService.GetCurrentVersion()
        }, _localNetworkDeviceStore);
        _localNetworkSyncTestService.DeviceRemembered += LocalNetworkSyncTestService_DeviceRemembered;

        var status = await _localNetworkSyncTestService.StartAsync();
        LocalNetworkSyncTestServiceStatus = status.Message?.StartsWith("Running:", StringComparison.Ordinal) == true
            ? FormatLocalNetworkSyncStatusMessage(status.Message)
            : $"Fehler: Port {port} ist belegt oder nicht verfügbar. {status.Message}";
    }

    private static string FormatLocalNetworkSyncStatusMessage(string? statusMessage)
    {
        const string runningPrefix = "Running: ";
        var message = string.IsNullOrWhiteSpace(statusMessage)
            ? string.Empty
            : statusMessage.Trim();

        if (message.StartsWith(runningPrefix, StringComparison.Ordinal))
        {
            message = message[runningPrefix.Length..];
        }

        return message
            .Replace("laeuft", "läuft", StringComparison.Ordinal)
            .Replace("verfuegbar", "verfügbar", StringComparison.Ordinal)
            .Replace("Ankuendigung", "Ankündigung", StringComparison.Ordinal)
            .Replace("fuer", "für", StringComparison.Ordinal);
    }

    private async void StopLocalNetworkSyncTestService_OnClick(object? sender, RoutedEventArgs e)
    {
        await StopLocalNetworkSyncTestServiceAsync();
    }

    private void StopLocalNetworkSyncTestService()
    {
        if (_localNetworkSyncTestService is null)
        {
            return;
        }

        _localNetworkSyncTestService.StopAsync().GetAwaiter().GetResult();
        _localNetworkSyncTestService.DeviceRemembered -= LocalNetworkSyncTestService_DeviceRemembered;
        _localNetworkSyncTestService = null;
        LocalNetworkSyncTestServiceStatus = "Testdienst gestoppt";
    }

    private async Task StopLocalNetworkSyncTestServiceAsync()
    {
        if (_localNetworkSyncTestService is null)
        {
            LocalNetworkSyncTestServiceStatus = "Testdienst gestoppt";
            return;
        }

        await _localNetworkSyncTestService.StopAsync();
        _localNetworkSyncTestService.DeviceRemembered -= LocalNetworkSyncTestService_DeviceRemembered;
        _localNetworkSyncTestService = null;
        LocalNetworkSyncTestServiceStatus = "Testdienst gestoppt";
    }

    private static bool TryParseLocalNetworkSyncPort(string portText, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(portText) || portText == "0")
        {
            return true;
        }

        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort))
        {
            return false;
        }

        if (parsedPort < 1024 || parsedPort > 65535)
        {
            return false;
        }

        port = parsedPort;
        return true;
    }

    private int EnsureLocalNetworkSyncDefaultPort()
    {
        if (_appSettings.LocalNetworkSyncPort >= 1024 && _appSettings.LocalNetworkSyncPort <= 65535)
        {
            return _appSettings.LocalNetworkSyncPort;
        }

        _appSettings.LocalNetworkSyncPort = DefaultLocalNetworkSyncPort;
        _settingsService.Save(_appSettings);
        RefreshLocalNetworkSyncEditorFields();
        RefreshLocalNetworkSyncDisplayProperties();
        return _appSettings.LocalNetworkSyncPort;
    }

    private void RefreshLocalNetworkSyncEditorFields()
    {
        LocalNetworkSyncDeviceNameInput = _appSettings.LocalNetworkSyncDeviceName.Trim();
        LocalNetworkSyncPortInput = _appSettings.LocalNetworkSyncPort > 0
            ? _appSettings.LocalNetworkSyncPort.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private void RefreshLocalNetworkSyncDisplayProperties()
    {
        OnPropertyChanged(nameof(LocalNetworkSyncStatusText));
        OnPropertyChanged(nameof(LocalNetworkSyncBonjourStatusText));
        OnPropertyChanged(nameof(LocalNetworkSyncDeviceNameText));
        OnPropertyChanged(nameof(LocalNetworkSyncPortText));
        OnPropertyChanged(nameof(LocalNetworkSyncTestServiceAddressText));
        OnPropertyChanged(nameof(LocalNetworkSyncHintText));
        OnPropertyChanged(nameof(HasLocalNetworkRememberedDevices));
        OnPropertyChanged(nameof(HasNoLocalNetworkRememberedDevices));
    }

    private void LocalNetworkSyncTestService_DeviceRemembered(object? sender, LocalNetworkRememberedDevice e)
    {
        Dispatcher.UIThread.Post(LoadLocalNetworkRememberedDevices);
    }

    private void LoadLocalNetworkRememberedDevices()
    {
        LocalNetworkRememberedDevices.Clear();
        foreach (var device in _localNetworkDeviceStore.Load())
        {
            LocalNetworkRememberedDevices.Add(LocalNetworkRememberedDeviceListItem.FromDevice(device));
        }

        OnPropertyChanged(nameof(HasLocalNetworkRememberedDevices));
        OnPropertyChanged(nameof(HasNoLocalNetworkRememberedDevices));
    }

    private string BuildLocalNetworkSyncTestServiceAddressText()
    {
        var port = _appSettings.LocalNetworkSyncPort >= 1024 && _appSettings.LocalNetworkSyncPort <= 65535
            ? _appSettings.LocalNetworkSyncPort
            : DefaultLocalNetworkSyncPort;
        var addresses = GetLocalLanIpv4Addresses()
            .Select(address => $"http://{address}:{port}/local-sync/status")
            .ToList();

        if (addresses.Count == 0)
        {
            return $"Keine lokale IPv4-Adresse gefunden. Auf diesem Rechner: http://127.0.0.1:{port}/local-sync/status";
        }

        return string.Join(Environment.NewLine, addresses);
    }

    private static IEnumerable<string> GetLocalLanIpv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
                || networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            foreach (var addressInfo in properties.UnicastAddresses)
            {
                var address = addressInfo.Address;
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    yield return address.ToString();
                }
            }
        }
    }

    private void EnsureLocalNetworkSyncLocalSettings(bool saveIfChanged = true)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(_appSettings.LocalNetworkSyncDeviceId))
        {
            _appSettings.LocalNetworkSyncDeviceId = AppSettingsService.CreateLocalNetworkSyncDeviceId();
            changed = true;
        }

        if (_appSettings.LocalNetworkSyncPairedDevices is null)
        {
            _appSettings.LocalNetworkSyncPairedDevices = [];
            changed = true;
        }

        if (_appSettings.LocalNetworkSyncEnabled)
        {
            _appSettings.LocalNetworkSyncEnabled = false;
            changed = true;
        }

        if (changed && saveIfChanged)
        {
            _settingsService.Save(_appSettings);
        }
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
            Title = "Datenordner auswählen",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        _appSettings.OneDriveEditDirectory = GetPersistedOneDriveEditDirectory(folderPath);
        _settingsService.Save(_appSettings);
        OnPropertyChanged(nameof(OneDriveEditDirectory));
        OnPropertyChanged(nameof(HasOneDriveEditDirectory));
        OnPropertyChanged(nameof(HasNoOneDriveEditDirectory));
        OnPropertyChanged(nameof(IpadSyncRootDirectory));
        OnPropertyChanged(nameof(HasIpadSyncRootDirectory));
        OnPropertyChanged(nameof(HasNoIpadSyncRootDirectory));
        LoadTechnicianOptions();
        BackupStatus = $"Datenordner gesetzt: {folderPath}";
        TriggerIpadSnapshotExport("folder-selected");
    }

    private void OpenOneDriveFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!HasOneDriveEditDirectory)
        {
            BackupStatus = "Noch kein Datenordner gewählt.";
            return;
        }

        OpenFolder(OneDriveEditDirectory);
    }

    private async void SelectIpadLiveFileTarget_OnClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            IpadLiveFileStatus = "Fehler beim Schreiben der Live-Datei: Ordnerauswahl ist nicht verfügbar.";
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Zielordner für live.bclive auswählen",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        _appSettings.IpadLiveFileTargetPath = folderPath;
        _settingsService.Save(_appSettings);
        RefreshIpadLiveFileTargetProperties();
        IpadLiveFileStatus = $"Zielordner gesetzt: {folderPath}";
        TriggerIpadLiveFileExport("live-file-target-selected");
    }

    private void WriteIpadLiveFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!HasIpadLiveFileTargetPath)
        {
            IpadLiveFileStatus = "Kein Zielordner eingerichtet. Bitte zuerst einen Ordner auswählen.";
            return;
        }

        TriggerIpadLiveFileExport("manual-live-file-write");
    }

    private void CheckRecommendedIpadLiveFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var recommendedFolder = GetRecommendedIpadLiveFolders()
            .FirstOrDefault(Directory.Exists);
        if (string.IsNullOrWhiteSpace(recommendedFolder))
        {
            IpadLiveFileStatus = "Der Legacy-Sync-Ordner wurde auf diesem Rechner noch nicht gefunden. Bitte zuerst den Datenordner migrieren oder auswählen.";
            return;
        }

        _appSettings.IpadLiveFileTargetPath = recommendedFolder;
        _settingsService.Save(_appSettings);
        RefreshIpadLiveFileTargetProperties();
        IpadLiveFileStatus = $"Legacy-Sync-Ordner gefunden und gesetzt: {recommendedFolder}";
    }

    private void TriggerIpadSnapshotExport(string reason)
    {
        _ipadSnapshotExportService.LogDiagnostic($"Legacy iPad snapshot export trigger ignored ({reason}): local network sync is the target path.");
    }

    private async Task RunIpadSnapshotExportAsync(string reason)
    {
        IsIpadSnapshotExportRunning = true;
        SetIpadSnapshotStatus("iPad-Sync wird aktualisiert …");

        try
        {
            IpadSnapshotExportService.SnapshotExportResult result;
            do
            {
                Interlocked.Exchange(ref _isPendingIpadSnapshotExport, 0);
                Debug.WriteLine($"iPad live sync started ({reason}): {OneDriveEditDirectory}");
                result = await _ipadSnapshotExportService.ExportLiveNowAsync(
                    _repository,
                    OneDriveEditDirectory,
                    _updateService.GetCurrentVersion(),
                    Environment.MachineName);
            }
            while (result.Success && Interlocked.Exchange(ref _isPendingIpadSnapshotExport, 0) == 1);

            SetIpadSnapshotStatus(
                result.Success
                    ? "iPad-Sync erfolgreich aktualisiert."
                    : "iPad-Sync konnte nicht aktualisiert werden. Details siehe Log.",
                autoHide: result.Success);
        }
        catch (Exception ex)
        {
            SetIpadSnapshotStatus("iPad-Sync konnte nicht aktualisiert werden. Details siehe Log.");
            Debug.WriteLine($"iPad live sync failed ({reason}): {ex}");
        }
        finally
        {
            IsIpadSnapshotExportRunning = false;
            Interlocked.Exchange(ref _isRunningIpadSnapshotExport, 0);
            if (Interlocked.Exchange(ref _isPendingIpadSnapshotExport, 0) == 1)
            {
                TriggerIpadSnapshotExport("pending-write");
            }
        }
    }

    private void Repository_OnDataWritten(string reason)
    {
        if (_isLoadingData)
        {
            _ipadSnapshotExportService.LogDiagnostic($"iPad snapshot export skipped ({reason}): app is loading data.");
            return;
        }

        if (_suppressRepositoryExportsDuringSave)
        {
            _repositoryDataWrittenDuringSave = true;
            _ipadSnapshotExportService.LogDiagnostic($"iPad snapshot export deferred ({reason}): save is running.");
            return;
        }

        TriggerIpadSnapshotExport(reason);
        TriggerIpadLiveFileExport(reason);
    }

    private void TriggerIpadLiveFileExport(string reason)
    {
        _ipadSnapshotExportService.LogDiagnostic($"Legacy iPad live file export trigger ignored ({reason}): local network sync is the target path.");
    }

    private async Task RunIpadLiveFileExportAsync(string reason)
    {
        IpadLiveFileStatus = "Live-Datei wird geschrieben …";

        try
        {
            IpadSnapshotExportService.SnapshotExportResult result;
            do
            {
                Interlocked.Exchange(ref _isPendingIpadLiveFileExport, 0);
                Debug.WriteLine($"iPad live file export started ({reason}): {IpadLiveFileTargetPath}");
                result = await _ipadSnapshotExportService.ExportLivePackageToFileAsync(
                    _repository,
                    IpadLiveFileTargetPath,
                    _updateService.GetCurrentVersion(),
                    Environment.MachineName);
            }
            while (result.Success && Interlocked.Exchange(ref _isPendingIpadLiveFileExport, 0) == 1);

            if (result.Success)
            {
                IpadLiveFileLastSuccessfulExport = $"Letzter erfolgreicher Exportzeitpunkt: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                IpadLiveFileLastError = string.Empty;
                IpadLiveFileStatus = $"Live-Datei geschrieben: {IpadLiveFileTargetPath}";
            }
            else
            {
                var errorMessage = result.ErrorMessage ?? "Details siehe Log.";
                IpadLiveFileLastError = $"Letzte Fehlermeldung: {errorMessage}";
                IpadLiveFileStatus = $"Fehler beim Schreiben der Live-Datei: {errorMessage}";
            }
        }
        catch (Exception ex)
        {
            IpadLiveFileStatus = "Fehler beim Schreiben der Live-Datei: Details siehe Log.";
            IpadLiveFileLastError = "Letzte Fehlermeldung: Details siehe Log.";
            Debug.WriteLine($"iPad live file export failed ({reason}): {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunningIpadLiveFileExport, 0);
            if (Interlocked.Exchange(ref _isPendingIpadLiveFileExport, 0) == 1)
            {
                TriggerIpadLiveFileExport("pending-live-file-write");
            }
        }
    }

    private void RefreshIpadLiveFileTargetProperties()
    {
        OnPropertyChanged(nameof(IpadLiveFileTargetFolder));
        OnPropertyChanged(nameof(IpadLiveFileTargetPath));
        OnPropertyChanged(nameof(HasIpadLiveFileTargetPath));
        OnPropertyChanged(nameof(HasNoIpadLiveFileTargetPath));
    }

    private void SaveTaskAndQueueIpadSnapshot(TaskItem task)
    {
        MarkTaskDirty(task);
    }

    private void SaveCategoryAndQueueIpadSnapshot(CategoryItem category, string? reason = null)
    {
        if (_isSavingCategorySnapshot)
        {
            return;
        }

        _isSavingCategorySnapshot = true;
        try
        {
            MarkCategoryDirty(category, reason);
        }
        finally
        {
            _isSavingCategorySnapshot = false;
        }
    }

    private void SaveAttachmentAndQueueIpadSnapshot(AttachmentItem attachment)
    {
        MarkAttachmentDirty(attachment);
    }

    private void DeleteTaskAndQueueIpadSnapshot(string taskId)
    {
        _dirtyTasks.Remove(taskId);
        foreach (var materialId in _dirtyMaterials
                     .Where(item => string.Equals(item.Value.TaskId, taskId, StringComparison.OrdinalIgnoreCase))
                     .Select(item => item.Key)
                     .ToList())
        {
            _dirtyMaterials.Remove(materialId);
        }

        _repository.DeleteTask(taskId);
    }

    private void DeleteAttachmentAndQueueIpadSnapshot(string attachmentId)
    {
        _dirtyAttachments.Remove(attachmentId);
        _deletedAttachments.Add(attachmentId);
        MarkDataDirty($"attachment-delete:{attachmentId}");
    }

    private void EmptyTrashAndQueueIpadSnapshot()
    {
        _emptyTrashRequested = true;
        MarkDataDirty("empty-trash");
    }

    private async void RefreshIpadSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!HasOneDriveEditDirectory)
        {
            SetIpadSnapshotStatus("Noch kein Datenordner gewählt.");
            return;
        }

        try
        {
            IsIpadSnapshotExportRunning = true;
            SetIpadSnapshotStatus("iPad-Sync wird aktualisiert …");
            var result = await _ipadSnapshotExportService.ExportNowAsync(
                _repository,
                OneDriveEditDirectory,
                _updateService.GetCurrentVersion(),
                Environment.MachineName);

            SetIpadSnapshotStatus(
                result.Success
                    ? "iPad-Sync erfolgreich aktualisiert. Vollsnapshot wurde ebenfalls erstellt."
                    : "iPad-Sync konnte nicht aktualisiert werden. Details siehe Log.",
                autoHide: result.Success);
        }
        catch (Exception ex)
        {
            SetIpadSnapshotStatus("iPad-Sync konnte nicht aktualisiert werden. Details siehe Log.");
            Debug.WriteLine($"Manual iPad snapshot export failed: {ex}");
        }
        finally
        {
            IsIpadSnapshotExportRunning = false;
        }
    }

    private void SetIpadSnapshotStatus(string status, bool autoHide = false)
    {
        var previousCts = _ipadSnapshotStatusHideCts;
        _ipadSnapshotStatusHideCts = null;
        previousCts?.Cancel();
        previousCts?.Dispose();

        IpadSnapshotStatus = status;
        if (!autoHide)
        {
            return;
        }

        var currentCts = new CancellationTokenSource();
        _ipadSnapshotStatusHideCts = currentCts;
        _ = HideSuccessfulIpadSnapshotStatusAsync(currentCts);
    }

    private async Task HideSuccessfulIpadSnapshotStatusAsync(CancellationTokenSource currentCts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), currentCts.Token);
            if (ReferenceEquals(_ipadSnapshotStatusHideCts, currentCts))
            {
                _ipadSnapshotStatusHideCts = null;
                IpadSnapshotStatus = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            // Eine neuere Statusmeldung bleibt sichtbar.
        }
        finally
        {
            currentCts.Dispose();
        }
    }

    private async void CreateBackup_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await Task.Run(() => _backupService.CreateBackup());
            LastBackupPath = result.BackupPath;
            LastBackupTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            BackupStatus = "Backup jetzt erstellt.";
            LoadBackupEntries();
        }
        catch (Exception ex)
        {
            BackupStatus = "Backup konnte nicht erstellt werden.";
            Debug.WriteLine($"Backup failed: {ex}");
        }
    }

    private async void RestoreBackup_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: BackupListItem backup })
        {
            return;
        }

        if (!await ShowRestoreBackupConfirmationDialogAsync(backup))
        {
            return;
        }

        if (!CanReadBackupFile(backup.FilePath, out var validationError))
        {
            BackupStatus = validationError;
            LoadBackupEntries();
            return;
        }

        var selectedCategoryId = SelectedCategory?.Id;
        var selectedTaskId = SelectedTask?.Id;

        try
        {
            if (SelectedTask is not null && !IsUnsavedPlaceholderTask(SelectedTask))
            {
                SaveTaskAndQueueIpadSnapshot(SelectedTask);
                if (!SaveNow("backup-restore-before-safety-backup"))
                {
                    return;
                }
            }

            BackupResult safetyBackup;
            try
            {
                safetyBackup = await Task.Run(() => _backupService.CreateBackup());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Safety backup before restore failed: {ex}");
                BackupStatus = "Wiederherstellung abgebrochen: Sicherheitsbackup des aktuellen Stands konnte nicht erstellt werden.";
                return;
            }

            if (safetyBackup.SkippedFiles > 0)
            {
                BackupStatus = "Wiederherstellung abgebrochen: Das Sicherheitsbackup des aktuellen Stands war unvollständig.";
                return;
            }

            LastBackupPath = safetyBackup.BackupPath;
            LastBackupTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            ClearSelectedTask();
            SqliteConnection.ClearAllPools();
            await Task.Run(() => RestoreBackupDatabase(backup.FilePath));
            SqliteConnection.ClearAllPools();

            try
            {
                _repository.Initialize();
                LoadData(selectedCategoryId, selectedTaskId);
                RefreshGlobalSearchResults();
                BackupStatus = "Backup wurde wiederhergestellt. Der Datenstand wurde neu geladen. Ein Neustart ist nicht erforderlich.";
            }
            catch (Exception reloadEx)
            {
                Debug.WriteLine($"Backup restore reload failed: {reloadEx}");
                BackupStatus = "Backup wurde wiederhergestellt. Bitte BüroCockpit neu starten, damit alle Daten vollständig neu geladen werden.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Backup restore failed: {ex}");
            BackupStatus = ex.Message;
        }
        finally
        {
            LoadBackupEntries();
        }
    }

    private void LoadBackupEntries()
    {
        AppPaths.EnsureBaseDirectories();
        BackupEntries.Clear();

        try
        {
            if (Directory.Exists(AppPaths.BackupDirectory))
            {
                foreach (var fileInfo in Directory.EnumerateFiles(AppPaths.BackupDirectory, "*.db")
                             .Select(path => new FileInfo(path))
                             .OrderByDescending(file => file.LastWriteTimeUtc)
                             .ThenByDescending(file => file.Length))
                {
                    BackupEntries.Add(CreateBackupListItem(fileInfo));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Backup list could not be loaded: {ex}");
        }

        OnPropertyChanged(nameof(HasBackupEntries));
        OnPropertyChanged(nameof(HasNoBackupEntries));
    }

    private static BackupListItem CreateBackupListItem(FileInfo fileInfo)
    {
        return new BackupListItem(
            fileInfo.FullName,
            fileInfo.Name,
            fileInfo.LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss"),
            FormatBackupFileSize(fileInfo.Length),
            string.Empty);
    }

    private static string FormatBackupFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{(long)size} {units[unitIndex]}"
            : $"{size:0.#} {units[unitIndex]}";
    }

    private async Task<bool> ShowRestoreBackupConfirmationDialogAsync(BackupListItem backup)
    {
        var result = false;
        var confirmButton = new Button
        {
            Content = "Ja, aktuellen Stand ersetzen",
            Classes = { "Primary" },
            MinWidth = 210
        };

        var cancelButton = new Button
        {
            Content = "Abbrechen",
            IsCancel = true,
            MinWidth = 100
        };

        var dialog = new Window
        {
            Title = "Backup wiederherstellen?",
            Width = 620,
            MinWidth = 500,
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
                            Text = "Der aktuelle Datenstand wird durch dieses Backup ersetzt.",
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"Ausgewählt: {backup.TimestampText} · {backup.SizeText}",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = backup.FileName,
                            Foreground = ResourceBrush("TextTertiaryBrush"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Vorher wird automatisch ein Sicherheitsbackup des aktuellen Stands erstellt. Anhänge und Dateien werden nicht gelöscht.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Margin = new Thickness(0, 8, 0, 0),
                            Children =
                            {
                                cancelButton,
                                confirmButton
                            }
                        }
                    }
                }
            }
        };

        confirmButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
    }

    private static void RestoreBackupDatabase(string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Das ausgewählte Backup wurde nicht gefunden.", backupPath);
        }

        AppPaths.EnsureBaseDirectories();
        var tempPath = Path.Combine(AppPaths.AppDataDirectory, $".restore-{Guid.NewGuid():N}.db");
        try
        {
            using var source = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
            target.Flush(flushToDisk: true);

            if (File.Exists(AppPaths.DatabasePath))
            {
                File.Replace(tempPath, AppPaths.DatabasePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, AppPaths.DatabasePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool CanReadBackupFile(string backupPath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!File.Exists(backupPath))
        {
            errorMessage = "Wiederherstellung abgebrochen: Das ausgewählte Backup wurde nicht gefunden.";
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length <= 0)
            {
                errorMessage = "Wiederherstellung abgebrochen: Das ausgewählte Backup ist leer.";
                return false;
            }

            using var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.CanRead;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Backup file could not be read: {ex}");
            errorMessage = "Wiederherstellung abgebrochen: Das ausgewählte Backup konnte nicht gelesen werden.";
            return false;
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
            AttachmentEditStatus = "Noch kein Datenordner gewählt.";
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

    private static bool TryShowFileInFolder(string path, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errorMessage = $"Datei nicht gefunden: {path}";
            return false;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return true;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { "-R", path },
                    UseShellExecute = false
                });
                return true;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return TryOpenExternalFile(directory, out errorMessage);
            }

            errorMessage = $"Ordner konnte nicht ermittelt werden: {path}";
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not show file in folder '{path}': {ex}");
            errorMessage = $"Datei konnte nicht im Ordner angezeigt werden: {path}";
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
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _searchSelectedTaskId = selectedTask.Id;
        }
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

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _searchSelectedTaskId = task.Id;
        }

        var category = GetTaskNavigationCategory(task, preferredCategoryId);
        if (category is null)
        {
            ClearSelectedTask();
            return;
        }

        ExpandCategoryAncestors(category);
        RebuildCategoryTreeViews();

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

    private void ExpandCategoryAncestors(CategoryItem category)
    {
        var current = category;
        while (!string.IsNullOrWhiteSpace(current.ParentId))
        {
            var parent = Categories.FirstOrDefault(item =>
                string.Equals(item.Id, current.ParentId, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
            {
                break;
            }

            parent.IsExpanded = true;
            current = parent;
        }
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
        var storedMaterialsById = Materials
            .Where(m => !string.IsNullOrWhiteSpace(m.TaskId))
            .Select(m => m.TaskId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(taskId => _repository.GetMaterials(taskId))
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var item in Materials.Where(m => !string.IsNullOrWhiteSpace(m.TaskId)))
        {
            if (!storedMaterialsById.TryGetValue(item.Id, out var storedMaterial) ||
                !MaterialMatchesStored(item, storedMaterial))
            {
                MarkMaterialDirty(item);
            }
        }
    }

    private static bool MaterialMatchesStored(MaterialItem current, MaterialItem stored)
    {
        return string.Equals(current.TaskId, stored.TaskId, StringComparison.OrdinalIgnoreCase) &&
               current.Quantity == stored.Quantity &&
               string.Equals(current.Unit, stored.Unit, StringComparison.Ordinal) &&
               string.Equals(current.Name, stored.Name, StringComparison.Ordinal) &&
               string.Equals(current.Status, stored.Status, StringComparison.Ordinal) &&
               string.Equals(current.Supplier, stored.Supplier, StringComparison.Ordinal) &&
               current.OrderedAt == stored.OrderedAt &&
               string.Equals(current.Note, stored.Note, StringComparison.Ordinal);
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
            _dirtyMaterials.Remove(duplicate.Id);
            _deletedMaterials.Add(duplicate.Id);
            MarkDataDirty($"material-delete:{duplicate.Id}");
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

    private bool IsInArchiveCategory(TaskItem task)
    {
        var archiveCategory = Categories.FirstOrDefault(category =>
            category.Name.Equals("Archiv", StringComparison.OrdinalIgnoreCase));
        return archiveCategory is not null && TaskBelongsToCategory(task, archiveCategory.Id);
    }

    private bool IsArchivedForSearch(TaskItem task)
    {
        return IsDoneOrArchived(task) || IsInArchiveCategory(task);
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
                SaveTaskAndQueueIpadSnapshot(duplicate.Task);
                RemovePendingNewTask(newTask);
                NavigateToTask(duplicate.Task, fromGlobalSearch: false);
                UpdateCategoryCounts();
                return true;

            case DuplicateTaskChoice.MoveToCategory:
                MoveTaskToCategory(duplicate.Task, targetCategory.Id);
                SaveTaskAndQueueIpadSnapshot(duplicate.Task);
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
                SaveTaskAndQueueIpadSnapshot(duplicate.Task);
                SelectCategoryAndTask(targetCategory, duplicate.Task);
                RefreshTaskCategorySelections();
                return true;

            case DuplicateTaskChoice.MoveToCategory:
                MoveTaskToCategory(duplicate.Task, targetCategory.Id);
                SaveTaskAndQueueIpadSnapshot(duplicate.Task);
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
        _dirtyTasks.Remove(task.Id);
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
        foreach (var category in GetOrderedTaskCategories())
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
            if (!IsSelectableTaskCategory(category))
            {
                continue;
            }

            TaskCategorySelections.Add(new TaskCategorySelection(
                category,
                TaskBelongsToCategory(SelectedTask, category.Id),
                isSelectable: true));
        }
    }

    private void RebuildCategoryTreeViews()
    {
        var normalCategories = Categories
            .Where(category => !IsSpecialCategory(category) && !IsLegacyMobileApprovalCategory(category.Name))
            .ToList();
        var byParent = normalCategories
            .GroupBy(category => category.ParentId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(category => category.SortOrder)
                    .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var visibleSidebarCategories = new List<CategoryItem>();
        void AddBranch(CategoryItem category, int level, string path, bool isVisibleInSidebar)
        {
            PrepareCategoryDisplay(category, level, string.IsNullOrWhiteSpace(path) ? category.Name : path, byParent.ContainsKey(category.Id));
            if (isVisibleInSidebar)
            {
                visibleSidebarCategories.Add(category);
            }

            if (!category.HasChildren)
            {
                return;
            }

            var childrenVisibleInSidebar = isVisibleInSidebar && category.IsExpanded;
            foreach (var child in byParent[category.Id])
            {
                AddBranch(child, level + 1, $"{category.SelectionName} / {child.Name}", childrenVisibleInSidebar);
            }
        }

        var rootCategories = normalCategories
            .Where(category => string.IsNullOrWhiteSpace(category.ParentId) || !normalCategories.Any(parent => string.Equals(parent.Id, category.ParentId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var category in rootCategories)
        {
            AddBranch(category, 0, category.Name, isVisibleInSidebar: true);
        }

        SidebarCategories.Clear();
        foreach (var category in Categories.Where(category =>
                     category.Id == OverviewCategoryId ||
                     (category.Id == DeskCategoryId && ShowDesktopSetting)))
        {
            PrepareCategoryDisplay(category, 0, category.Name, hasChildren: false);
            SidebarCategories.Add(category);
        }

        foreach (var category in visibleSidebarCategories.Where(category => !IsArchiveCategory(category)))
        {
            SidebarCategories.Add(category);
        }

        foreach (var category in Categories.Where(category => category.Id == SettingsCategoryId))
        {
            PrepareCategoryDisplay(category, 0, category.Name, hasChildren: false);
            SidebarCategories.Add(category);
        }
    }

    private static void PrepareCategoryDisplay(CategoryItem category, int level, string selectionName, bool hasChildren)
    {
        category.Level = level;
        category.DisplayName = category.Name;
        category.SelectionName = selectionName;
        category.HasChildren = hasChildren;
    }

    private IEnumerable<CategoryItem> GetOrderedTaskCategories()
    {
        return Categories
            .Where(IsTaskCategoryChoiceVisible)
            .OrderBy(category => category.ParentId is null ? category.SortOrder : GetRootSortOrder(category))
            .ThenBy(category => category.SelectionName, StringComparer.CurrentCultureIgnoreCase);
    }

    private int GetRootSortOrder(CategoryItem category)
    {
        var current = category;
        while (!string.IsNullOrWhiteSpace(current.ParentId))
        {
            var parent = Categories.FirstOrDefault(item => string.Equals(item.Id, current.ParentId, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
            {
                break;
            }

            current = parent;
        }

        return current.SortOrder;
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
                IsSelectableTaskCategory(category) &&
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                string.Equals(category.Name, "Offene Aufträge", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                ShowDesktopSetting &&
                category.Id == DeskCategoryId)
            ?? Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                category.Id != DeskCategoryId)
            ?? Categories.FirstOrDefault();
    }

    private CategoryItem? GetStartupTaskCategory()
    {
        return Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                string.Equals(category.Name, "Offene Aufgaben", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                string.Equals(category.Name, "Offene Aufträge", StringComparison.OrdinalIgnoreCase))
            ?? Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) && category.IsVisible)
            ?? Categories.FirstOrDefault(IsSelectableTaskCategory);
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

    private static CategoryItem CreateMobileInboxCategory()
    {
        return new CategoryItem
        {
            Id = MobileInboxCategoryId,
            Name = MobileInboxCategoryName,
            SortOrder = int.MinValue + 2,
            Color = "#EAF7EF",
            IsVisible = true
        };
    }

    private static CategoryItem CreateOverviewCategory()
    {
        return new CategoryItem
        {
            Id = OverviewCategoryId,
            Name = OverviewCategoryName,
            SortOrder = int.MinValue,
            Color = "#E6F0FF",
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

    private bool TaskBelongsToSelectedCategory(TaskItem task, CategoryItem category)
    {
        if (!TaskBelongsToCategory(task, category.Id))
        {
            return false;
        }

        var descendantIds = GetDescendantCategoryIds(category.Id);
        return descendantIds.Count == 0 || !GetTaskCategoryIds(task).Any(descendantIds.Contains);
    }

    private bool TaskBelongsToCategoryOrDescendant(TaskItem task, CategoryItem category)
    {
        if (TaskBelongsToCategory(task, category.Id))
        {
            return true;
        }

        var descendantIds = GetDescendantCategoryIds(category.Id);
        return descendantIds.Count > 0 && GetTaskCategoryIds(task).Any(descendantIds.Contains);
    }

    private HashSet<string> GetDescendantCategoryIds(string categoryId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        pending.Enqueue(categoryId);

        while (pending.Count > 0)
        {
            var currentId = pending.Dequeue();
            foreach (var child in Categories.Where(category => string.Equals(category.ParentId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                if (result.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return result;
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
        return category.Id == OverviewCategoryId ||
               category.Id == DeskCategoryId ||
               category.Id == TrashCategoryId ||
               category.Id == MobileInboxCategoryId ||
               category.Id == SettingsCategoryId ||
               category.Name == OverviewCategoryName;
    }

    private bool IsTaskCategoryChoiceVisible(CategoryItem category)
    {
        return IsDeskCategory(category) ||
               (!IsSpecialCategory(category) &&
                !IsArchiveCategory(category) &&
                !IsLegacyMobileApprovalCategory(category.Name));
    }

    private bool IsSelectableTaskCategory(CategoryItem category)
    {
        if (!IsTaskCategoryChoiceVisible(category) || HasChildCategories(category))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(category.ParentId))
        {
            return RootEndCategoryNames.Contains(category.Name.Trim());
        }

        return true;
    }

    private bool HasChildCategories(CategoryItem category)
    {
        return Categories.Any(child =>
            string.Equals(child.ParentId, category.Id, StringComparison.OrdinalIgnoreCase) &&
            IsTaskCategoryChoiceVisible(child));
    }

    private static bool IsDeskCategory(CategoryItem category)
    {
        return string.Equals(category.Id, DeskCategoryId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(category.Name?.Trim(), DeskCategoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchiveCategory(CategoryItem category)
    {
        return string.Equals(category.Name?.Trim(), "Archiv", StringComparison.OrdinalIgnoreCase);
    }

    private void LoadMobileInboxEntries()
    {
        MobileInboxEntries.Clear();
        _mobileInboxTaskMap.Clear();

        foreach (var entry in _mobileInboxLoader.Load(
                     IpadLiveFileTargetPath,
                     IpadLiveFileTargetFolder,
                     AppPaths.AppDataDirectory))
        {
            MobileInboxEntries.Add(entry);
        }
    }

    private IEnumerable<TaskItem> GetMobileInboxTasks(string? searchText)
    {
        _mobileInboxTaskMap.Clear();
        IEnumerable<MobileInboxEntry> entries = MobileInboxEntries;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var query = searchText.Trim();
            entries = entries.Where(entry =>
                MobileInboxContains(entry.CustomerName, query) ||
                MobileInboxContains(entry.Title, query) ||
                MobileInboxContains(entry.Address, query) ||
                MobileInboxContains(entry.Phone, query) ||
                MobileInboxContains(entry.Email, query) ||
                MobileInboxContains(entry.Notes, query) ||
                MobileInboxContains(entry.Category, query));
        }

        foreach (var entry in entries.OrderByDescending(entry => entry.CreatedAt))
        {
            var task = CreateMobileInboxTask(entry);
            _mobileInboxTaskMap[task.Id] = entry;
            yield return task;
        }
    }

    private static TaskItem CreateMobileInboxTask(MobileInboxEntry entry)
    {
        var categoryChips = GetVisibleMobileInboxCategoryChips(entry.Category);
        return new TaskItem
        {
            Id = $"{MobileInboxCategoryId}:{entry.Id}",
            CategoryId = MobileInboxCategoryId,
            CategoryIds = new List<string> { MobileInboxCategoryId },
            CustomerName = entry.DisplayCustomerName,
            CustomerAddress = entry.Address,
            CustomerEmail = entry.Email,
            CustomerPhone = entry.Phone,
            Title = entry.DisplayTitle,
            Description = entry.Notes,
            Status = entry.DisplayStatusText,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.CreatedAt,
            CategoryNameChips = categoryChips,
            ShowCategoryHint = categoryChips.Count > 0,
            IsMobileInboxCard = true,
            MobileInboxCreatedAtText = entry.CreatedAtText,
            MobileInboxAttachmentOverviewText = entry.MobileAttachmentOverviewText,
            MobileInboxStatusBadgeBackground = entry.StatusBadgeBackground,
            MobileInboxStatusBadgeBorderBrush = entry.StatusBadgeBorderBrush,
            MobileInboxStatusBadgeForeground = entry.StatusBadgeForeground
        };
    }

    private static List<string> GetVisibleMobileInboxCategoryChips(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || IsLegacyMobileApprovalCategory(category))
        {
            return new List<string>();
        }

        return new List<string> { category.Trim() };
    }

    private static bool MobileInboxContains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool IsMobileInboxTask(TaskItem? task)
    {
        return task is not null && _mobileInboxTaskMap.ContainsKey(task.Id);
    }

    private MobileInboxEntry? GetMobileInboxEntry(TaskItem task)
    {
        return _mobileInboxTaskMap.TryGetValue(task.Id, out var entry) ? entry : null;
    }

    private void ShowMobileInboxPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MobileInboxPreviewItem item })
        {
            SelectedMobileInboxPreviewItem = item;
        }
    }

    private void CloseMobileInboxPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        SelectedMobileInboxPreviewItem = null;
    }

    private void OpenMobileInboxPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = SelectedMobileInboxPreviewItem?.EffectiveDetailPath;
        if (!TryOpenExternalFile(path ?? string.Empty, out var errorMessage))
        {
            MobileInboxPreviewStatus = errorMessage ?? "Datei konnte nicht geöffnet werden.";
        }
    }

    private void ShowMobileInboxPreviewInFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = SelectedMobileInboxPreviewItem?.EffectiveDetailPath;
        if (!TryShowFileInFolder(path ?? string.Empty, out var errorMessage))
        {
            MobileInboxPreviewStatus = errorMessage ?? "Datei konnte nicht im Ordner angezeigt werden.";
        }
    }

    private async void ImportMobileInboxEntry_OnClick(object? sender, RoutedEventArgs e)
    {
        var entry = SelectedMobileInboxEntry;
        if (entry is null)
        {
            return;
        }

        var existingTask = FindImportedMobileInboxTask(entry.Id);
        if (existingTask is not null)
        {
            await ShowMobileInboxImportMessageDialogAsync(
                "Bereits übernommen",
                "Dieser mobile Eingang wurde bereits als Auftrag in BüroCockpit übernommen.");
            SelectImportedTask(existingTask);
            return;
        }

        if (!await ShowMobileInboxImportConfirmationDialogAsync())
        {
            return;
        }

        TaskItem? importedTask = null;
        try
        {
            var targetCategory = ResolveMobileInboxImportCategory(entry);
            var categoryWasMatched = IsMatchingMobileInboxCategory(entry.Category, targetCategory.Name);
            importedTask = CreateTaskFromMobileInboxEntry(entry, targetCategory, categoryWasMatched);
            importedTask.SortPosition = _repository.GetTopTaskSortPosition(targetCategory.Id);
            var importResult = PrepareMobileInboxImport(entry);
            if (!importResult.CanImport)
            {
                await ShowMobileInboxImportMessageDialogAsync(
                    "Übernahme nicht vollständig möglich",
                    importResult.ToUserMessage());
                return;
            }

            SaveTaskAndQueueIpadSnapshot(importedTask);
            if (!SaveNow("mobile-inbox-import"))
            {
                return;
            }

            ImportMobileInboxAttachments(importResult, importedTask);
            if (!SaveNow("mobile-inbox-import-finish"))
            {
                return;
            }

            MoveMobileInboxEntryToProcessed(entry);

            ClearSearchTextWithoutRefresh();
            LoadData(targetCategory.Id, importedTask.Id);
        }
        catch (Exception ex)
        {
            if (importedTask is not null)
            {
                DeleteTaskAndQueueIpadSnapshot(importedTask.Id);
            }

            Debug.WriteLine($"Mobile inbox import failed for '{entry.DirectoryPath}': {ex}");
            await ShowMobileInboxImportMessageDialogAsync(
                "Übernahme fehlgeschlagen",
                ex is MobileInboxImportException importException
                    ? importException.Result.ToUserMessage()
                    : "Der mobile Eingang konnte nicht übernommen werden. Es wurde kein normaler Auftrag sichtbar angelegt.");
        }
    }

    private TaskItem? FindImportedMobileInboxTask(string mobileInboxId)
    {
        if (string.IsNullOrWhiteSpace(mobileInboxId))
        {
            return null;
        }

        var marker = BuildMobileInboxImportMarker(mobileInboxId);
        return AllTasks.FirstOrDefault(task =>
            !task.IsDeleted &&
            !string.IsNullOrWhiteSpace(task.Description) &&
            task.Description.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectImportedTask(TaskItem task)
    {
        var category = Categories.FirstOrDefault(item => TaskBelongsToCategory(task, item.Id) && !IsSpecialCategory(item))
            ?? Categories.FirstOrDefault(item => string.Equals(item.Id, task.CategoryId, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            return;
        }

        SelectCategoryAndTask(category, task);
    }

    private CategoryItem ResolveMobileInboxImportCategory(MobileInboxEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Category) && !IsLegacyMobileApprovalCategory(entry.Category))
        {
            var existingCategory = Categories.FirstOrDefault(category =>
                IsSelectableTaskCategory(category) &&
                IsMatchingMobileInboxCategory(entry.Category, category.Name));
            if (existingCategory is not null)
            {
                return existingCategory;
            }
        }

        return Categories.FirstOrDefault(category =>
                   IsSelectableTaskCategory(category) &&
                   string.Equals(category.Name, "Offene Aufgaben", StringComparison.CurrentCultureIgnoreCase))
               ?? Categories.First(IsSelectableTaskCategory);
    }

    private static bool IsMatchingMobileInboxCategory(string? mobileCategoryName, string? categoryName)
    {
        var normalizedMobileCategory = NormalizeMobileInboxCategoryName(mobileCategoryName);
        var normalizedCategory = NormalizeMobileInboxCategoryName(categoryName);
        return !string.IsNullOrWhiteSpace(normalizedMobileCategory) &&
               string.Equals(normalizedMobileCategory, normalizedCategory, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string NormalizeMobileInboxCategoryName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static TaskItem CreateTaskFromMobileInboxEntry(MobileInboxEntry entry, CategoryItem category, bool categoryWasMatched)
    {
        var now = DateTime.Now;
        var importNote = $"Mobiler Eingang vom iPad übernommen am {now:dd.MM.yyyy HH:mm}";
        var marker = BuildMobileInboxImportMarker(entry.Id);
        var descriptionParts = new List<string?>
            {
                entry.Notes?.Trim(),
                importNote
            };

        if (!categoryWasMatched &&
            !string.IsNullOrWhiteSpace(entry.Category) &&
            !IsLegacyMobileApprovalCategory(entry.Category))
        {
            descriptionParts.Add($"Ursprüngliche iPad-Kategorie: {entry.Category.Trim()}");
        }

        descriptionParts.Add($"[{marker}]");

        return new TaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            CategoryId = category.Id,
            CategoryIds = new List<string> { category.Id },
            CustomerName = entry.CustomerName,
            CustomerAddress = entry.Address,
            CustomerEmail = entry.Email,
            CustomerPhone = entry.Phone,
            Title = entry.DisplayTitle,
            Description = string.Join(
                Environment.NewLine + Environment.NewLine,
                descriptionParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            Status = "Offen",
            Priority = "Normal",
            CreatedAt = entry.CreatedAt == default ? now : entry.CreatedAt,
            UpdatedAt = now
        };
    }

    private MobileInboxImportResult PrepareMobileInboxImport(MobileInboxEntry entry)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var originalPhotoSources = entry.OriginalPhotoPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        foreach (var path in originalPhotoSources)
        {
            AddMobileInboxAttachmentProblem(errors, path, "Originalfoto");
        }

        var photoSources = originalPhotoSources.Count > 0
            ? originalPhotoSources
            : entry.PhotoPreviews
                .Where(item => item.Exists)
                .Select(item => item.Path)
                .ToList();

        foreach (var preview in entry.PhotoPreviews.Concat(entry.SketchPreviews).Concat(entry.FilePreviews))
        {
            if (preview.IsMissing)
            {
                warnings.Add($"{preview.DisplayKind} fehlt: {FormatMobileInboxPathForMessage(preview.Path)}");
            }
            else if (preview.IsUnreadable)
            {
                warnings.Add($"{preview.DisplayKind} ist nicht lesbar: {FormatMobileInboxPathForMessage(preview.Path)}");
            }

            if (preview.IsDetailMissing)
            {
                warnings.Add($"{preview.DisplayKind} Detaildatei fehlt: {FormatMobileInboxPathForMessage(preview.EffectiveDetailPath)}");
            }
            else if (preview.IsDetailUnreadable)
            {
                warnings.Add($"{preview.DisplayKind} Detaildatei ist nicht lesbar: {FormatMobileInboxPathForMessage(preview.EffectiveDetailPath)}");
            }
        }

        var attachmentSources = photoSources
            .Select(path => new MobileInboxAttachmentSource(path, GetMobileInboxPhotoKind(path)))
            .Concat(GetMobileInboxSketchPngPaths(entry).Select(path => new MobileInboxAttachmentSource(path, "Skizze")))
            .Concat(GetMobileInboxDrawingPaths(entry).Select(path => new MobileInboxAttachmentSource(path, "Skizzen-Rohdaten")))
            .Concat(entry.FilePreviews
                .Where(item => item.DetailExists)
                .Select(item => new MobileInboxAttachmentSource(item.EffectiveDetailPath, "Sonstige Datei")))
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .GroupBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (var source in attachmentSources)
        {
            AddMobileInboxAttachmentProblem(errors, source.Path, source.Kind);
        }

        if (errors.Count == 0 && attachmentSources.Count == 0 && (entry.OriginalPhotoPaths.Count > 0 || entry.SketchPreviews.Count > 0 || entry.FilePreviews.Count > 0))
        {
            errors.Add("Es wurden Anhänge referenziert, aber keine übernehmbare Datei gefunden.");
        }

        return new MobileInboxImportResult(
            attachmentSources
                .Where(source => File.Exists(source.Path) && IsMobileInboxImportSourceReadable(source.Path, source.Kind))
                .ToList(),
            errors.Distinct(StringComparer.CurrentCultureIgnoreCase).ToList(),
            warnings.Distinct(StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    private void ImportMobileInboxAttachments(MobileInboxImportResult importResult, TaskItem task)
    {
        if (!importResult.CanImport)
        {
            throw new MobileInboxImportException(importResult);
        }

        var importedAttachments = new List<string>();
        foreach (var source in importResult.AttachmentSources)
        {
            var normalizedSourcePath = Path.GetFullPath(source.Path);
            var originalName = Path.GetFileName(normalizedSourcePath);
            var destinationPath = ResolveAttachmentStoragePath(normalizedSourcePath, task.Id);
            var attachment = new AttachmentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                TaskId = task.Id,
                FileName = BuildMobileInboxAttachmentFileName(source.Kind, originalName),
                StoredPath = AppPaths.ToStoredPath(destinationPath),
                ThumbnailPath = string.Empty,
                FileType = Path.GetExtension(originalName),
                AddedAt = DateTime.Now
            };

            attachment.ContentHash = EnsureAttachmentContentHash(attachment, persist: false) ?? string.Empty;
            EnsureAttachmentThumbnail(attachment);
            SaveAttachmentAndQueueIpadSnapshot(attachment);
            importedAttachments.Add($"{source.Kind}: {originalName}");
        }

        if (importedAttachments.Count > 0)
        {
            task.Description = string.Join(
                Environment.NewLine + Environment.NewLine,
                new[]
                {
                    task.Description,
                    "Übernommene mobile Anhänge:" + Environment.NewLine + string.Join(Environment.NewLine, importedAttachments.Select(item => $"- {item}"))
                }.Where(part => !string.IsNullOrWhiteSpace(part)));
            task.UpdatedAt = DateTime.Now;
            SaveTaskAndQueueIpadSnapshot(task);
        }
    }

    private static void AddMobileInboxAttachmentProblem(List<string> errors, string path, string kind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"{kind}: Dateipfad fehlt.");
            return;
        }

        if (!File.Exists(path))
        {
            errors.Add($"{kind} fehlt: {FormatMobileInboxPathForMessage(path)}");
            return;
        }

        if (!IsMobileInboxImportSourceReadable(path, kind))
        {
            errors.Add($"{kind} ist nicht lesbar: {FormatMobileInboxPathForMessage(path)}");
        }
    }

    private static string FormatMobileInboxPathForMessage(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? "Datei ohne Namen" : fileName;
    }

    private static bool IsMobileInboxImportSourceReadable(string path, string kind)
    {
        if (!IsMobileInboxImageAttachmentKind(kind))
        {
            return true;
        }

        try
        {
            using var _ = new Avalonia.Media.Imaging.Bitmap(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMobileInboxImageAttachmentKind(string kind)
    {
        return string.Equals(kind, "Originalfoto", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "Markiertes Foto", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "Skizze", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMobileInboxPhotoKind(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/annotated/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("-markiert", StringComparison.OrdinalIgnoreCase)
            ? "Markiertes Foto"
            : "Originalfoto";
    }

    private static string BuildMobileInboxAttachmentFileName(string kind, string originalName)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return originalName;
        }

        return originalName.StartsWith($"{kind} - ", StringComparison.CurrentCultureIgnoreCase)
            ? originalName
            : $"{kind} - {originalName}";
    }

    private static IEnumerable<string> GetMobileInboxSketchPngPaths(MobileInboxEntry entry)
    {
        var previewPaths = entry.SketchPreviews
            .Where(item => item.Exists)
            .Select(item => item.Path);
        var sketchesDirectory = Path.Combine(entry.DirectoryPath, "sketches");
        var pngPaths = Directory.Exists(sketchesDirectory)
            ? Directory.EnumerateFiles(sketchesDirectory, "*.png", SearchOption.TopDirectoryOnly)
            : Enumerable.Empty<string>();

        return previewPaths.Concat(pngPaths);
    }

    private static IEnumerable<string> GetMobileInboxDrawingPaths(MobileInboxEntry entry)
    {
        var sketchesDirectory = Path.Combine(entry.DirectoryPath, "sketches");
        return Directory.Exists(sketchesDirectory)
            ? Directory.EnumerateFiles(sketchesDirectory, "*.pkdrawing", SearchOption.TopDirectoryOnly)
            : Enumerable.Empty<string>();
    }

    private static void MoveMobileInboxEntryToProcessed(MobileInboxEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DirectoryPath) || !Directory.Exists(entry.DirectoryPath))
        {
            throw new DirectoryNotFoundException(entry.DirectoryPath);
        }

        var inboxDirectory = Directory.GetParent(entry.DirectoryPath)?.FullName
            ?? throw new InvalidOperationException("Mobile-Inbox-Ordner konnte nicht ermittelt werden.");
        var baseDirectory = Directory.GetParent(inboxDirectory)?.FullName ?? inboxDirectory;
        var processedDirectory = Path.Combine(baseDirectory, "mobile-processed");
        Directory.CreateDirectory(processedDirectory);

        var targetDirectory = GetUniqueProcessedDirectoryPath(
            Path.Combine(processedDirectory, Path.GetFileName(entry.DirectoryPath)));
        Directory.Move(entry.DirectoryPath, targetDirectory);
        Directory.SetLastWriteTime(targetDirectory, DateTime.Now);
    }

    private static string GetUniqueProcessedDirectoryPath(string preferredPath)
    {
        if (!Directory.Exists(preferredPath) && !File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var parent = Path.GetDirectoryName(preferredPath) ?? string.Empty;
        var name = Path.GetFileName(preferredPath);
        for (var index = 1; index < 1000; index++)
        {
            var candidate = Path.Combine(parent, $"{name}-{DateTime.Now:yyyyMMddHHmmss}-{index}");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Kein freier Zielordner unter mobile-processed gefunden.");
    }

    private async void CleanupProcessedMobileInbox_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!await ShowMobileProcessedCleanupConfirmationDialogAsync())
        {
            return;
        }

        try
        {
            var cleanupResult = CleanupOldProcessedMobileInboxEntries(DateTime.Now.AddDays(-MobileProcessedRetentionDays));
            MobileInboxCleanupStatus = cleanupResult.DeletedCount == 0 && cleanupResult.FailedCount == 0
                ? "Keine übernommenen mobilen Eingänge älter als 30 Tage gefunden."
                : $"{cleanupResult.DeletedCount} übernommene mobile Eingänge bereinigt. Fehler: {cleanupResult.FailedCount}.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Mobile processed cleanup failed: {ex}");
            MobileInboxCleanupStatus = "Übernommene mobile Eingänge konnten nicht bereinigt werden.";
        }
    }

    private MobileProcessedCleanupResult CleanupOldProcessedMobileInboxEntries(DateTime cutoff)
    {
        var deletedCount = 0;
        var failedCount = 0;

        foreach (var processedDirectory in ResolveMobileProcessedDirectories())
        {
            if (!IsSafeMobileProcessedDirectory(processedDirectory))
            {
                continue;
            }

            foreach (var entryDirectory in Directory.EnumerateDirectories(processedDirectory, "mobile-*", SearchOption.TopDirectoryOnly))
            {
                if (!IsSafeMobileProcessedEntryDirectory(processedDirectory, entryDirectory))
                {
                    continue;
                }

                var importedAt = GetMobileProcessedEntryAgeTime(entryDirectory);
                if (importedAt > cutoff)
                {
                    continue;
                }

                try
                {
                    Directory.Delete(entryDirectory, recursive: true);
                    deletedCount++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failedCount++;
                    Debug.WriteLine($"Mobile processed entry cleanup failed for '{entryDirectory}': {ex}");
                }
            }
        }

        return new MobileProcessedCleanupResult(deletedCount, failedCount);
    }

    private IEnumerable<string> ResolveMobileProcessedDirectories()
    {
        var candidates = new List<string>();
        AddMobileProcessedCandidates(IpadLiveFileTargetPath, candidates);
        AddMobileProcessedCandidates(IpadLiveFileTargetFolder, candidates);
        return candidates.Where(Directory.Exists);
    }

    private static void AddMobileProcessedCandidates(string? path, List<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        var baseDirectory = IsIpadLiveFilePath(fullPath) || File.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "mobile-processed", StringComparison.OrdinalIgnoreCase))
        {
            AddMobileProcessedCandidate(baseDirectory, candidates);
            return;
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "mobile-inbox", StringComparison.OrdinalIgnoreCase))
        {
            var inboxParent = Directory.GetParent(baseDirectory);
            if (inboxParent is not null)
            {
                AddMobileProcessedCandidate(Path.Combine(inboxParent.FullName, "mobile-processed"), candidates);
            }

            return;
        }

        AddMobileProcessedCandidate(Path.Combine(baseDirectory, "mobile-processed"), candidates);

        var parent = Directory.GetParent(baseDirectory);
        if (parent is not null)
        {
            AddMobileProcessedCandidate(Path.Combine(parent.FullName, "mobile-processed"), candidates);
        }

        if (string.Equals(Path.GetFileName(baseDirectory), "live", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(baseDirectory), "Sync", StringComparison.OrdinalIgnoreCase))
        {
            var syncParent = Directory.GetParent(baseDirectory);
            if (syncParent is not null)
            {
                AddMobileProcessedCandidate(Path.Combine(syncParent.FullName, "mobile-processed"), candidates);
            }
        }
    }

    private static void AddMobileProcessedCandidate(string candidate, List<string> candidates)
    {
        if (!candidates.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidates.Add(candidate);
        }
    }

    private static bool IsSafeMobileProcessedDirectory(string path)
    {
        return Directory.Exists(path) &&
               string.Equals(Path.GetFileName(Path.GetFullPath(path)), "mobile-processed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeMobileProcessedEntryDirectory(string processedDirectory, string entryDirectory)
    {
        var processedFullPath = Path.GetFullPath(processedDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var entryFullPath = Path.GetFullPath(entryDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(Path.GetFileName(entryFullPath), Path.GetFileName(entryDirectory), StringComparison.Ordinal) &&
               Path.GetFileName(entryFullPath).StartsWith("mobile-", StringComparison.OrdinalIgnoreCase) &&
               entryFullPath.StartsWith(processedFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetMobileProcessedEntryAgeTime(string entryDirectory)
    {
        var lastWriteTime = Directory.GetLastWriteTime(entryDirectory);
        if (lastWriteTime.Year >= 2000)
        {
            return lastWriteTime;
        }

        var jsonPath = Path.Combine(entryDirectory, "aufgabe.json");
        if (!File.Exists(jsonPath))
        {
            return lastWriteTime;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (document.RootElement.TryGetProperty("createdAt", out var createdAtElement) &&
                DateTime.TryParse(createdAtElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var createdAt))
            {
                return createdAt;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Debug.WriteLine($"Mobile processed entry age could not be read from '{jsonPath}': {ex}");
        }

        return lastWriteTime;
    }

    private async Task<bool> ShowMobileProcessedCleanupConfirmationDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Übernommene mobile Eingänge bereinigen",
            Width = 500,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Übernommene mobile Eingänge älter als 30 Tage endgültig löschen?",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Es werden nur Ordner unter mobile-processed gelöscht. mobile-inbox bleibt unverändert.",
                            FontSize = 13,
                            Foreground = ResourceBrush("TextSecondaryBrush"),
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
                                CreateMobileInboxDialogAction("Abbrechen", false),
                                CreateMobileInboxDialogAction("Bereinigen", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var cancelAction = (Border)buttonsPanel.Children[0];
        var cleanupAction = (Border)buttonsPanel.Children[1];
        var result = false;

        cancelAction.PointerReleased += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        cleanupAction.PointerReleased += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private static string BuildMobileInboxImportMarker(string mobileInboxId)
    {
        return $"{MobileInboxImportMarkerPrefix} {mobileInboxId.Trim()}";
    }

    private static bool IsLegacyMobileApprovalCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var legacyName = string.Join(" ", "Wartet", "auf", "Freigabe");
        return string.Equals(value.Trim(), legacyName, StringComparison.CurrentCultureIgnoreCase);
    }

    private sealed record MobileProcessedCleanupResult(int DeletedCount, int FailedCount);

    private async Task<bool> ShowMobileInboxImportConfirmationDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Mobilen Eingang übernehmen",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Diesen mobilen Eingang als Auftrag in BüroCockpit übernehmen?",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = ResourceBrush("TextPrimaryBrush"),
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
                                CreateMobileInboxDialogAction("Abbrechen", false),
                                CreateMobileInboxDialogAction("Übernehmen", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var cancelAction = (Border)buttonsPanel.Children[0];
        var importAction = (Border)buttonsPanel.Children[1];
        var result = false;

        cancelAction.PointerReleased += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        importAction.PointerReleased += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowMobileInboxImportMessageDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = ResourceBrush("WindowBackgroundBrush"),
            Content = new Border
            {
                Background = ResourceBrush("SurfaceElevatedBrush"),
                BorderBrush = ResourceBrush("BorderBrushDark"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = ResourceBrush("TextPrimaryBrush"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 13,
                            Foreground = ResourceBrush("TextSecondaryBrush"),
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
                                CreateMobileInboxDialogAction("OK", true)
                            }
                        }
                    }
                }
            }
        };

        var buttonsPanel = ((StackPanel)((Border)dialog.Content!).Child!).Children.OfType<StackPanel>().Last();
        var okAction = (Border)buttonsPanel.Children[0];
        okAction.PointerReleased += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static Border CreateMobileInboxDialogAction(string text, bool isPrimary)
    {
        var normalBackground = ResourceBrush(isPrimary ? "AccentBrush" : "SurfaceElevatedBrush");
        var hoverBackground = ResourceBrush(isPrimary ? "AccentHoverBrush" : "HoverBackgroundBrush");
        var normalBorder = ResourceBrush(isPrimary ? "AccentBrush" : "BorderBrushDark");
        var hoverBorder = ResourceBrush(isPrimary ? "AccentHoverBrush" : "BorderBrushStrong");
        var foreground = ResourceBrush(isPrimary ? "TextOnAccentBrush" : "TextPrimaryBrush");

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
            MinWidth = isPrimary ? 130 : 105,
            Height = 34,
            Background = normalBackground,
            BorderBrush = normalBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 6),
            Child = label
        };

        action.PointerEntered += (_, _) =>
        {
            action.Background = hoverBackground;
            action.BorderBrush = hoverBorder;
        };

        action.PointerExited += (_, _) =>
        {
            action.Background = normalBackground;
            action.BorderBrush = normalBorder;
        };

        return action;
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
            SaveAttachmentAndQueueIpadSnapshot(attachment);
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
        _dirtyDeskItems.Remove(deskItem.Id);
        _deletedDeskItems.Add(deskItem.Id);
        MarkDataDirty($"desk-item-delete:{deskItem.Id}");
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
        TechnicianProfiles.Clear();

        var settings = _liveSettingsService.Load(ResolveLiveSettingsSyncRootDirectory(), _appSettings.TechnicianNames);
        foreach (var profile in settings.Technicians)
        {
            TechnicianProfiles.Add(profile);
            TechnicianOptions.Add(profile.Name);
        }

        SelectedTechnicianProfile = TechnicianProfiles.FirstOrDefault(profile => profile.IsStandard)
            ?? TechnicianProfiles.FirstOrDefault();
    }

    private void SaveTechnicianOptions()
    {
        var settings = new LiveSettings
        {
            Technicians = TechnicianProfiles.ToList()
        };
        _liveSettingsService.Save(ResolveLiveSettingsSyncRootDirectory(), settings);
        LoadTechnicianOptions();
    }

    private string ResolveLiveSettingsSyncRootDirectory()
    {
        var sharedDirectory = HasOneDriveEditDirectory
            ? OneDriveEditDirectory
            : AppPaths.AppDataDirectory;

        return IpadSnapshotExportService.ResolveSyncRootDirectory(sharedDirectory);
    }

    private void AddTechnician_OnClick(object? sender, RoutedEventArgs e)
    {
        var profile = new TechnicianProfile { Name = "Neuer Techniker" };
        var number = 2;
        while (TechnicianProfiles.Any(existing => string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
        {
            profile.Name = $"Neuer Techniker {number++}";
        }

        TechnicianProfiles.Add(profile);
        SelectedTechnicianProfile = profile;
    }

    private void SaveTechnicianProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedTechnicianProfile is null)
        {
            return;
        }

        var name = TechnicianNameInput.Trim();
        if (string.IsNullOrWhiteSpace(name) || TechnicianProfiles.Any(profile =>
                !ReferenceEquals(profile, SelectedTechnicianProfile) &&
                string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedTechnicianProfile.Name = name;
        SelectedTechnicianProfile.Abbreviation = TechnicianAbbreviationInput.Trim();
        SelectedTechnicianProfile.Email = TechnicianEmailInput.Trim();
        SelectedTechnicianProfile.Phone = TechnicianPhoneInput.Trim();
        SaveTechnicianOptions();
    }

    private void RemoveTechnician_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TechnicianProfile profile } && !profile.IsStandard)
        {
            TechnicianProfiles.Remove(profile);
            if (ReferenceEquals(SelectedTechnicianProfile, profile))
            {
                SelectedTechnicianProfile = TechnicianProfiles.FirstOrDefault();
            }
            SaveTechnicianOptions();
        }
    }

    private void LoadTechnicianEditor(TechnicianProfile? profile)
    {
        TechnicianNameInput = profile?.Name ?? string.Empty;
        TechnicianAbbreviationInput = profile?.Abbreviation ?? string.Empty;
        TechnicianEmailInput = profile?.Email ?? string.Empty;
        TechnicianPhoneInput = profile?.Phone ?? string.Empty;
    }

    private void SetTechnicianEditorValue(ref string field, string? value, string propertyName)
    {
        var normalized = value ?? string.Empty;
        if (field == normalized) return;
        field = normalized;
        OnPropertyChanged(propertyName);
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
    public DashboardSection(string title, string emptyText, int count, IEnumerable<TaskItem> tasks)
    {
        Title = title;
        EmptyText = emptyText;
        Count = count;
        Tasks = new ObservableCollection<TaskItem>(tasks);
    }

    public string Title { get; }
    public string EmptyText { get; }
    public int Count { get; }
    public ObservableCollection<TaskItem> Tasks { get; }
    public bool HasTasks => Tasks.Count > 0;
    public bool HasNoTasks => !HasTasks;
}

public sealed record TaskUndoSnapshot(TaskItem Task, IReadOnlyList<MaterialItem> Materials, IReadOnlyList<AttachmentItem> Attachments);

public sealed record LocalNetworkRememberedDeviceListItem(
    string DeviceName,
    string Platform,
    string LastSeenText,
    string StatusText)
{
    public static LocalNetworkRememberedDeviceListItem FromDevice(LocalNetworkRememberedDevice device)
    {
        var deviceName = string.IsNullOrWhiteSpace(device.DeviceName)
            ? "Unbenanntes iPad"
            : device.DeviceName.Trim();
        var platform = string.IsNullOrWhiteSpace(device.Platform)
            ? "iPadOS"
            : device.Platform.Trim();
        var lastSeen = device.LastSeenUtc == default
            ? "zuletzt gesehen: unbekannt"
            : $"zuletzt gesehen: {device.LastSeenUtc.ToLocalTime():dd.MM.yyyy HH:mm}";
        var status = string.Equals(device.Status, "remembered", StringComparison.OrdinalIgnoreCase)
            ? "Status: vorgemerkt, Sync noch nicht aktiv"
            : $"Status: {device.Status}";

        return new LocalNetworkRememberedDeviceListItem(deviceName, platform, lastSeen, status);
    }
}

internal sealed record MobileInboxAttachmentSource(string Path, string Kind);

internal sealed record MobileInboxImportResult(
    IReadOnlyList<MobileInboxAttachmentSource> AttachmentSources,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool CanImport => Errors.Count == 0;

    public string ToUserMessage()
    {
        var parts = new List<string>
        {
            CanImport
                ? "Der mobile Eingang kann vollständig übernommen werden."
                : "Der mobile Eingang wurde nicht übernommen, weil mindestens ein benötigter Anhang fehlt oder nicht lesbar ist."
        };

        if (AttachmentSources.Count > 0)
        {
            parts.Add("Übernehmbare Anhänge:" + Environment.NewLine +
                      string.Join(Environment.NewLine, AttachmentSources.Select(source => $"- {source.Kind}: {Path.GetFileName(source.Path)}")));
        }

        if (Errors.Count > 0)
        {
            parts.Add("Fehler:" + Environment.NewLine +
                      string.Join(Environment.NewLine, Errors.Select(error => $"- {error}")));
        }

        if (Warnings.Count > 0)
        {
            parts.Add("Hinweise:" + Environment.NewLine +
                      string.Join(Environment.NewLine, Warnings.Select(warning => $"- {warning}")));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }
}

internal sealed class MobileInboxImportException : Exception
{
    public MobileInboxImportException(MobileInboxImportResult result)
        : base(result.ToUserMessage())
    {
        Result = result;
    }

    public MobileInboxImportResult Result { get; }
}

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
        DateTime? sentAt,
        bool isArchived)
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
        IsArchived = isArchived;
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
    public bool IsArchived { get; }
}

public sealed class TaskCategorySelection
{
    public TaskCategorySelection(CategoryItem category, bool isSelected, bool isSelectable)
    {
        Category = category;
        IsSelected = isSelected;
        IsSelectable = isSelectable;
    }

    public CategoryItem Category { get; }
    public string Name => Category.Name;
    public bool IsSelected { get; set; }
    public bool IsSelectable { get; }
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
            Background = MainWindow.ResourceBrush("SurfaceElevatedBrush"),
            Foreground = MainWindow.ResourceBrush("TextPrimaryBrush"),
            BorderBrush = MainWindow.ResourceBrush("BorderBrushDark"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };

        button.PointerEntered += (_, _) =>
        {
            button.Background = MainWindow.ResourceBrush("HoverBackgroundBrush");
            button.Foreground = MainWindow.ResourceBrush("TextPrimaryBrush");
            button.BorderBrush = MainWindow.ResourceBrush("BorderBrushStrong");
        };

        button.PointerExited += (_, _) =>
        {
            button.Background = MainWindow.ResourceBrush("SurfaceElevatedBrush");
            button.Foreground = MainWindow.ResourceBrush("TextPrimaryBrush");
            button.BorderBrush = MainWindow.ResourceBrush("BorderBrushDark");
        };

        button.Click += (_, _) => Close(choice);
        return button;
    }
}
