using System.Drawing;
using System.Windows.Forms;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Forms;

public sealed class PreviewForm : Form
{
    private readonly PictureBox _pictureBox = new();
    private readonly Label _errorLabel = new();
    private readonly PreviewNavigationOverlay _overlay = new();
    private IReadOnlyList<ImageItem> _items = Array.Empty<ImageItem>();
    private int _currentIndex = -1;
    private Image? _currentImage;

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
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

        _overlay.Dock = DockStyle.Fill;
        _overlay.NavigateRequested += OverlayOnNavigateRequested;

        _pictureBox.Controls.Add(_overlay);
        Controls.Add(_pictureBox);
        Controls.Add(_errorLabel);
        _overlay.BringToFront();
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

    public void ShowImage(IReadOnlyList<ImageItem> items, ImageItem item, Point cursorLocation, bool pinned = false)
    {
        _items = items.Count == 0 ? new[] { item } : items;
        _currentIndex = ResolveCurrentIndex(item);
        IsPinned = pinned;
        FormBorderStyle = pinned ? FormBorderStyle.SizableToolWindow : FormBorderStyle.None;
        ShowInTaskbar = pinned;
        _errorLabel.Visible = false;
        SetPreviewTitle();

        var screen = Screen.FromPoint(cursorLocation).WorkingArea;
        var maxWidth = Math.Min(900, screen.Width - 80);
        var maxHeight = Math.Min(700, screen.Height - 80);
        Size = new Size(Math.Max(360, maxWidth), Math.Max(260, maxHeight));

        var x = Math.Min(cursorLocation.X + 18, screen.Right - Width);
        var y = Math.Min(cursorLocation.Y + 18, screen.Bottom - Height);
        Location = new Point(Math.Max(screen.Left, x), Math.Max(screen.Top, y));

        ShowCurrentImage();

        if (!Visible)
        {
            Show();
        }
        else
        {
            Activate();
        }
    }

    public void ShowImage(ImageItem item, Point cursorLocation, bool pinned = false)
    {
        ShowImage(new[] { item }, item, cursorLocation, pinned);
    }

    public void HidePreview()
    {
        if (!IsPinned)
        {
            Hide();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Left)
        {
            Navigate(-1);
            return true;
        }

        if (keyData == Keys.Right)
        {
            Navigate(1);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OverlayOnNavigateRequested(object? sender, int delta)
    {
        Navigate(delta);
    }

    private void Navigate(int delta)
    {
        if (_items.Count <= 1 || _currentIndex < 0)
        {
            return;
        }

        _currentIndex = PreviewNavigationPolicy.Move(_currentIndex, _items.Count, delta);
        ShowCurrentImage();
    }

    private void ShowCurrentImage()
    {
        if (_currentIndex < 0 || _currentIndex >= _items.Count)
        {
            return;
        }

        var item = _items[_currentIndex];

        _pictureBox.Image = null;
        _currentImage?.Dispose();
        _currentImage = null;
        _errorLabel.Visible = false;
        _overlay.UpdateState(_items, _currentIndex);

        try
        {
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            _currentImage = CreatePreviewBitmap(source, ClientSize.Width, ClientSize.Height);
            _pictureBox.Image = _currentImage;
            SetPreviewTitle();
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
        {
            Size = new Size(420, 220);
            _errorLabel.Text = $"\u65e0\u6cd5\u9884\u89c8\u56fe\u7247\r\n{ex.Message}";
            _errorLabel.Visible = true;
            SetPreviewTitle();
        }
    }

    private int ResolveCurrentIndex(ImageItem item)
    {
        if (_items.Count == 0)
        {
            return -1;
        }

        for (var index = 0; index < _items.Count; index++)
        {
            if (_items[index].Id == item.Id)
            {
                return index;
            }
        }

        return 0;
    }

    private void SetPreviewTitle()
    {
        if (_currentIndex < 0 || _items.Count == 0)
        {
            Text = IsPinned ? "\u5927\u56fe\u67e5\u770b" : "\u56fe\u7247\u9884\u89c8";
            return;
        }

        var item = _items[_currentIndex];
        var prefix = IsPinned ? "\u5927\u56fe\u67e5\u770b" : "\u56fe\u7247\u9884\u89c8";
        Text = _items.Count > 1
            ? $"{prefix} ({_currentIndex + 1}/{_items.Count}) - {item.FileName}"
            : $"{prefix} - {item.FileName}";
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
