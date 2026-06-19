namespace BueroCockpit.Models;

public sealed record BackupListItem(string FilePath, string FileName, string TimestampText, string SizeText, string HintText)
{
    public bool HasHintText => !string.IsNullOrWhiteSpace(HintText);
}
