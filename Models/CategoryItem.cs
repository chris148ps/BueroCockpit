namespace BueroCockpit.Models;

public sealed class CategoryItem : ObservableObject
{
    private string _name = string.Empty;
    private string? _parentId;
    private int _sortOrder;
    private int _level;
    private string _sortMode = "Erstellt am";
    private string _color = "#E8EDF7";
    private bool _isVisible = true;
    private int _taskCount;
    private bool _isSelected;
    private bool _isExpanded;
    private bool _hasChildren;
    private CategoryDropVisualState _dropVisualState = CategoryDropVisualState.None;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string? ParentId { get => _parentId; set => SetProperty(ref _parentId, string.IsNullOrWhiteSpace(value) ? null : value); }
    public int SortOrder { get => _sortOrder; set => SetProperty(ref _sortOrder, value); }
    public int Level { get => _level; set => SetProperty(ref _level, value); }
    public string SortMode { get => _sortMode; set => SetProperty(ref _sortMode, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public int TaskCount { get => _taskCount; set => SetProperty(ref _taskCount, value); }
    public bool HasChildren
    {
        get => _hasChildren;
        set
        {
            if (SetProperty(ref _hasChildren, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }
    }
    public string DisplayName { get; set; } = string.Empty;
    public string SelectionName { get; set; } = string.Empty;
    public bool IsChildCategory => !string.IsNullOrWhiteSpace(ParentId);
    public int SidebarIndent => Math.Clamp(Level, 0, 4) * 16;
    public int SelectionIndent => Math.Clamp(Level, 0, 4) * 18;
    public Avalonia.Thickness DropLineMargin => new(SidebarIndent + 20, 0, 10, 0);
    public string ExpandGlyph => HasChildren ? (IsExpanded ? "▾" : "▸") : string.Empty;
    public CategoryDropVisualState DropVisualState
    {
        get => _dropVisualState;
        set
        {
            if (SetProperty(ref _dropVisualState, value))
            {
                OnPropertyChanged(nameof(IsDropBefore));
                OnPropertyChanged(nameof(IsDropInside));
                OnPropertyChanged(nameof(IsDropAfter));
                OnPropertyChanged(nameof(SidebarBorderBrush));
            }
        }
    }

    public bool IsDropBefore => DropVisualState == CategoryDropVisualState.Before;
    public bool IsDropInside => DropVisualState == CategoryDropVisualState.Inside;
    public bool IsDropAfter => DropVisualState == CategoryDropVisualState.After;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SidebarBorderBrush));
            }
        }
    }

    public string SidebarBorderBrush => IsDropInside ? "#1D4ED8" : IsSelected ? "#111827" : "#D6DDE8";
}

public enum CategoryDropVisualState
{
    None,
    Before,
    Inside,
    After
}
