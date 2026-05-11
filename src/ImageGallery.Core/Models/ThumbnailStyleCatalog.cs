namespace ImageGallery.Core.Models;

public static class ThumbnailStyleCatalog
{
    private static readonly ThumbnailSelectionVisualProfile SelectionVisualProfile = new(
        OverlayArgb: unchecked((int)0x4A184A96),
        BorderArgb: unchecked((int)0xFF2A71DC),
        GlowArgb: unchecked((int)0x882A71DC),
        BorderThickness: 2,
        BorderRadius: 0);

    private static readonly IReadOnlyList<ThumbnailStyleDefinition> VisibleStyles = new[]
    {
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Default,
            "Default",
            "Standard card style with the baseline look",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 6,
            CardVisualProfile: new ThumbnailCardVisualProfile(245, 24, 0, 12, 4),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Rounded,
            "Rounded",
            "Softer rounded card corners",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 16,
            CardVisualProfile: new ThumbnailCardVisualProfile(245, 26, 0, 14, 4),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Shadow,
            "Shadow",
            "Heavier shadow and stronger depth",
            Padding: 10,
            Gap: 16,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 10,
            CardVisualProfile: new ThumbnailCardVisualProfile(236, 30, 8, 84, 18),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Border,
            "Border",
            "Clean card with a more obvious edge",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 4,
            CardVisualProfile: new ThumbnailCardVisualProfile(250, 84, 0, 10, 4),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Polaroid,
            "Polaroid",
            "Photo-frame style with a larger bottom margin",
            Padding: 11,
            Gap: 18,
            TextTopSpacing: 8,
            TextLineHeight: 18,
            CornerRadius: 4,
            CardVisualProfile: new ThumbnailCardVisualProfile(240, 18, 0, 18, 6),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Glass,
            "Glass",
            "Soft translucent glass card",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12,
            CardVisualProfile: new ThumbnailCardVisualProfile(154, 34, 10, 20, 6),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Crystal,
            "Crystal",
            "Cool crystal card with brighter edge light",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12,
            CardVisualProfile: new ThumbnailCardVisualProfile(190, 58, 26, 26, 8),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Neon,
            "Neon",
            "Higher-contrast neon glow",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12,
            CardVisualProfile: new ThumbnailCardVisualProfile(235, 76, 44, 18, 6),
            SelectionVisualProfile: SelectionVisualProfile),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Minimal,
            "Minimal",
            "Flat minimal card style",
            Padding: 6,
            Gap: 10,
            TextTopSpacing: 4,
            TextLineHeight: 18,
            CornerRadius: 4,
            CardVisualProfile: new ThumbnailCardVisualProfile(255, 14, 0, 8, 3),
            SelectionVisualProfile: SelectionVisualProfile)
    };

    private static readonly ThumbnailStyleDefinition CompactFallback = new(
        GalleryDisplayStyle.Compact,
        "Compact",
        "Compatibility fallback for the old compact style",
        Padding: 6,
        Gap: 8,
        TextTopSpacing: 4,
        TextLineHeight: 18,
        CornerRadius: 4,
        CardVisualProfile: new ThumbnailCardVisualProfile(255, 14, 0, 8, 3),
        SelectionVisualProfile: SelectionVisualProfile);

    public static IReadOnlyList<ThumbnailStyleDefinition> All => VisibleStyles;

    public static GalleryDisplayStyle ResolveVisibleStyle(GalleryDisplayStyle style)
    {
        return style == GalleryDisplayStyle.Compact ? GalleryDisplayStyle.Minimal : style;
    }

    public static ThumbnailStyleDefinition Get(GalleryDisplayStyle style)
    {
        if (style == GalleryDisplayStyle.Compact)
        {
            return CompactFallback;
        }

        foreach (var definition in VisibleStyles)
        {
            if (definition.Value == style)
            {
                return definition;
            }
        }

        return VisibleStyles[0];
    }
}
