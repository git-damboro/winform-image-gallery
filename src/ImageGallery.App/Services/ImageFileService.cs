using System.Drawing;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Services;

public sealed class ImageFileService
{
    public Task<IReadOnlyList<ImageItem>> CreateItemsAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.Run<IReadOnlyList<ImageItem>>(() => paths.Select(CreateItem).ToArray());
    }

    public IReadOnlyList<ImageItem> CreatePlaceholderItems(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new ImageItem(
                filePath: $"virtual://performance/{index:D5}.jpg",
                fileName: $"performance-{index:D5}.jpg",
                fileSizeBytes: 0,
                extension: ".jpg",
                width: 1920,
                height: 1080,
                errorMessage: "\u6027\u80fd\u9a8c\u8bc1\u5360\u4f4d\u56fe"))
            .ToArray();
    }

    private static ImageItem CreateItem(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new ImageItem(filePath, fileName, 0, extension, errorMessage: "\u6587\u4ef6\u4e0d\u5b58\u5728");
            }

            if (!FileFormatPolicy.IsRecognized(extension))
            {
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: FileFormatPolicy.GetSupportMessage(extension));
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, image.Width, image.Height);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
            {
                var message = FileFormatPolicy.IsNativelyDecodable(extension)
                    ? ex.Message
                    : $"{FileFormatPolicy.GetSupportMessage(extension)}: {ex.Message}";
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: message);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ImageItem(filePath, fileName, 0, extension, errorMessage: ex.Message);
        }
    }
}
