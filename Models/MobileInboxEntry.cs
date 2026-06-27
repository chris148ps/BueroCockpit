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
    public string Kind { get; init; } = string.Empty;
    public bool Exists => !string.IsNullOrWhiteSpace(Path) && File.Exists(Path);
    public bool IsMissing => !Exists;
    public string DisplayKind => Kind switch
    {
        "annotated" => "Markiertes Foto",
        "sketches" => "Skizze",
        "drawing" => "Skizzen-Rohdaten",
        _ => "Originalfoto"
    };
    public string DisplayName => string.IsNullOrWhiteSpace(FileName) ? DisplayKind : $"{DisplayKind}: {FileName}";
    public string StatusText => Exists ? "Vorschau verfügbar" : $"Datei fehlt: {Path}";
}
