using System.Drawing;
using System.Windows.Forms;
using ImageGallery.App.Controls;
using ImageGallery.App.Services;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Forms;

public sealed class MainForm : Form
{
    private readonly List<ImageItem> _items = new();
    private readonly ImageFileService _imageFileService = new();
    private readonly ImageGalleryControl _galleryControl = new();
    private readonly Label _countLabel = new();
    private readonly TrackBar _sizeTrackBar = new();
    private readonly ComboBox _styleComboBox = new();
    private PreviewForm? _previewForm;
    private PreviewForm? _pinnedPreviewForm;

    public MainForm()
    {
        Text = "WinForm Image Gallery";
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = BuildToolbar();
        Controls.Add(_galleryControl);
        Controls.Add(toolbar);

        toolbar.Dock = DockStyle.Top;
        _galleryControl.Dock = DockStyle.Fill;
        _galleryControl.PreviewRequested += GalleryControlOnPreviewRequested;
        _galleryControl.ImageOpenRequested += GalleryControlOnImageOpenRequested;
        _galleryControl.PreviewCloseRequested += GalleryControlOnPreviewCloseRequested;

        UpdateCountLabel();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewForm?.Dispose();
            _pinnedPreviewForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(8),
            WrapContents = false
        };

        var addButton = new Button
        {
            Text = "\u6dfb\u52a0\u56fe\u7247",
            AutoSize = true
        };
        addButton.Click += AddButtonOnClick;

        var deleteButton = new Button
        {
            Text = "\u5220\u9664\u9009\u4e2d",
            AutoSize = true
        };
        deleteButton.Click += DeleteButtonOnClick;

        var simulateButton = new Button
        {
            Text = "\u6a21\u62df1\u4e07\u5f20",
            AutoSize = true
        };
        simulateButton.Click += SimulateButtonOnClick;

        var sizeLabel = new Label
        {
            Text = "\u7f29\u7565\u56fe",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(16, 8, 0, 0)
        };

        _sizeTrackBar.Minimum = 64;
        _sizeTrackBar.Maximum = 256;
        _sizeTrackBar.TickFrequency = 32;
        _sizeTrackBar.Value = 128;
        _sizeTrackBar.Width = 160;
        _sizeTrackBar.ValueChanged += (_, _) => _galleryControl.ThumbnailSize = _sizeTrackBar.Value;

        _styleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _styleComboBox.Width = 120;
        _styleComboBox.DataSource = Enum.GetValues(typeof(GalleryDisplayStyle));
        _styleComboBox.SelectedItem = GalleryDisplayStyle.Crystal;
        _styleComboBox.SelectedValueChanged += (_, _) =>
        {
            if (_styleComboBox.SelectedItem is GalleryDisplayStyle style)
            {
                _galleryControl.DisplayStyle = style;
            }
        };

        _countLabel.AutoSize = true;
        _countLabel.Margin = new Padding(16, 8, 0, 0);

        toolbar.Controls.Add(addButton);
        toolbar.Controls.Add(deleteButton);
        toolbar.Controls.Add(simulateButton);
        toolbar.Controls.Add(sizeLabel);
        toolbar.Controls.Add(_sizeTrackBar);
        toolbar.Controls.Add(_styleComboBox);
        toolbar.Controls.Add(_countLabel);

        return toolbar;
    }

    private async void AddButtonOnClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = FileFormatPolicy.FileDialogFilter,
            Multiselect = true,
            Title = "\u9009\u62e9\u56fe\u7247"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            var newItems = await _imageFileService.CreateItemsAsync(dialog.FileNames);
            _items.AddRange(newItems);
            _galleryControl.SetItems(_items);
            UpdateCountLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u6dfb\u52a0\u56fe\u7247\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void DeleteButtonOnClick(object? sender, EventArgs e)
    {
        var indexes = _galleryControl.GetSelectedIndexesDescending();
        foreach (var index in indexes)
        {
            if (index >= 0 && index < _items.Count)
            {
                _items.RemoveAt(index);
            }
        }

        _galleryControl.RemoveSelectedIndexes(indexes);
        _galleryControl.SetItems(_items);
        UpdateCountLabel();
        ClosePreview();
    }

    private void SimulateButtonOnClick(object? sender, EventArgs e)
    {
        _items.Clear();
        _items.AddRange(_imageFileService.CreatePlaceholderItems(10_000));
        _galleryControl.SetItems(_items);
        UpdateCountLabel();
        ClosePreview();
    }

    private void GalleryControlOnPreviewRequested(object? sender, ImageItem item)
    {
        try
        {
            _previewForm ??= new PreviewForm();
            _previewForm.ShowImage(item, Cursor.Position);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u9884\u89c8\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void GalleryControlOnImageOpenRequested(object? sender, ImageItem item)
    {
        try
        {
            _pinnedPreviewForm ??= new PreviewForm();
            _pinnedPreviewForm.ShowImage(item, Cursor.Position, pinned: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u5927\u56fe\u67e5\u770b\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void GalleryControlOnPreviewCloseRequested(object? sender, EventArgs e)
    {
        ClosePreview();
    }

    private void ClosePreview()
    {
        if (_previewForm == null)
        {
            return;
        }

        _previewForm.HidePreview();
    }

    private void UpdateCountLabel()
    {
        _countLabel.Text = $"\u5171 {_items.Count:N0} \u5f20";
    }
}
