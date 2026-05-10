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

    public void ShowImage(ImageItem item, Point cursorLocation)
    {
        _pictureBox.Image = null;
        _currentImage?.Dispose();
        _currentImage = null;
        _errorLabel.Visible = false;

        try
        {
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            _currentImage = new Bitmap(source);
            _pictureBox.Image = _currentImage;

            var maxWidth = Math.Min(900, Screen.FromPoint(cursorLocation).WorkingArea.Width - 80);
            var maxHeight = Math.Min(700, Screen.FromPoint(cursorLocation).WorkingArea.Height - 80);
            Size = new Size(Math.Max(360, maxWidth), Math.Max(260, maxHeight));
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
        {
            Size = new Size(420, 220);
            _errorLabel.Text = $"无法预览图片\r\n{ex.Message}";
            _errorLabel.Visible = true;
        }

        var screen = Screen.FromPoint(cursorLocation).WorkingArea;
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
}
