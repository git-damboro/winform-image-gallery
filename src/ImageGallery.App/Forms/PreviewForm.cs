using System.Drawing;
using System.Windows.Forms;
using ImageGallery.Core.Models;

namespace ImageGallery.App.Forms;

public sealed class PreviewForm : Form
{
    private readonly PictureBox _pictureBox = new();
    private readonly Label _errorLabel = new();
    private Image? _currentImage;

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(24, 28, 36);
        Padding = new Padding(8);
        Size = new Size(720, 520);

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _pictureBox.BackColor = Color.FromArgb(18, 22, 30);

        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.TextAlign = ContentAlignment.MiddleCenter;
        _errorLabel.ForeColor = Color.White;
        _errorLabel.Visible = false;

        Controls.Add(_pictureBox);
        Controls.Add(_errorLabel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentImage?.Dispose();
        }

        base.Dispose(disposing);
    }

    public bool IsPinned { get; private set; }

    public void ShowImage(ImageItem item, Point cursorLocation, bool pinned = false)
    {
        IsPinned = pinned;
        FormBorderStyle = pinned ? FormBorderStyle.SizableToolWindow : FormBorderStyle.None;
        ShowInTaskbar = pinned;
        _pictureBox.Image = null;
        _currentImage?.Dispose();
        _currentImage = null;
        _errorLabel.Visible = false;
        Text = pinned ? "\u5927\u56fe\u67e5\u770b" : "\u56fe\u7247\u9884\u89c8";

        var screen = Screen.FromPoint(cursorLocation).WorkingArea;
        var maxWidth = Math.Min(900, screen.Width - 80);
        var maxHeight = Math.Min(700, screen.Height - 80);
        Size = new Size(Math.Max(360, maxWidth), Math.Max(260, maxHeight));

        try
        {
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            _currentImage = CreatePreviewBitmap(source, ClientSize.Width, ClientSize.Height);
            _pictureBox.Image = _currentImage;
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
        {
            Size = new Size(420, 220);
            _errorLabel.Text = $"\u65e0\u6cd5\u9884\u89c8\u56fe\u7247\r\n{ex.Message}";
            _errorLabel.Visible = true;
        }

        var x = Math.Min(cursorLocation.X + 18, screen.Right - Width);
        var y = Math.Min(cursorLocation.Y + 18, screen.Bottom - Height);
        Location = new Point(Math.Max(screen.Left, x), Math.Max(screen.Top, y));

        if (!Visible)
        {
            Show();
        }
        else
        {
            Activate();
        }
    }

    public void HidePreview()
    {
        if (!IsPinned)
        {
            Hide();
        }
    }

    private static Bitmap CreatePreviewBitmap(Image source, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(maxWidth / (double)source.Width, maxHeight / (double)source.Height);
        scale = Math.Min(1d, scale);

        var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        var bitmap = new Bitmap(targetWidth, targetHeight);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(18, 22, 30));
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);

        return bitmap;
    }
}
