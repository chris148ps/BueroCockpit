namespace BueroCockpit.Models;

public sealed class AttachmentItem : ObservableObject
{
    private string _fileName = string.Empty;
    private string _storedPath = string.Empty;
    private string _thumbnailPath = string.Empty;
    private string _fileType = string.Empty;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string FileName { get => _fileName; set => SetProperty(ref _fileName, value); }
    public string StoredPath { get => _storedPath; set => SetProperty(ref _storedPath, value); }
    public string ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (SetProperty(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
                OnPropertyChanged(nameof(HasNoThumbnail));
            }
        }
    }
    public string FileType { get => _fileType; set => SetProperty(ref _fileType, value); }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public string FileTypeBadge => string.IsNullOrWhiteSpace(FileType) ? "FILE" : FileType.TrimStart('.').ToUpperInvariant();
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath);
    public bool HasNoThumbnail => !HasThumbnail;
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

    public string CardBackground => IsSelected ? "#EEF5FF" : "#FFFFFF";
    public string CardBorderBrush => IsSelected ? "#7CB7FF" : "#E0E0E5";
}
