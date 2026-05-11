namespace ImageGallery.Core.Models;

public static class ThumbnailStyleCatalog
{
    private static readonly IReadOnlyList<ThumbnailStyleDefinition> VisibleStyles = new[]
    {
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Default,
            "Default",
            "标准卡片样式，保持当前基础效果",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 6),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Rounded,
            "Rounded",
            "更圆润的卡片边角",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 16),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Shadow,
            "Shadow",
            "强调阴影层次的卡片样式",
            Padding: 10,
            Gap: 16,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 10),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Border,
            "Border",
            "更明显的描边卡片样式",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 4),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Polaroid,
            "Polaroid",
            "拍立得照片样式，底部留白更明显",
            Padding: 11,
            Gap: 18,
            TextTopSpacing: 8,
            TextLineHeight: 18,
            CornerRadius: 4),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Glass,
            "Glass",
            "磨砂玻璃质感卡片",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Crystal,
            "Crystal",
            "冷色水晶高光卡片，适合强调质感",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Neon,
            "Neon",
            "霓虹发光效果卡片",
            Padding: 10,
            Gap: 14,
            TextTopSpacing: 6,
            TextLineHeight: 20,
            CornerRadius: 12),
        new ThumbnailStyleDefinition(
            GalleryDisplayStyle.Minimal,
            "Minimal",
            "极简扁平样式，信息更克制",
            Padding: 6,
            Gap: 10,
            TextTopSpacing: 4,
            TextLineHeight: 18,
            CornerRadius: 4)
    };

    private static readonly ThumbnailStyleDefinition CompactFallback = new(
        GalleryDisplayStyle.Compact,
        "Compact",
        "兼容旧版紧凑样式",
        Padding: 6,
        Gap: 8,
        TextTopSpacing: 4,
        TextLineHeight: 18,
        CornerRadius: 4);

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
