namespace ImageGallery.Core.Models;

public sealed record ThumbnailStyleDefinition(
    GalleryDisplayStyle Value,
    string Label,
    string Description,
    int Padding,
    int Gap,
    int TextTopSpacing,
    int TextLineHeight,
    int CornerRadius);
