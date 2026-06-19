using BueroCockpit.Data;

namespace BueroCockpit.Models;

public sealed class DeskItem : ObservableObject
{
    private string _type = "Note";
    private string _displayName = string.Empty;
    private string _text = string.Empty;
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private string _referencePath = string.Empty;
    private string _thumbnailPath = string.Empty;
    private string _linkedTaskId = string.Empty;
    private string _contentHash = string.Empty;
    private double _x = 48;
    private double _y = 48;
    private double _width = 300;
    private double _height = 210;
    private bool _isImportant;
    private bool _isRenaming;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(IsFileCard));
                OnPropertyChanged(nameof(IsPdfCard));
                OnPropertyChanged(nameof(IsImageCard));
                OnPropertyChanged(nameof(IsNoteCard));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(HasFileReference));
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(HasPreviewText));
                OnPropertyChanged(nameof(HasSimplePreviewPlaceholder));
                OnPropertyChanged(nameof(FileBadgeText));
                OnPropertyChanged(nameof(FileKindLabel));
                OnPropertyChanged(nameof(ImportantToggleText));
                OnPropertyChanged(nameof(HasLinkedTask));
                OnPropertyChanged(nameof(FileCardBackground));
                OnPropertyChanged(nameof(FileCardBorderBrush));
                OnPropertyChanged(nameof(FileCardPreviewBackground));
                OnPropertyChanged(nameof(FileCardBadgeBackground));
                OnPropertyChanged(nameof(FileCardBadgeBorderBrush));
            }
        }
    }
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(HasPreviewText));
                OnPropertyChanged(nameof(HasSimplePreviewPlaceholder));
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }
    public string DisplayName
    {
        get
        {
            if (IsRenaming)
            {
                return _displayName ?? string.Empty;
            }


            if (!string.IsNullOrWhiteSpace(_displayName))
            {
                return _displayName;
            }

            if (IsNoteCard)
            {
                return "Notizzettel";
            }

            if (HasFileName)
            {
                return FileName;
            }

            var fileName = Path.GetFileName(ResolveDeskFilePath());
            return string.IsNullOrWhiteSpace(fileName) ? "Datei" : fileName;
        }
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(FileStatusText));
            }
        }
    }
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(PdfPath));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(FileStatusText));
                OnPropertyChanged(nameof(HasFileReference));
                OnPropertyChanged(nameof(HasFile));
                OnPropertyChanged(nameof(HasMissingFile));
                OnPropertyChanged(nameof(HasPreviewText));
                OnPropertyChanged(nameof(HasSimplePreviewPlaceholder));
                OnPropertyChanged(nameof(FileBadgeText));
                OnPropertyChanged(nameof(FileKindLabel));
                OnPropertyChanged(nameof(HasLinkedTask));
            }
        }
    }
    public string PdfPath
    {
        get => FilePath;
        set => FilePath = value;
    }
    public string FileName
    {
        get => _fileName;
        set
        {
            if (SetProperty(ref _fileName, value))
            {
                OnPropertyChanged(nameof(HasFileName));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(FileStatusText));
                OnPropertyChanged(nameof(FileBadgeText));
                OnPropertyChanged(nameof(FileKindLabel));
                OnPropertyChanged(nameof(PreviewText));
            }
        }
    }
    public string ReferencePath
    {
        get => _referencePath;
        set
        {
            if (SetProperty(ref _referencePath, value))
            {
                OnPropertyChanged(nameof(HasReferencePath));
                OnPropertyChanged(nameof(HasLinkedTask));
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
                OnPropertyChanged(nameof(PdfThumbnailPath));
                OnPropertyChanged(nameof(HasPreviewThumbnail));
                OnPropertyChanged(nameof(HasPdfThumbnail));
                OnPropertyChanged(nameof(HasSimplePreviewPlaceholder));
            }
        }
    }
    public string LinkedTaskId
    {
        get => _linkedTaskId;
        set
        {
            if (SetProperty(ref _linkedTaskId, value))
            {
                OnPropertyChanged(nameof(HasLinkedTask));
            }
        }
    }
    public string ContentHash
    {
        get => _contentHash;
        set => SetProperty(ref _contentHash, value);
    }
    public string PdfThumbnailPath
    {
        get => ThumbnailPath;
        set => ThumbnailPath = value;
    }
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
                OnPropertyChanged(nameof(FileCardBackground));
                OnPropertyChanged(nameof(FileCardBorderBrush));
                OnPropertyChanged(nameof(FileCardPreviewBackground));
                OnPropertyChanged(nameof(FileCardBadgeBackground));
                OnPropertyChanged(nameof(FileCardBadgeBorderBrush));
                OnPropertyChanged(nameof(ImportantToggleText));
            }
        }
    }
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming == value)
            {
                return;
            }

            _isRenaming = value;
            OnPropertyChanged(nameof(IsRenaming));
            OnPropertyChanged(nameof(IsDisplayNameVisible));
        }
    }

    public bool IsDisplayNameVisible => !IsRenaming;

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

    public bool IsNoteCard => string.Equals(Type, "Note", StringComparison.OrdinalIgnoreCase);
    public bool IsPdfCard => string.Equals(Type, "Pdf", StringComparison.OrdinalIgnoreCase);
    public bool IsImageCard => string.Equals(Type, "Image", StringComparison.OrdinalIgnoreCase);
    public bool IsFileCard => !IsNoteCard;
    public bool HasFileReference => IsFileCard && !string.IsNullOrWhiteSpace(FilePath);
    public bool HasFile => HasFileReference && File.Exists(ResolveDeskFilePath());
    public bool HasMissingFile => HasFileReference && !HasFile;
    public bool HasFileName => !string.IsNullOrWhiteSpace(FileName);
    public bool HasReferencePath => !string.IsNullOrWhiteSpace(ReferencePath);
    public bool HasLinkedTask =>
        !string.IsNullOrWhiteSpace(LinkedTaskId) ||
        TryGetLinkedTaskId(ReferencePath) is not null ||
        TryGetLinkedTaskId(FilePath) is not null;
    public bool HasPreviewText => IsFileCard && !string.IsNullOrWhiteSpace(Text);
    public bool HasPreviewThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ResolveDeskThumbnailPath());
    public bool HasPdfThumbnail => HasPreviewThumbnail;
    public bool HasSimplePreviewPlaceholder => IsFileCard && !HasPreviewThumbnail && !HasPreviewText;
    public string FileDisplayName => HasMissingFile ? "Datei nicht gefunden" : DisplayName;
    public string FileStatusText => HasMissingFile ? $"Datei nicht gefunden: {ResolveDeskFilePath()}" : string.Empty;
    public string PreviewText => HasPreviewText ? Text.Trim() : string.Empty;
    public string FileBadgeText
    {
        get
        {
            var fileName = HasFileName ? FileName : Path.GetFileName(ResolveDeskFilePath());
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "DATEI";
            }

            return extension.TrimStart('.').ToUpperInvariant();
        }
    }
    public string FileKindLabel => IsPdfCard ? "PDF" : IsImageCard ? "BILD" : FileBadgeText;
    public string ImportantToggleText => IsImportant ? "Wichtig-Markierung entfernen" : "Als wichtig markieren";
    public string NoteBackground => IsImportant ? "#FFD8D4" : "#FFF3B5";
    public string NoteBorderBrush => IsImportant ? "#CB5B53" : "#C9B760";
    public string NoteHeaderBackground => IsImportant ? "#F7BBB5" : "#F7E28B";
    public string FileCardBackground => IsImportant ? "#FFF1EE" : "#FCFBF8";
    public string FileCardBorderBrush => IsImportant ? "#B42318" : "#DAD4C7";
    public string FileCardPreviewBackground => IsImportant ? "#FFF7F5" : "#FFFFFF";
    public string FileCardBadgeBackground => IsImportant ? "#FFD9D4" : "#E4E0D8";
    public string FileCardBadgeBorderBrush => IsImportant ? "#D92D20" : "#CFC9BB";

    private static string? TryGetLinkedTaskId(string? path)
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

    private string ResolveDeskFilePath()
    {
        return AppPaths.ResolveDeskItemPath(FilePath, HasFileName ? FileName : null);
    }

    private string ResolveDeskThumbnailPath()
    {
        return AppPaths.ResolveDeskItemPath(ThumbnailPath);
    }
}
