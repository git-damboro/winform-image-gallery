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
        var inputs = filePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new GalleryImageInput(path, ImageContentInfo.Empty));
        return CreateItemsAsync(inputs, progress, cancellationToken);
    }

    public Task<IReadOnlyList<ImageItem>> CreateItemsAsync(
        IEnumerable<GalleryImageInput> inputs,
        IProgress<ImageImportProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var normalizedInputs = inputs
            .GroupBy(input => input.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        return Task.Run<IReadOnlyList<ImageItem>>(() =>
        {
            var items = new List<ImageItem>(normalizedInputs.Length);
            var progressPolicy = new TaskProgressUpdatePolicy(normalizedInputs.Length, minimumStep: 1);
            for (var index = 0; index < normalizedInputs.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = CreateItem(normalizedInputs[index]);
                items.Add(item);
                var completed = index + 1;
                if (progressPolicy.ShouldReport(completed))
                {
                    progress?.Report(new ImageImportProgress(completed, normalizedInputs.Length, item.FileName, item));
                }
            }

            return items;
        }, cancellationToken);
    }

    private static ImageItem CreateItem(GalleryImageInput input)
    {
        var filePath = input.FilePath;
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new ImageItem(filePath, fileName, 0, extension, errorMessage: "\u6587\u4ef6\u4e0d\u5b58\u5728", contentInfo: input.ContentInfo);
            }

            if (!FileFormatPolicy.IsRecognized(extension))
            {
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: FileFormatPolicy.GetSupportMessage(extension), contentInfo: input.ContentInfo);
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, image.Width, image.Height, contentInfo: input.ContentInfo);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
            {
                var message = FileFormatPolicy.IsNativelyDecodable(extension)
                    ? ex.Message
                    : $"{FileFormatPolicy.GetSupportMessage(extension)}: {ex.Message}";
                return new ImageItem(filePath, fileName, fileInfo.Length, extension, errorMessage: message, contentInfo: input.ContentInfo);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ImageItem(filePath, fileName, 0, extension, errorMessage: ex.Message, contentInfo: input.ContentInfo);
        }
    }
}

public readonly record struct ImageImportProgress(int Completed, int Total, string CurrentFileName, ImageItem? Item = null);
