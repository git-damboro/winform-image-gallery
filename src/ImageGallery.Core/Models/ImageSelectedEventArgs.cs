namespace ImageGallery.Core.Models;

public sealed class ImageSelectedEventArgs : EventArgs
{
    public ImageSelectedEventArgs(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
