using ImageGallery.Core.Models;

namespace ImageGallery.Core.Services;

public static class ThumbnailInfoFormatter
{
    public static IReadOnlyList<string> GetLines(ImageItem item, ThumbnailInfoFields fields)
    {
        var lines = new List<string>(7);

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

        if (fields.HasFlag(ThumbnailInfoFields.Diameter) && item.ContentInfo.MaxObjectDiameter.HasValue)
        {
            lines.Add($"直径 {item.ContentInfo.MaxObjectDiameter.Value:F2}");
        }

        if (fields.HasFlag(ThumbnailInfoFields.Area) && item.ContentInfo.MaxObjectArea.HasValue)
        {
            lines.Add($"面积 {item.ContentInfo.MaxObjectArea.Value:F2}");
        }

        if (fields.HasFlag(ThumbnailInfoFields.SizeSpec) && item.ContentInfo.SizeSpec is not null)
        {
            lines.Add($"规格 {item.ContentInfo.SizeSpec}");
        }

        return lines;
    }
}
