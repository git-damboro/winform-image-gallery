namespace ImageGallery.Core.Models;

public readonly record struct ThumbnailCardVisualProfile(
    int SurfaceAlpha,
    int BorderAlpha,
    int BorderGlowAlpha,
    int ShadowAlpha,
    int ShadowBlurPx);

public readonly record struct ThumbnailSelectionVisualProfile(
    int OverlayArgb,
    int BorderArgb,
    int GlowArgb,
    int BorderThickness,
    int BorderRadius);

public sealed record ThumbnailStyleDefinition(
    GalleryDisplayStyle Value,
    string Label,
    string Description,
    int Padding,
    int Gap,
    int TextTopSpacing,
    int TextLineHeight,
    int CornerRadius,
    ThumbnailCardVisualProfile CardVisualProfile,
    ThumbnailSelectionVisualProfile SelectionVisualProfile);
