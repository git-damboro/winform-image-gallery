namespace ImageGallery.Core.Models;

public readonly record struct ImageContentInfo(
    double? MaxObjectDiameter,
    double? MaxObjectArea,
    string? SizeSpec)
{
    public static ImageContentInfo Empty => new(null, null, null);
}
