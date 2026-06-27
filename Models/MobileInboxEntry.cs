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
}

public sealed class MobileInboxPreviewItem
{
    public string FileName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool Exists => !string.IsNullOrWhiteSpace(Path) && File.Exists(Path);
}
