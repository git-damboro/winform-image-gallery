using ImageGallery.Core.Models;

namespace ImageGallery.Core.Services;

public static class ThumbnailInfoFormatter
{
    public static IReadOnlyList<string> GetLines(ImageItem item, ThumbnailInfoFields fields)
    {
        var lines = new List<string>(4);

        if (fields.HasFlag(ThumbnailInfoFields.FileName))
        {
            lines.Add(item.FileName);
        }

        if (fields.HasFlag(ThumbnailInfoFields.FileSize))
        {
            lines.Add(HumanReadableSizeFormatter.Format(item.FileSizeBytes));
        }

        if (fields.HasFlag(ThumbnailInfoFields.ImageType))
        {
            lines.Add(item.ImageType);
        }

        if (fields.HasFlag(ThumbnailInfoFields.Dimensions))
        {
            lines.Add(item.DimensionsText);
        }

        return lines;
    }
}
