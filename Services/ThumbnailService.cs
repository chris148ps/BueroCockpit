using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using BueroCockpit.Data;
using BueroCockpit.Models;
using PDFtoImage;

namespace BueroCockpit.Services;

public sealed class ThumbnailService
{
    private const int ThumbnailWidth = 160;
    private const int ThumbnailHeight = 110;
    private const int PdfPreviewWidth = 420;
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
        var storedPath = AppPaths.ResolveDataPath(attachment.StoredPath);
        try
        {
            if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
            {
                return null;
            }

            var extension = Path.GetExtension(storedPath);
            if (ImageExtensions.Contains(extension))
            {
                return EnsureImageThumbnail(attachment, storedPath);
            }

            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return EnsurePdfThumbnail(attachment, storedPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thumbnail generation failed for '{storedPath}': {ex}");
        }

        return null;
    }

    private static string? EnsureImageThumbnail(AttachmentItem attachment, string storedPath)
    {
        var thumbnailPath = GetThumbnailPath(attachment, storedPath);
        if (IsCurrent(storedPath, thumbnailPath))
        {
            return thumbnailPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);

        using var source = new Bitmap(storedPath);
        using var renderTarget = new RenderTargetBitmap(new PixelSize(ThumbnailWidth, ThumbnailHeight), new Vector(96, 96));
        using (var context = renderTarget.CreateDrawingContext())
        {
            var canvas = new Rect(0, 0, ThumbnailWidth, ThumbnailHeight);
            context.DrawRectangle(Brushes.White, null, canvas);
            context.DrawImage(source, new Rect(source.Size), GetContainedRect(source.Size, canvas));
        }

        renderTarget.Save(thumbnailPath, 92);
        File.SetLastWriteTimeUtc(thumbnailPath, File.GetLastWriteTimeUtc(storedPath));
        return thumbnailPath;
    }

    private static string? EnsurePdfThumbnail(AttachmentItem attachment, string storedPath)
    {
        var currentThumbnailPath = AppPaths.ResolveDataPath(attachment.ThumbnailPath);
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return IsCurrent(storedPath, currentThumbnailPath) ? currentThumbnailPath : null;
        }

        var thumbnailPath = GetThumbnailPath(attachment, storedPath);
        if (IsCurrent(storedPath, thumbnailPath))
        {
            return thumbnailPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        Conversion.SavePng(
            thumbnailPath,
            File.ReadAllBytes(storedPath),
            new Index(0),
            password: null,
            options: new PDFtoImage.RenderOptions { Width = PdfPreviewWidth, WithAspectRatio = true });
        File.SetLastWriteTimeUtc(thumbnailPath, File.GetLastWriteTimeUtc(storedPath));
        return thumbnailPath;
    }

    private static bool IsCurrent(string sourcePath, string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(sourcePath) || !File.Exists(thumbnailPath))
        {
            return false;
        }

        return File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(sourcePath);
    }

    private static string GetThumbnailPath(AttachmentItem attachment, string storedPath)
    {
        var attachmentDirectory = Path.GetDirectoryName(storedPath) ?? string.Empty;
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
