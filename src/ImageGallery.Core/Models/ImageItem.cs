namespace ImageGallery.Core.Models;

public sealed class ImageItem
{
    public ImageItem(
        string filePath,
        string fileName,
        long fileSizeBytes,
        string extension,
        int? width = null,
        int? height = null,
        string? errorMessage = null)
    {
        Id = Guid.NewGuid();
        FilePath = filePath;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Extension = extension;
        Width = width;
        Height = height;
        ErrorMessage = errorMessage;
    }

    public Guid Id { get; }

    public string FilePath { get; }

    public string FileName { get; }

    public long FileSizeBytes { get; }

    public string Extension { get; }

    public int? Width { get; }

    public int? Height { get; }

    public string? ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string ImageType => Extension.TrimStart('.').ToUpperInvariant();

    public string DimensionsText => Width.HasValue && Height.HasValue
        ? $"{Width.Value} x {Height.Value}"
        : "Unknown";
}
