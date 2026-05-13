namespace ImageGallery.Core.Models;

public enum ImageDeleteAction { Single, Multiple, ClearAll }

public sealed class ImageDeletedEventArgs : EventArgs
{
    public ImageDeletedEventArgs(ImageDeleteAction action, IReadOnlyList<string> filePaths)
    {
        Action = action;
        FilePaths = filePaths;
    }

    public ImageDeleteAction Action { get; }

    public IReadOnlyList<string> FilePaths { get; }
}
