namespace BueroCockpit.Models;

public sealed class MobileInboxEntry
{
    public string Id { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public string JsonPath { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string Status { get; init; } = "new";
    public string CustomerName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Title { get; init; } = "Mobile Besichtigung";
    public string Category { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public IReadOnlyList<MobileInboxPreviewItem> PhotoPreviews { get; init; } = Array.Empty<MobileInboxPreviewItem>();
    public IReadOnlyList<MobileInboxPreviewItem> SketchPreviews { get; init; } = Array.Empty<MobileInboxPreviewItem>();
    public IReadOnlyList<string> OriginalPhotoPaths { get; init; } = Array.Empty<string>();

    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
    public string DisplayCustomerName => string.IsNullOrWhiteSpace(CustomerName) ? "Unbekannter Kunde" : CustomerName;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Mobile Besichtigung" : Title;
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    public bool HasPhotoPreviews => PhotoPreviews.Count > 0;
    public bool HasSketchPreviews => SketchPreviews.Count > 0;
    public string DisplayStatusText => HasAttachmentIssues ? "Fehler" : Status.Trim().ToLowerInvariant() switch
    {
        "approved" or "released" or "freigegeben" => "Freigegeben",
        "imported" or "processed" or "done" or "verarbeitet" or "uebernommen" or "übernommen" => "Übernommen",
        "cleanup" or "ready-for-cleanup" or "bereinigung" => "Übernommen",
        "error" or "failed" or "fehlerhaft" => "Fehler",
        _ => "Neu"
    };
    public string StatusBadgeBackground => DisplayStatusText switch
    {
        "Fehler" => "#FFF1F0",
        "Freigegeben" => "#E8F1FF",
        "Übernommen" => "#E9F7EF",
        _ => "#F4F1EA"
    };
    public string StatusBadgeBorderBrush => DisplayStatusText switch
    {
        "Fehler" => "#E48A8A",
        "Freigegeben" => "#8FB8F6",
        "Übernommen" => "#88C9A1",
        _ => "#DAD4C7"
    };
    public string StatusBadgeForeground => DisplayStatusText switch
    {
        "Fehler" => "#B42318",
        "Freigegeben" => "#2457A6",
        "Übernommen" => "#1F7A3F",
        _ => "#6E6255"
    };
    public bool HasAttachmentIssues =>
        PhotoPreviews.Any(item => item.HasIssue) ||
        SketchPreviews.Any(item => item.HasIssue) ||
        OriginalPhotoPaths.Any(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path));
    public string AttachmentIssueText
    {
        get
        {
            var missingCount =
                PhotoPreviews.Count(item => item.IsMissing || item.IsDetailMissing) +
                SketchPreviews.Count(item => item.IsMissing || item.IsDetailMissing) +
                OriginalPhotoPaths.Count(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path));
            var unreadableCount = PhotoPreviews.Count(item => item.IsUnreadable) +
                                  SketchPreviews.Count(item => item.IsUnreadable);
            var parts = new List<string>();
            if (missingCount == 1)
            {
                parts.Add("1 referenzierte Datei fehlt");
            }
            else if (missingCount > 1)
            {
                parts.Add($"{missingCount} referenzierte Dateien fehlen");
            }

            if (unreadableCount == 1)
            {
                parts.Add("1 Vorschau ist nicht lesbar");
            }
            else if (unreadableCount > 1)
            {
                parts.Add($"{unreadableCount} Vorschauen sind nicht lesbar");
            }

            return string.Join(" · ", parts);
        }
    }
    public bool HasAttachmentIssueText => !string.IsNullOrWhiteSpace(AttachmentIssueText);
    public string MobileAttachmentOverviewText
    {
        get
        {
            var originalCount = OriginalPhotoPaths.Count(path => !IsAnnotatedPath(path));
            var annotatedCount = OriginalPhotoPaths.Count(IsAnnotatedPath);
            var parts = new List<string>();
            if (originalCount == 1)
            {
                parts.Add("1 Originalfoto");
            }
            else if (originalCount > 1)
            {
                parts.Add($"{originalCount} Originalfotos");
            }

            if (annotatedCount == 1)
            {
                parts.Add("1 markierte Version");
            }
            else if (annotatedCount > 1)
            {
                parts.Add($"{annotatedCount} markierte Versionen");
            }

            if (SketchPreviews.Count == 1)
            {
                parts.Add("1 Skizze");
            }
            else if (SketchPreviews.Count > 1)
            {
                parts.Add($"{SketchPreviews.Count} Skizzen");
            }

            return parts.Count == 0 ? "Keine Anhänge referenziert." : string.Join(" · ", parts);
        }
    }

    private static bool IsAnnotatedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/annotated/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("-markiert", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MobileInboxPreviewItem
{
    public string FileName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string DetailPath { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool Exists => !string.IsNullOrWhiteSpace(Path) && File.Exists(Path);
    public string EffectiveDetailPath => !string.IsNullOrWhiteSpace(DetailPath) ? DetailPath : Path;
    public bool DetailExists => !string.IsNullOrWhiteSpace(EffectiveDetailPath) && File.Exists(EffectiveDetailPath);
    public bool IsMissing => !Exists;
    public bool IsDetailMissing => !DetailExists;
    public bool IsUnreadable => Exists && IsPreviewImagePath(Path) && !CanLoadBitmap(Path);
    public bool HasIssue => IsMissing || IsDetailMissing || IsUnreadable || IsDetailUnreadable;
    public bool HasPreviewImage => Exists && !IsUnreadable;
    public bool HasPreviewMessage => !HasPreviewImage;
    public string DisplayKind => Kind switch
    {
        "annotated" => "Markiertes Foto",
        "sketches" => "Skizze",
        "drawing" => "Skizzen-Rohdaten",
        "file" => "Sonstige Datei",
        _ => "Originalfoto"
    };
    public string DisplayName => string.IsNullOrWhiteSpace(FileName) ? DisplayKind : $"{DisplayKind}: {FileName}";
    public string DetailFileName => string.IsNullOrWhiteSpace(EffectiveDetailPath) ? FileName : System.IO.Path.GetFileName(EffectiveDetailPath);
    public string StatusText
    {
        get
        {
            if (!Exists)
            {
                return "Datei fehlt.";
            }

            return IsUnreadable ? "Vorschau ist nicht lesbar." : "Vorschau verfügbar";
        }
    }
    public bool IsDetailUnreadable => DetailExists && IsPreviewImagePath(EffectiveDetailPath) && !CanLoadBitmap(EffectiveDetailPath);
    public bool HasDetailImage => DetailExists && !IsDetailUnreadable;
    public string DetailStatusText
    {
        get
        {
            if (!DetailExists)
            {
                return "Datei fehlt oder kann nicht geladen werden.";
            }

            return IsDetailUnreadable
                ? "Die Detailvorschau ist nicht lesbar."
                : "Detailansicht verfügbar";
        }
    }

    private static bool IsPreviewImagePath(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanLoadBitmap(string path)
    {
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
}
