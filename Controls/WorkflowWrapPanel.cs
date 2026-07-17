using Avalonia;
using Avalonia.Controls;

namespace BueroCockpit.Controls;

public sealed class WorkflowWrapPanel : Panel
{
    private const double HorizontalSpacing = 14;
    private const double VerticalSpacing = 8;
    protected override Size MeasureOverride(Size availableSize)
    {
        var availableWidth = NormalizeAvailableWidth(availableSize.Width);
        foreach (var child in Children)
        {
            child.Measure(new Size(availableWidth, availableSize.Height));
        }

        return MeasureRows(availableWidth);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var availableWidth = Math.Max(0, finalSize.Width);
        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;

        foreach (var child in Children)
        {
            var childWidth = Math.Min(child.DesiredSize.Width, availableWidth);
            var childHeight = child.DesiredSize.Height;
            var requiredWidth = x <= 0 ? childWidth : HorizontalSpacing + childWidth;

            if (x > 0 && x + requiredWidth > availableWidth + 0.5)
            {
                y += rowHeight + VerticalSpacing;
                x = 0;
                rowHeight = 0;
            }

            if (x > 0)
            {
                x += HorizontalSpacing;
            }

            child.Classes.Set("WorkflowRowStart", x <= 0);
            var bounds = new Rect(x, y, childWidth, childHeight);
            child.Arrange(bounds);
            x += childWidth;
            rowHeight = Math.Max(rowHeight, childHeight);
        }

        return finalSize;
    }

    private Size MeasureRows(double availableWidth)
    {
        var width = 0d;
        var height = 0d;
        var rowWidth = 0d;
        var rowHeight = 0d;

        foreach (var child in Children)
        {
            var childWidth = Math.Min(child.DesiredSize.Width, availableWidth);
            var requiredWidth = rowWidth <= 0 ? childWidth : HorizontalSpacing + childWidth;
            if (rowWidth > 0 && rowWidth + requiredWidth > availableWidth + 0.5)
            {
                width = Math.Max(width, rowWidth);
                height += rowHeight + VerticalSpacing;
                rowWidth = 0;
                rowHeight = 0;
                requiredWidth = childWidth;
            }

            rowWidth += requiredWidth;
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        width = Math.Max(width, rowWidth);
        height += rowHeight;
        return new Size(width, height);
    }

    private static double NormalizeAvailableWidth(double width)
    {
        return double.IsNaN(width) || double.IsInfinity(width)
            ? double.MaxValue
            : Math.Max(0, width);
    }
}
