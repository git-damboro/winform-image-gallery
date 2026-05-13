namespace ImageGallery.Core.Models;

public enum ImageSelectionMode
{
    HoverPreview,
    PinnedOpen
}

public sealed class ImageSelectedEventArgs : EventArgs
{
    public ImageSelectedEventArgs(string filePath, ImageSelectionMode mode)
    {
        FilePath = filePath;
        Mode = mode;
    }

    public string FilePath { get; }

    public ImageSelectionMode Mode { get; }
}
