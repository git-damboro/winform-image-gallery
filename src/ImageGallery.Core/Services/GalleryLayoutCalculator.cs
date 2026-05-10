using ImageGallery.Core.Models;

namespace ImageGallery.Core.Services;

public static class GalleryLayoutCalculator
{
    public static GalleryLayout Calculate(
        int itemCount,
        int viewportWidth,
        int viewportHeight,
        int scrollX,
        int scrollY,
        GalleryLayoutOptions options)
    {
        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        }

        if (options.ThumbnailSize <= 0 || options.TextAreaHeight < 0 || options.Padding < 0 || options.Gap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (itemCount == 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return new GalleryLayout(itemCount, 1, 0, 0, 0, options, VisibleRange.Empty);
        }

        var columns = Math.Max(1, (viewportWidth + options.Gap) / options.StepX);
        var rows = (int)Math.Ceiling(itemCount / (double)columns);
        var totalColumns = Math.Min(itemCount, columns);
        var totalWidth = totalColumns * options.CardWidth + Math.Max(0, totalColumns - 1) * options.Gap;
        var totalHeight = rows * options.CardHeight + Math.Max(0, rows - 1) * options.Gap;

        var firstRow = Math.Max(0, scrollY / options.StepY);
        var lastRow = Math.Min(rows - 1, Math.Max(0, (scrollY + viewportHeight) / options.StepY));
        var firstIndex = Math.Min(itemCount - 1, firstRow * columns);
        var lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * columns - 1);

        return new GalleryLayout(
            itemCount,
            columns,
            rows,
            totalWidth,
            totalHeight,
            options,
            new VisibleRange(firstIndex, lastIndex));
    }
}
