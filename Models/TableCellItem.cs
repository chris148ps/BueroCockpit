namespace BueroCockpit.Models;

public sealed class TableCellItem
{
    public TableCellItem(string key, string text, double width, bool isStatus)
    {
        Key = key;
        Text = text;
        Width = width;
        IsStatus = isStatus;
    }

    public string Key { get; }
    public string Text { get; }
    public string ToolTipText => Text;
    public double Width { get; }
    public bool IsStatus { get; }
    public bool IsNotStatus => !IsStatus;
}
