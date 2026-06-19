using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace BueroCockpit.Converters;

internal static class ThumbnailBitmapCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        if (Cache.TryGetValue(path, out var cachedEntry) &&
            cachedEntry.LastWriteTimeUtc == lastWriteTimeUtc &&
            cachedEntry.BitmapReference.TryGetTarget(out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            var bitmap = new Bitmap(path);
            Cache[path] = new CacheEntry(lastWriteTimeUtc, new WeakReference<Bitmap>(bitmap));
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private sealed record CacheEntry(DateTime LastWriteTimeUtc, WeakReference<Bitmap> BitmapReference);
}
