using BueroCockpit.Data;

namespace BueroCockpit.Models;

public sealed class AttachmentItem : ObservableObject
{
    private string _fileName = string.Empty;
    private string _storedPath = string.Empty;
    private string _thumbnailPath = string.Empty;
    private string _fileType = string.Empty;
    private string _contentHash = string.Empty;
    private bool _isSelected;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string FileName
    {
        get => _fileName;
        set
        {
            if (SetProperty(ref _fileName, value))
            {
                OnPropertyChanged(nameof(FileDisplayName));
            }
        }
    }
    public string StoredPath
    {
        get => _storedPath;
        set
        {
            if (SetProperty(ref _storedPath, value))
            {
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(HasNoFile));
                OnPropertyChanged(nameof(HasMissingFile));
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(FileStatusText));
            }
        }
    }
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
    public string ContentHash { get => _contentHash; set => SetProperty(ref _contentHash, value); }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public string FileTypeBadge => string.IsNullOrWhiteSpace(FileType) ? "FILE" : FileType.TrimStart('.').ToUpperInvariant();
    public bool HasFile =>
        !string.IsNullOrWhiteSpace(StoredPath) &&
        File.Exists(AppPaths.ResolveDataPath(StoredPath));
    public bool HasNoFile => !HasFile;
    public bool HasMissingFile => !string.IsNullOrWhiteSpace(StoredPath) && !HasFile;
    public string FileDisplayName => HasMissingFile ? "Datei nicht gefunden" : FileName;
    public string FileStatusText =>
        HasMissingFile ? $"Datei nicht gefunden: {AppPaths.ResolveDataPath(StoredPath)}" : string.Empty;
    public bool HasThumbnail =>
        !string.IsNullOrWhiteSpace(ThumbnailPath) &&
        File.Exists(AppPaths.ResolveDataPath(ThumbnailPath));
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
    public bool IsSelectedForPrint { get; set; }

    public string CardBorderBrush => IsSelected ? "#7CB7FF" : "#E0E0E5";
}
