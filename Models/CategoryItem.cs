namespace BueroCockpit.Models;

public sealed class CategoryItem : ObservableObject
{
    private string _name = string.Empty;
    private int _sortOrder;
    private string _sortMode = "Erstellt am";
    private string _color = "#E8EDF7";
    private bool _isVisible = true;
    private int _taskCount;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public int SortOrder { get => _sortOrder; set => SetProperty(ref _sortOrder, value); }
    public string SortMode { get => _sortMode; set => SetProperty(ref _sortMode, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public int TaskCount { get => _taskCount; set => SetProperty(ref _taskCount, value); }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SidebarBackground));
                OnPropertyChanged(nameof(SidebarBorderBrush));
            }
        }
    }

    public string SidebarBackground => IsSelected ? "#DDEBFF" : Color;
    public string SidebarBorderBrush => IsSelected ? "#8DBEFF" : "Transparent";
}
