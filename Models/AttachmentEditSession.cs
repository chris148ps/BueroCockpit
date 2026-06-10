namespace BueroCockpit.Models;

public sealed class AttachmentEditSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AttachmentId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string ExportPath { get; set; } = string.Empty;
    public string? BackupPath { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public string OriginalHashAtExport { get; set; } = string.Empty;
    public string ExportedFileHashAtExport { get; set; } = string.Empty;
    public string Status { get; set; } = "Exported";
    public DateTime? ImportedAt { get; set; }
}
