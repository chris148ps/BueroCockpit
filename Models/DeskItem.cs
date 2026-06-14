namespace BueroCockpit.Models;

public sealed class DeskItem : ObservableObject
{
    private string _type = "Note";
    private string _text = string.Empty;
    private double _x = 48;
    private double _y = 48;
    private double _width = 300;
    private double _height = 210;
    private bool _isImportant;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get => _type; set => SetProperty(ref _type, value); }
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Width { get => _width; set => SetProperty(ref _width, value); }
    public double Height { get => _height; set => SetProperty(ref _height, value); }
    public bool IsImportant
    {
        get => _isImportant;
        set
        {
            if (SetProperty(ref _isImportant, value))
            {
                OnPropertyChanged(nameof(NoteBackground));
                OnPropertyChanged(nameof(NoteBorderBrush));
                OnPropertyChanged(nameof(NoteHeaderBackground));
            }
        }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    public string NoteBackground => IsImportant ? "#FFD8D4" : "#FFF3B5";
    public string NoteBorderBrush => IsImportant ? "#CB5B53" : "#C9B760";
    public string NoteHeaderBackground => IsImportant ? "#F7BBB5" : "#F7E28B";
}
