using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using BueroCockpit.Data;

namespace BueroCockpit.Converters;

public sealed class DeskThumbnailImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = AppPaths.ResolveDeskItemPath(value as string);
        return ThumbnailBitmapCache.Load(path);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
