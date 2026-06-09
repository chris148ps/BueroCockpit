using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using BueroCockpit.Models;

namespace BueroCockpit.Services;

public sealed class ThumbnailService
{
    private const int ThumbnailWidth = 160;
    private const int ThumbnailHeight = 110;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp"
    };

    public string? EnsureThumbnail(AttachmentItem attachment)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(attachment.StoredPath) || !File.Exists(attachment.StoredPath))
            {
                return null;
            }

            var extension = Path.GetExtension(attachment.StoredPath);
            if (ImageExtensions.Contains(extension))
            {
                return EnsureImageThumbnail(attachment);
            }

            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return EnsurePdfThumbnail(attachment);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thumbnail generation failed for '{attachment.StoredPath}': {ex}");
        }

        return null;
    }

    private static string? EnsureImageThumbnail(AttachmentItem attachment)
    {
        var thumbnailPath = GetThumbnailPath(attachment);
        if (IsCurrent(attachment.StoredPath, thumbnailPath))
        {
            return thumbnailPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);

        using var source = new Bitmap(attachment.StoredPath);
        using var renderTarget = new RenderTargetBitmap(new PixelSize(ThumbnailWidth, ThumbnailHeight), new Vector(96, 96));
        using (var context = renderTarget.CreateDrawingContext())
        {
            var canvas = new Rect(0, 0, ThumbnailWidth, ThumbnailHeight);
            context.DrawRectangle(Brushes.White, null, canvas);
            context.DrawImage(source, new Rect(source.Size), GetContainedRect(source.Size, canvas));
        }

        renderTarget.Save(thumbnailPath, 92);
        File.SetLastWriteTimeUtc(thumbnailPath, File.GetLastWriteTimeUtc(attachment.StoredPath));
        return thumbnailPath;
    }

    private static string? EnsurePdfThumbnail(AttachmentItem attachment)
    {
        // Kept separate so a robust cross-platform PDF renderer can be added without touching image thumbnails.
        Debug.WriteLine($"PDF thumbnail rendering is not implemented yet for '{attachment.StoredPath}'.");
        return IsCurrent(attachment.StoredPath, attachment.ThumbnailPath) ? attachment.ThumbnailPath : null;
    }

    private static bool IsCurrent(string sourcePath, string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(sourcePath) || !File.Exists(thumbnailPath))
        {
            return false;
        }

        return File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(sourcePath);
    }

    private static string GetThumbnailPath(AttachmentItem attachment)
    {
        var attachmentDirectory = Path.GetDirectoryName(attachment.StoredPath) ?? string.Empty;
        var thumbnailDirectory = Path.Combine(attachmentDirectory, "Thumbnails");
        return Path.Combine(thumbnailDirectory, $"{attachment.Id}.png");
    }

    private static Rect GetContainedRect(Size imageSize, Rect canvas)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return canvas;
        }

        var scale = Math.Min(canvas.Width / imageSize.Width, canvas.Height / imageSize.Height);
        var width = imageSize.Width * scale;
        var height = imageSize.Height * scale;
        var x = canvas.X + (canvas.Width - width) / 2;
        var y = canvas.Y + (canvas.Height - height) / 2;

        return new Rect(x, y, width, height);
    }
}
