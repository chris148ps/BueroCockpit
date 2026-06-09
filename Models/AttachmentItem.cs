namespace BueroCockpit.Models;

public sealed class AttachmentItem : ObservableObject
{
    private string _fileName = string.Empty;
    private string _storedPath = string.Empty;
    private string _thumbnailPath = string.Empty;
    private string _fileType = string.Empty;

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
}
