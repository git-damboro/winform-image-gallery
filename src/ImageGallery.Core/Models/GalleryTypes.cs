namespace ImageGallery.Core.Models;

public enum GalleryDisplayStyle
{
    Default,
    Compact,
    Crystal,
    Rounded,
    Shadow,
    Border,
    Polaroid,
    Glass,
    Neon,
    Minimal
}

[Flags]
public enum ThumbnailInfoFields
{
    None = 0,
    FileName = 1,
    FileSize = 2,
    ImageType = 4,
    Dimensions = 8,
    All = FileName | FileSize | ImageType | Dimensions
}

public readonly record struct GalleryLayoutOptions(
    int ThumbnailSize,
    int TextAreaHeight,
    int Padding,
    int Gap)
{
    public int CardWidth => ThumbnailSize + Padding * 2;

    public int CardHeight => ThumbnailSize + TextAreaHeight + Padding * 3;

    public int StepX => CardWidth + Gap;

    public int StepY => CardHeight + Gap;
}

public readonly record struct GalleryRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int x, int y)
    {
        return x >= X && x < Right && y >= Y && y < Bottom;
    }
}

public readonly record struct VisibleRange(int FirstIndex, int LastIndex)
{
    public static VisibleRange Empty => new(-1, -1);

    public bool IsEmpty => FirstIndex < 0 || LastIndex < FirstIndex;
}

public sealed class GalleryLayout
{
    public GalleryLayout(
        int itemCount,
        int columns,
        int rows,
        int totalWidth,
        int totalHeight,
        GalleryLayoutOptions options,
        VisibleRange visibleRange)
    {
        ItemCount = itemCount;
        Columns = columns;
        Rows = rows;
        TotalWidth = totalWidth;
        TotalHeight = totalHeight;
        Options = options;
        VisibleRange = visibleRange;
    }

    public int ItemCount { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int TotalWidth { get; }

    public int TotalHeight { get; }

    public GalleryLayoutOptions Options { get; }

    public VisibleRange VisibleRange { get; }

    public GalleryRect GetItemRect(int index)
    {
        if (index < 0 || index >= ItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var row = index / Columns;
        var column = index % Columns;

        return new GalleryRect(
            X: column * Options.StepX,
            Y: row * Options.StepY,
            Width: Options.CardWidth,
            Height: Options.CardHeight);
    }

    public int IndexFromPoint(int x, int y)
    {
        if (x < 0 || y < 0 || Columns <= 0)
        {
            return -1;
        }

        var column = x / Options.StepX;
        var row = y / Options.StepY;

        if (column >= Columns || row >= Rows)
        {
            return -1;
        }

        var index = row * Columns + column;
        if (index < 0 || index >= ItemCount)
        {
            return -1;
        }

        return GetItemRect(index).Contains(x, y) ? index : -1;
    }
}
