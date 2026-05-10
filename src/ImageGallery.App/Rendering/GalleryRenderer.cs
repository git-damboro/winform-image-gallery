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
        int thumbnailSize)
    {
        DrawBackground(graphics, bounds, selected, hovered, style);

        var padding = style == GalleryDisplayStyle.Compact ? 6 : 10;
        var imageBounds = new Rectangle(
            bounds.X + padding,
            bounds.Y + padding,
            thumbnailSize,
            thumbnailSize);

        DrawThumbnail(graphics, item, thumbnail, imageBounds);

        var textBounds = new Rectangle(
            bounds.X + padding,
            imageBounds.Bottom + 6,
            bounds.Width - padding * 2,
            Math.Max(0, bounds.Bottom - imageBounds.Bottom - padding));

        DrawMetadata(graphics, baseFont, item, textBounds, style);
    }

    private static void DrawBackground(
        Graphics graphics,
        Rectangle bounds,
        bool selected,
        bool hovered,
        GalleryDisplayStyle style)
    {
        var card = Rectangle.Inflate(bounds, -1, -1);

        if (style == GalleryDisplayStyle.Crystal)
        {
            using var shadow = new SolidBrush(Color.FromArgb(35, 30, 55, 90));
            graphics.FillRoundedRectangle(shadow, new Rectangle(card.X + 2, card.Y + 3, card.Width, card.Height), 10);

            using var fill = new LinearGradientBrush(
                card,
                Color.FromArgb(248, 255, 255, 255),
                Color.FromArgb(218, 224, 244, 255),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(fill, card, 10);

            var highlight = new Rectangle(card.X + 2, card.Y + 2, card.Width - 4, Math.Max(10, card.Height / 3));
            using var highBrush = new LinearGradientBrush(
                highlight,
                Color.FromArgb(160, 255, 255, 255),
                Color.FromArgb(20, 255, 255, 255),
                LinearGradientMode.Vertical);
            graphics.FillRoundedRectangle(highBrush, highlight, 8);

            using var border = new Pen(selected ? Color.FromArgb(45, 122, 255) : Color.FromArgb(150, 188, 205, 235), selected ? 2f : 1f);
            graphics.DrawRoundedRectangle(border, card, 10);

            if (hovered && !selected)
            {
                using var hoverPen = new Pen(Color.FromArgb(120, 80, 150, 255), 1.4f);
                graphics.DrawRoundedRectangle(hoverPen, Rectangle.Inflate(card, -2, -2), 8);
            }

            return;
        }

        var fillColor = selected
            ? Color.FromArgb(224, 238, 255)
            : hovered ? Color.FromArgb(248, 250, 253) : Color.White;
        var borderColor = selected ? Color.FromArgb(32, 110, 230) : Color.FromArgb(212, 218, 226);

        using var brush = new SolidBrush(fillColor);
        using var pen = new Pen(borderColor);
        graphics.FillRoundedRectangle(brush, card, 6);
        graphics.DrawRoundedRectangle(pen, card, 6);
    }

    private static void DrawThumbnail(Graphics graphics, ImageItem item, Image? thumbnail, Rectangle imageBounds)
    {
        using var backgroundBrush = new SolidBrush(Color.FromArgb(238, 241, 245));
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

        using var border = new Pen(Color.FromArgb(210, 216, 224));
        graphics.DrawRectangle(border, imageBounds);
    }

    private static void DrawMetadata(Graphics graphics, Font baseFont, ImageItem item, Rectangle textBounds, GalleryDisplayStyle style)
    {
        var lineHeight = style == GalleryDisplayStyle.Compact ? 15 : 17;
        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        using var detailFont = new Font(baseFont.FontFamily, Math.Max(7.5f, baseFont.Size - 1f), FontStyle.Regular);

        var nameRect = new Rectangle(textBounds.X, textBounds.Y, textBounds.Width, lineHeight);
        TextRenderer.DrawText(graphics, item.FileName, baseFont, nameRect, Color.FromArgb(32, 38, 46), flags);

        var sizeAndType = $"{HumanReadableSizeFormatter.Format(item.FileSizeBytes)}  {item.ImageType}";
        var detailRect = new Rectangle(textBounds.X, nameRect.Bottom, textBounds.Width, lineHeight);
        TextRenderer.DrawText(graphics, sizeAndType, detailFont, detailRect, Color.FromArgb(92, 101, 115), flags);

        if (style != GalleryDisplayStyle.Compact)
        {
            var dimensionRect = new Rectangle(textBounds.X, detailRect.Bottom, textBounds.Width, lineHeight);
            TextRenderer.DrawText(graphics, item.DimensionsText, detailFont, dimensionRect, Color.FromArgb(112, 120, 132), flags);
        }
    }
}

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
