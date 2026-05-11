using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Rendering;

public sealed class GalleryRenderer
{
    public void DrawCard(
        Graphics graphics,
        Font baseFont,
        ImageItem item,
        Image? thumbnail,
        Rectangle bounds,
        bool selected,
        bool hovered,
        GalleryDisplayStyle style,
        ThumbnailInfoFields infoFields,
        int thumbnailSize)
    {
        var styleDefinition = ThumbnailStyleCatalog.Get(style);
        var visuals = GetVisualSpec(style, styleDefinition.CardVisualProfile);

        DrawBackground(
            graphics,
            bounds,
            selected,
            hovered,
            style,
            styleDefinition.CardVisualProfile,
            styleDefinition.SelectionVisualProfile,
            visuals);

        var padding = styleDefinition.Padding;
        var imageBounds = new Rectangle(
            bounds.X + padding,
            bounds.Y + padding,
            thumbnailSize,
            thumbnailSize);

        DrawThumbnail(graphics, item, thumbnail, imageBounds);

        if (infoFields != ThumbnailInfoFields.None)
        {
            var textBounds = new Rectangle(
                bounds.X + padding,
                imageBounds.Bottom + styleDefinition.TextTopSpacing,
                bounds.Width - padding * 2,
                Math.Max(0, bounds.Bottom - imageBounds.Bottom - padding));

            DrawMetadata(graphics, baseFont, item, textBounds, styleDefinition, infoFields);
        }
    }

    private static void DrawBackground(
        Graphics graphics,
        Rectangle bounds,
        bool selected,
        bool hovered,
        GalleryDisplayStyle style,
        ThumbnailCardVisualProfile profile,
        ThumbnailSelectionVisualProfile selectionProfile,
        ThumbnailVisualSpec visuals)
    {
        var card = Rectangle.Inflate(bounds, -1, -1);
        var borderRect = Rectangle.Inflate(card, -1, -1);
        var radius = visuals.Radius;

        DrawCardShadowLayers(graphics, card, radius, profile, visuals.ShadowColor, selected, hovered);

        var surfaceAlpha = ClampByte(profile.SurfaceAlpha + (selected ? 6 : hovered ? 3 : 0));
        using var fillBrush = new LinearGradientBrush(
            card,
            Color.FromArgb(surfaceAlpha, visuals.FillStart),
            Color.FromArgb(ClampByte(surfaceAlpha - 12), visuals.FillEnd),
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(fillBrush, card, radius);

        if (style == GalleryDisplayStyle.Polaroid && visuals.UseBottomBand)
        {
            DrawPolaroidBand(graphics, card, visuals, selected, hovered);
        }

        if (style == GalleryDisplayStyle.Glass && visuals.UseGlassOverlay)
        {
            DrawGlassSurface(graphics, card, visuals, profile, selected, hovered);
        }

        if (style == GalleryDisplayStyle.Crystal)
        {
            // Crystal is intentionally the strongest card-level glass effect:
            // colder surface, brighter sheen, and a more obvious edge glow.
            DrawCrystalSurface(graphics, card, visuals, profile, selected, hovered);
        }

        if (style == GalleryDisplayStyle.Neon && visuals.UseHighlight)
        {
            DrawNeonSurface(graphics, card, visuals, profile, selected, hovered);
        }

        if (visuals.UseHighlight && style != GalleryDisplayStyle.Crystal && style != GalleryDisplayStyle.Neon)
        {
            DrawSoftHighlight(graphics, card, visuals, profile, selected, hovered);
        }

        using var borderBrush = new LinearGradientBrush(
            borderRect,
            Color.FromArgb(ClampByte(profile.BorderAlpha), visuals.BorderStart),
            Color.FromArgb(ClampByte(profile.BorderAlpha), visuals.BorderEnd),
            LinearGradientMode.Horizontal);
        using var borderPen = new Pen(borderBrush, visuals.BorderThickness + (selected ? 1 : hovered ? 1 : 0));
        graphics.DrawRoundedRectangle(borderPen, card, radius);

        if (selected)
        {
            DrawSelectionLayer(graphics, card, selectionProfile, hovered);
        }

        if (visuals.UseGlow && profile.BorderGlowAlpha > 0)
        {
            DrawBorderGlow(graphics, card, radius, visuals.GlowColor, profile.BorderGlowAlpha, selected, hovered);
        }
    }

    private static void DrawThumbnail(
        Graphics graphics,
        ImageItem item,
        Image? thumbnail,
        Rectangle imageBounds)
    {
        var contentBounds = Rectangle.Inflate(imageBounds, -1, -1);
        var radius = Math.Max(2, Math.Min(4, Math.Min(contentBounds.Width, contentBounds.Height) / 8));

        using var clipPath = RoundedRectangleGraphicsExtensions.CreateRoundedRectanglePath(contentBounds, radius);
        var state = graphics.Save();
        try
        {
            graphics.SetClip(clipPath);

            using var backgroundBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
            graphics.FillPath(backgroundBrush, clipPath);

            if (thumbnail != null)
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(thumbnail, contentBounds);
            }
            else
            {
                var text = item.HasError ? "\u65e0\u6cd5\u52a0\u8f7d" : "\u52a0\u8f7d\u4e2d";
                TextRenderer.DrawText(
                    graphics,
                    text,
                    SystemFonts.MessageBoxFont,
                    contentBounds,
                    item.HasError ? Color.FromArgb(176, 50, 50) : Color.FromArgb(96, 104, 116),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        finally
        {
            graphics.Restore(state);
        }

        using var border = new Pen(Color.FromArgb(120, 208, 214, 222));
        graphics.DrawPath(border, clipPath);
    }

    private static void DrawMetadata(
        Graphics graphics,
        Font baseFont,
        ImageItem item,
        Rectangle textBounds,
        ThumbnailStyleDefinition style,
        ThumbnailInfoFields infoFields)
    {
        var lineHeight = style.TextLineHeight;
        var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        using var detailFont = new Font(baseFont.FontFamily, Math.Max(7.5f, baseFont.Size - 1f), FontStyle.Regular);

        var lines = ThumbnailInfoFormatter.GetLines(item, infoFields);
        for (var index = 0; index < lines.Count; index++)
        {
            var lineRect = new Rectangle(textBounds.X, textBounds.Y + index * lineHeight, textBounds.Width, lineHeight);
            var font = index == 0 && infoFields.HasFlag(ThumbnailInfoFields.FileName) ? baseFont : detailFont;
            var color = index == 0 && infoFields.HasFlag(ThumbnailInfoFields.FileName)
                ? Color.FromArgb(32, 38, 46)
                : Color.FromArgb(92, 101, 115);

            TextRenderer.DrawText(graphics, lines[index], font, lineRect, color, flags);
        }
    }

    private static ThumbnailVisualSpec GetVisualSpec(GalleryDisplayStyle style, ThumbnailCardVisualProfile profile)
    {
        return style switch
        {
            GalleryDisplayStyle.Rounded => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(241, 245, 249),
                BorderStart: Color.FromArgb(194, 205, 218),
                BorderEnd: Color.FromArgb(172, 190, 210),
                GlowColor: Color.FromArgb(112, 144, 196),
                ShadowColor: Color.FromArgb(42, 30, 45, 70),
                HighlightColor: Color.FromArgb(180, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 16,
                BorderThickness: 1,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false),
            GalleryDisplayStyle.Shadow => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(235, 239, 244),
                BorderStart: Color.FromArgb(204, 211, 222),
                BorderEnd: Color.FromArgb(181, 191, 205),
                GlowColor: Color.FromArgb(108, 129, 165),
                ShadowColor: Color.FromArgb(58, 26, 34, 50),
                HighlightColor: Color.FromArgb(140, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 10,
                BorderThickness: 1,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false),
            GalleryDisplayStyle.Border => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(248, 250, 252),
                BorderStart: Color.FromArgb(95, 132, 211),
                BorderEnd: Color.FromArgb(64, 110, 197),
                GlowColor: Color.FromArgb(85, 120, 215),
                ShadowColor: Color.FromArgb(28, 40, 54, 70),
                HighlightColor: Color.FromArgb(120, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 4,
                BorderThickness: 2,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false),
            GalleryDisplayStyle.Polaroid => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 252),
                FillEnd: Color.FromArgb(246, 244, 239),
                BorderStart: Color.FromArgb(221, 215, 203),
                BorderEnd: Color.FromArgb(204, 197, 183),
                GlowColor: Color.FromArgb(110, 118, 130),
                ShadowColor: Color.FromArgb(48, 40, 42, 52),
                HighlightColor: Color.FromArgb(120, 255, 255, 255),
                BottomBandColor: Color.FromArgb(255, 249, 246, 240),
                Radius: 4,
                BorderThickness: 1,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: true,
                UseGlassOverlay: false),
            GalleryDisplayStyle.Glass => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(236, 246, 255),
                FillEnd: Color.FromArgb(214, 231, 250),
                BorderStart: Color.FromArgb(144, 182, 229),
                BorderEnd: Color.FromArgb(121, 162, 216),
                GlowColor: Color.FromArgb(92, 145, 212),
                ShadowColor: Color.FromArgb(30, 32, 48, 72),
                HighlightColor: Color.FromArgb(180, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 12,
                BorderThickness: 1,
                UseHighlight: true,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: true),
            GalleryDisplayStyle.Crystal => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(247, 252, 255),
                FillEnd: Color.FromArgb(217, 232, 250),
                BorderStart: Color.FromArgb(170, 195, 255),
                BorderEnd: Color.FromArgb(112, 178, 248),
                GlowColor: Color.FromArgb(120, 160, 255),
                ShadowColor: Color.FromArgb(50, 28, 48, 84),
                HighlightColor: Color.FromArgb(190, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 12,
                BorderThickness: 2,
                UseHighlight: true,
                UseGlow: true,
                UseBottomBand: false,
                UseGlassOverlay: true),
            GalleryDisplayStyle.Neon => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(244, 240, 255),
                FillEnd: Color.FromArgb(230, 226, 250),
                BorderStart: Color.FromArgb(109, 80, 232),
                BorderEnd: Color.FromArgb(49, 189, 230),
                GlowColor: Color.FromArgb(120, 66, 245),
                ShadowColor: Color.FromArgb(36, 33, 36, 76),
                HighlightColor: Color.FromArgb(130, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 12,
                BorderThickness: 2,
                UseHighlight: true,
                UseGlow: true,
                UseBottomBand: false,
                UseGlassOverlay: false),
            GalleryDisplayStyle.Minimal => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(250, 251, 253),
                BorderStart: Color.FromArgb(218, 222, 228),
                BorderEnd: Color.FromArgb(205, 211, 219),
                GlowColor: Color.FromArgb(90, 120, 170),
                ShadowColor: Color.FromArgb(18, 0, 0, 0),
                HighlightColor: Color.FromArgb(90, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 4,
                BorderThickness: 1,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false),
            _ => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(245, 248, 252),
                BorderStart: Color.FromArgb(212, 218, 226),
                BorderEnd: Color.FromArgb(190, 199, 210),
                GlowColor: Color.FromArgb(108, 138, 188),
                ShadowColor: Color.FromArgb(28, 20, 32, 52),
                HighlightColor: Color.FromArgb(100, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 6,
                BorderThickness: 1,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false)
        };
    }

    private static void DrawCardShadowLayers(
        Graphics graphics,
        Rectangle card,
        int radius,
        ThumbnailCardVisualProfile profile,
        Color shadowColor,
        bool selected,
        bool hovered)
    {
        if (profile.ShadowAlpha <= 0)
        {
            return;
        }

        var layerCount = Math.Max(2, Math.Min(7, (profile.ShadowBlurPx + 2) / 3));
        for (var layer = layerCount; layer >= 1; layer--)
        {
            var alpha = ClampByte(profile.ShadowAlpha * layer / (layerCount + 1));
            alpha = ClampByte(alpha + (selected ? 12 : hovered ? 6 : 0));

            var spread = Math.Max(0, layer - 1);
            var shadowRect = Rectangle.Inflate(card, spread, spread);
            shadowRect.Offset(1 + layer / 4, 2 + layer);

            using var brush = new SolidBrush(Color.FromArgb(alpha, shadowColor));
            graphics.FillRoundedRectangle(brush, shadowRect, radius + Math.Min(4, layer / 2));
        }
    }

    private static void DrawPolaroidBand(Graphics graphics, Rectangle card, ThumbnailVisualSpec visuals, bool selected, bool hovered)
    {
        var bandHeight = Math.Max(12, card.Height / 5);
        var bandRect = new Rectangle(card.X + 1, card.Bottom - bandHeight - 1, card.Width - 2, bandHeight);
        using var bandBrush = new LinearGradientBrush(
            bandRect,
            selected ? Color.FromArgb(255, 255, 250, 244) : hovered ? Color.FromArgb(255, 253, 250, 245) : Color.FromArgb(255, 250, 247, 240),
            visuals.BottomBandColor,
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(bandBrush, bandRect, Math.Max(2, visuals.Radius - 2));
    }

    private static void DrawGlassSurface(
        Graphics graphics,
        Rectangle card,
        ThumbnailVisualSpec visuals,
        ThumbnailCardVisualProfile profile,
        bool selected,
        bool hovered)
    {
        var sheenRect = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(12, card.Height / 2));
        using var sheenBrush = new LinearGradientBrush(
            sheenRect,
            Color.FromArgb(ClampByte(profile.BorderGlowAlpha + (selected ? 24 : hovered ? 16 : 8)), 255, 255, 255),
            Color.FromArgb(14, 210, 235, 255),
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(sheenBrush, sheenRect, Math.Max(2, visuals.Radius - 2));
    }

    private static void DrawCrystalSurface(
        Graphics graphics,
        Rectangle card,
        ThumbnailVisualSpec visuals,
        ThumbnailCardVisualProfile profile,
        bool selected,
        bool hovered)
    {
        var sheenRect = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(14, card.Height / 2));
        using var sheenBrush = new LinearGradientBrush(
            sheenRect,
            Color.FromArgb(ClampByte(profile.BorderGlowAlpha + (selected ? 42 : hovered ? 28 : 18)), 255, 255, 255),
            Color.FromArgb(18, 198, 228, 255),
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(sheenBrush, sheenRect, Math.Max(2, visuals.Radius - 2));

        var beamRect = new Rectangle(card.X + card.Width / 6, card.Y + 1, Math.Max(1, card.Width / 3), Math.Max(1, card.Height - 2));
        using var beamBrush = new LinearGradientBrush(
            beamRect,
            Color.FromArgb(70, 170, 214, 255),
            Color.FromArgb(0, 170, 214, 255),
            LinearGradientMode.Horizontal);
        graphics.FillRoundedRectangle(beamBrush, beamRect, Math.Max(2, visuals.Radius - 2));
    }

    private static void DrawNeonSurface(
        Graphics graphics,
        Rectangle card,
        ThumbnailVisualSpec visuals,
        ThumbnailCardVisualProfile profile,
        bool selected,
        bool hovered)
    {
        var sheenRect = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(12, card.Height / 3));
        using var sheenBrush = new LinearGradientBrush(
            sheenRect,
            Color.FromArgb(ClampByte(profile.BorderGlowAlpha + (selected ? 30 : hovered ? 18 : 10)), 255, 255, 255),
            Color.FromArgb(12, 240, 230, 255),
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(sheenBrush, sheenRect, Math.Max(2, visuals.Radius - 2));
    }

    private static void DrawSoftHighlight(
        Graphics graphics,
        Rectangle card,
        ThumbnailVisualSpec visuals,
        ThumbnailCardVisualProfile profile,
        bool selected,
        bool hovered)
    {
        var highlightRect = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(10, card.Height / 3));
        using var highlightBrush = new LinearGradientBrush(
            highlightRect,
            Color.FromArgb(ClampByte(profile.BorderGlowAlpha + (selected ? 14 : hovered ? 10 : 4)), visuals.HighlightColor),
            Color.FromArgb(8, visuals.HighlightColor),
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(highlightBrush, highlightRect, Math.Max(2, visuals.Radius - 2));
    }

    private static void DrawBorderGlow(
        Graphics graphics,
        Rectangle card,
        int radius,
        Color glowColor,
        int glowAlpha,
        bool selected,
        bool hovered)
    {
        var alpha = ClampByte(glowAlpha + (selected ? 18 : hovered ? 12 : 0));
        using var outerPen = new Pen(Color.FromArgb(alpha, glowColor), 2.2f);
        graphics.DrawRoundedRectangle(outerPen, Rectangle.Inflate(card, 1, 1), Math.Max(2, radius - 1));

        using var innerPen = new Pen(Color.FromArgb(ClampByte(alpha / 2), glowColor), 4f);
        graphics.DrawRoundedRectangle(innerPen, Rectangle.Inflate(card, 2, 2), Math.Max(2, radius - 1));
    }

    private static void DrawSelectionLayer(
        Graphics graphics,
        Rectangle card,
        ThumbnailSelectionVisualProfile selectionProfile,
        bool hovered)
    {
        var overlayAlpha = ClampByte(Color.FromArgb(selectionProfile.OverlayArgb).A + (hovered ? 8 : 0));
        var overlayColor = Color.FromArgb(overlayAlpha, Color.FromArgb(selectionProfile.OverlayArgb));
        using var overlayBrush = new SolidBrush(overlayColor);
        graphics.FillRoundedRectangle(overlayBrush, card, Math.Max(2, selectionProfile.BorderRadius));

        var borderRect = Rectangle.Inflate(card, 2, 2);
        using var glowPen = new Pen(Color.FromArgb(Color.FromArgb(selectionProfile.GlowArgb).A, Color.FromArgb(selectionProfile.GlowArgb)), selectionProfile.BorderThickness + 2);
        graphics.DrawRectangle(glowPen, borderRect);

        using var borderPen = new Pen(Color.FromArgb(selectionProfile.BorderArgb), selectionProfile.BorderThickness);
        graphics.DrawRectangle(borderPen, borderRect);
    }

    private static int ClampByte(int value)
    {
        return Math.Clamp(value, 0, 255);
    }
}

internal readonly record struct ThumbnailVisualSpec(
    Color FillStart,
    Color FillEnd,
    Color BorderStart,
    Color BorderEnd,
    Color GlowColor,
    Color ShadowColor,
    Color HighlightColor,
    Color BottomBandColor,
    int Radius,
    int BorderThickness,
    bool UseHighlight,
    bool UseGlow,
    bool UseBottomBand,
    bool UseGlassOverlay);

internal static class RoundedRectangleGraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);

        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
