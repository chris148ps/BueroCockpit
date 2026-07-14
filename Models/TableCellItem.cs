namespace BueroCockpit.Models;

public sealed class TableCellItem
{
    public TableCellItem(string key, string text, double width, bool isStatus)
    {
        Key = key;
        Text = text;
        Width = width;
        IsStatus = isStatus;
        IsCategory = string.Equals(key, "Kategorie", StringComparison.OrdinalIgnoreCase);
    }

    public string Key { get; }
    public string Text { get; }
    public string ToolTipText => Text;
    public double Width { get; }
    public bool IsStatus { get; }
    public bool IsCategory { get; }
    public bool IsCategoryBadgeVisible => IsCategory && !string.IsNullOrWhiteSpace(Text);
    public bool IsPlainText => !IsStatus && !IsCategory;
}
