using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using BueroCockpit.Data;

namespace BueroCockpit.Converters;

public sealed class ThumbnailImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = AppPaths.ResolveDataPath(value as string);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
