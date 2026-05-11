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
        var visuals = GetVisualSpec(style);
        DrawBackground(graphics, bounds, selected, hovered, style, visuals);

        var padding = styleDefinition.Padding;
        var imageBounds = new Rectangle(
            bounds.X + padding,
            bounds.Y + padding,
            thumbnailSize,
            thumbnailSize);

        DrawThumbnail(graphics, item, thumbnail, imageBounds, visuals);

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
        ThumbnailVisualSpec visuals)
    {
        var card = Rectangle.Inflate(bounds, -1, -1);
        var borderRect = Rectangle.Inflate(card, -1, -1);

        if (style == GalleryDisplayStyle.Crystal)
        {
            // Crystal is the premium variant: layered shadow, cool gradient fill,
            // diagonal highlight, and stronger hover glow. Keep the layout fixed
            // so the card feels luminous without shifting neighboring items.
            using var shadow = new SolidBrush(Color.FromArgb(selected ? 52 : hovered ? 44 : 36, 30, 55, 90));
            graphics.FillRoundedRectangle(shadow, new Rectangle(card.X + 2, card.Y + 3, card.Width, card.Height), visuals.Radius);

            using var fill = new LinearGradientBrush(
                card,
                Color.FromArgb(selected ? 252 : hovered ? 249 : 247, 255, 255, 255),
                Color.FromArgb(selected ? 226 : hovered ? 221 : 216, 224, 244, 255),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(fill, card, visuals.Radius);

            var highlight = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(10, card.Height / 3));
            using var highBrush = new LinearGradientBrush(
                highlight,
                Color.FromArgb(170, 255, 255, 255),
                Color.FromArgb(18, 255, 255, 255),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(highBrush, highlight, visuals.Radius - 2);

            using var crystalBorderBrush = new LinearGradientBrush(
                borderRect,
                selected ? Color.FromArgb(125, 72, 165, 255) : Color.FromArgb(160, 143, 196, 255),
                selected ? Color.FromArgb(70, 188, 255, 255) : Color.FromArgb(130, 180, 210, 248),
                LinearGradientMode.Horizontal);
            using var border = new Pen(crystalBorderBrush, selected ? 2.2f : hovered ? 1.8f : 1.35f);
            graphics.DrawRoundedRectangle(border, card, visuals.Radius);

            if (hovered && !selected)
            {
                using var hoverPen = new Pen(Color.FromArgb(140, 102, 180, 255), 1.5f);
                graphics.DrawRoundedRectangle(hoverPen, Rectangle.Inflate(card, -2, -2), visuals.Radius - 2);
            }

            return;
        }

        if (visuals.DrawShadow)
        {
            using var shadowBrush = new SolidBrush(Color.FromArgb(selected ? 42 : hovered ? 34 : 28, visuals.ShadowColor));
            graphics.FillRoundedRectangle(shadowBrush, new Rectangle(card.X + 2, card.Y + 3, card.Width, card.Height), visuals.Radius);
        }

        using var fillBrush = new LinearGradientBrush(
            card,
            selected ? Color.FromArgb(248, visuals.FillStart) : hovered ? Color.FromArgb(250, visuals.FillStart) : visuals.FillStart,
            selected ? Color.FromArgb(232, visuals.FillEnd) : hovered ? Color.FromArgb(236, visuals.FillEnd) : visuals.FillEnd,
            LinearGradientMode.Vertical);
        graphics.FillRoundedRectangle(fillBrush, card, visuals.Radius);

        if (visuals.UseGlassOverlay)
        {
            var glassRect = new Rectangle(card.X + 1, card.Y + 1, card.Width - 2, Math.Max(12, card.Height / 2));
            using var glassBrush = new LinearGradientBrush(
                glassRect,
                Color.FromArgb(150, visuals.HighlightColor),
                Color.FromArgb(14, visuals.HighlightColor),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(glassBrush, glassRect, Math.Max(2, visuals.Radius - 2));
        }

        if (visuals.UseBottomBand)
        {
            var bandHeight = Math.Max(14, card.Height / 7);
            var bandRect = new Rectangle(card.X + 1, card.Bottom - bandHeight - 1, card.Width - 2, bandHeight);
            using var bandBrush = new SolidBrush(Color.FromArgb(visuals.BottomBandColor.A, visuals.BottomBandColor));
            graphics.FillRoundedRectangle(bandBrush, bandRect, Math.Max(2, visuals.Radius - 2));
        }

        if (visuals.UseHighlight)
        {
            var highlightRect = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(10, card.Height / 3));
            using var highlightBrush = new LinearGradientBrush(
                highlightRect,
                Color.FromArgb(selected ? 176 : hovered ? 168 : 156, visuals.HighlightColor),
                Color.FromArgb(12, visuals.HighlightColor),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(highlightBrush, highlightRect, Math.Max(2, visuals.Radius - 2));
        }

        var borderStart = selected
            ? Color.FromArgb(Math.Min(255, (int)(visuals.BorderStart.A * 1.1f)), visuals.BorderStart)
            : visuals.BorderStart;
        var borderEnd = hovered
            ? Color.FromArgb(Math.Min(255, (int)(visuals.BorderEnd.A * 1.08f)), visuals.BorderEnd)
            : visuals.BorderEnd;

        using var borderBrush = new LinearGradientBrush(borderRect, borderStart, borderEnd, LinearGradientMode.Horizontal);
        using var borderPen = new Pen(borderBrush, visuals.BorderThickness + (selected ? 1 : hovered ? 1 : 0));
        graphics.DrawRoundedRectangle(borderPen, card, visuals.Radius);

        if (visuals.UseGlow || hovered || selected)
        {
            var glowRect = Rectangle.Inflate(card, -2, -2);
            using var glowPen = new Pen(Color.FromArgb(selected ? 85 : hovered ? 70 : 45, visuals.GlowColor), visuals.BorderThickness + 1.5f);
            graphics.DrawRoundedRectangle(glowPen, glowRect, Math.Max(2, visuals.Radius - 2));
        }
    }

    private static void DrawThumbnail(Graphics graphics, ImageItem item, Image? thumbnail, Rectangle imageBounds, ThumbnailVisualSpec visuals)
    {
        var imageBackground = visuals.UseGlassOverlay
            ? Color.FromArgb(232, 240, 248)
            : visuals.UseGlow
                ? Color.FromArgb(239, 242, 251)
                : Color.FromArgb(238, 241, 245);

        using var backgroundBrush = new SolidBrush(imageBackground);
        graphics.FillRectangle(backgroundBrush, imageBounds);

        if (thumbnail != null)
        {
            graphics.DrawImage(thumbnail, imageBounds);
        }
        else
        {
            var text = item.HasError ? "\u65e0\u6cd5\u52a0\u8f7d" : "\u52a0\u8f7d\u4e2d";
            TextRenderer.DrawText(
                graphics,
                text,
                SystemFonts.MessageBoxFont,
                imageBounds,
                item.HasError ? Color.FromArgb(176, 50, 50) : Color.FromArgb(96, 104, 116),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        using var border = new Pen(visuals.ImageBorderColor);
        graphics.DrawRectangle(border, imageBounds);
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

    private static ThumbnailVisualSpec GetVisualSpec(GalleryDisplayStyle style)
    {
        return style switch
        {
            GalleryDisplayStyle.Rounded => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(240, 244, 248),
                BorderStart: Color.FromArgb(194, 205, 218),
                BorderEnd: Color.FromArgb(172, 190, 210),
                GlowColor: Color.FromArgb(112, 144, 196),
                ShadowColor: Color.FromArgb(42, 30, 45, 70),
                HighlightColor: Color.FromArgb(180, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 16,
                BorderThickness: 1,
                DrawShadow: true,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(206, 213, 223)),
            GalleryDisplayStyle.Shadow => new ThumbnailVisualSpec(
                FillStart: Color.FromArgb(255, 255, 255),
                FillEnd: Color.FromArgb(235, 239, 244),
                BorderStart: Color.FromArgb(204, 211, 222),
                BorderEnd: Color.FromArgb(181, 191, 205),
                GlowColor: Color.FromArgb(108, 129, 165),
                ShadowColor: Color.FromArgb(55, 26, 34, 50),
                HighlightColor: Color.FromArgb(140, 255, 255, 255),
                BottomBandColor: Color.FromArgb(0, 0, 0, 0),
                Radius: 10,
                BorderThickness: 1,
                DrawShadow: true,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(206, 213, 223)),
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
                DrawShadow: false,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(130, 150, 176)),
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
                DrawShadow: true,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: true,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(206, 201, 190)),
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
                DrawShadow: true,
                UseHighlight: true,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: true,
                ImageBorderColor: Color.FromArgb(176, 198, 224)),
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
                DrawShadow: true,
                UseHighlight: true,
                UseGlow: true,
                UseBottomBand: false,
                UseGlassOverlay: true,
                ImageBorderColor: Color.FromArgb(184, 206, 236)),
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
                DrawShadow: true,
                UseHighlight: true,
                UseGlow: true,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(148, 138, 235)),
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
                DrawShadow: false,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(210, 216, 224)),
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
                DrawShadow: false,
                UseHighlight: false,
                UseGlow: false,
                UseBottomBand: false,
                UseGlassOverlay: false,
                ImageBorderColor: Color.FromArgb(210, 216, 224))
        };
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
    bool DrawShadow,
    bool UseHighlight,
    bool UseGlow,
    bool UseBottomBand,
    bool UseGlassOverlay,
    Color ImageBorderColor);

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

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
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
