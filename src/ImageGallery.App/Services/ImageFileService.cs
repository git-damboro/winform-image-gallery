using System.Drawing;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Services;

public sealed class ImageFileService
{
    public Task<IReadOnlyList<ImageItem>> CreateItemsAsync(IEnumerable<string> filePaths)
    {
        return CreateItemsAsync(filePaths, progress: null, CancellationToken.None);
    }

    public Task<IReadOnlyList<ImageItem>> CreateItemsAsync(
        IEnumerable<string> filePaths,
        IProgress<ImageImportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var paths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.Run<IReadOnlyList<ImageItem>>(() =>
        {
            var items = new List<ImageItem>(paths.Length);
            var progressPolicy = new TaskProgressUpdatePolicy(paths.Length, minimumStep: 100);
            for (var index = 0; index < paths.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = CreateItem(paths[index]);
                items.Add(item);
                var completed = index + 1;
                if (progressPolicy.ShouldReport(completed))
                {
                    progress?.Report(new ImageImportProgress(completed, paths.Length, item.FileName));
                }
            }

            return items;
        }, cancellationToken);
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
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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

public readonly record struct ImageImportProgress(int Completed, int Total, string CurrentFileName);
