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
    private readonly BueroRepository _repository = new();
    private readonly ThumbnailService _thumbnailService = new();
    private CategoryItem? _selectedCategory;
    private TaskItem? _selectedTask;
    private CategoryItem? _selectedTaskCategory;
    private string _taskListCaption = "0 Aufgaben";
    private bool _isLoadingSelection;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CategoryItem> Categories { get; } = new();
    public ObservableCollection<TaskItem> AllTasks { get; } = new();
    public ObservableCollection<TaskItem> VisibleTasks { get; } = new();
    public ObservableCollection<MaterialItem> Materials { get; } = new();
    public ObservableCollection<AttachmentItem> Attachments { get; } = new();

    public string[] StatusOptions { get; } = ["Offen", "In Arbeit", "Wartet", "Erledigt", "Archiv"];
    public string[] PriorityOptions { get; } = ["Niedrig", "Normal", "Hoch", "Dringend"];
    public string[] MaterialStatusOptions { get; } = ["Offen", "Anfragen", "Bestellt", "Geliefert", "Nicht noetig"];

    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value)
            {
                return;
            }

            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            RefreshVisibleTasks();
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
            _selectedTask = value;
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

        AllTasks.Clear();
        foreach (var task in _repository.GetTasks())
        {
            AllTasks.Add(task);
        }

        UpdateCategoryCounts();
        SelectedCategory = Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben") ?? Categories.FirstOrDefault();
    }

    private void RefreshVisibleTasks()
    {
        VisibleTasks.Clear();

        var selected = SelectedCategory;
        var tasks = selected?.Name == "Übersicht" || selected is null
            ? AllTasks
            : new ObservableCollection<TaskItem>(AllTasks.Where(t => t.CategoryId == selected.Id));

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

    private void UpdateCategoryCounts()
    {
        foreach (var category in Categories)
        {
            category.TaskCount = category.Name == "Übersicht"
                ? AllTasks.Count
                : AllTasks.Count(t => t.CategoryId == category.Id);
        }
    }

    private void LoadTaskDetails()
    {
        _isLoadingSelection = true;
        Materials.Clear();
        Attachments.Clear();

        if (SelectedTask is not null)
        {
            SelectedTaskCategory = Categories.FirstOrDefault(c => c.Id == SelectedTask.CategoryId);

            foreach (var item in _repository.GetMaterials(SelectedTask.Id))
            {
                Materials.Add(item);
            }

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
        var category = SelectedCategory?.Name == "Übersicht" ? Categories.FirstOrDefault(c => c.Name == "Offene Aufgaben") : SelectedCategory;
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

        SelectedTask.CategoryId = SelectedTaskCategory?.Id ?? SelectedTask.CategoryId;
        if (SelectedTask.Status == "Erledigt" && SelectedTask.CompletedAt is null)
        {
            SelectedTask.CompletedAt = DateTime.Now;
        }
        else if (SelectedTask.Status != "Erledigt")
        {
            SelectedTask.CompletedAt = null;
        }

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
        SelectedTask = null;
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
            Status = "Offen"
        };

        Materials.Add(item);
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
    }

    private void OpenAttachment_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AttachmentItem item } || !File.Exists(item.StoredPath))
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
