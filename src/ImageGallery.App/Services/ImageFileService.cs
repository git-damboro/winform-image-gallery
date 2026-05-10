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

    private static ImageItem CreateItem(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new ImageItem(filePath, fileName, 0, extension, errorMessage: "文件不存在");
            }

            if (!FileFormatPolicy.IsSupported(extension))
            {
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: "不支持的图片格式");
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, image.Width, image.Height);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
            {
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: ex.Message);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ImageItem(filePath, fileName, 0, extension, errorMessage: ex.Message);
        }
    }
}
