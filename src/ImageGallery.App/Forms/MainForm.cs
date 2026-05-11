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
    private readonly GallerySessionStore _sessionStore = new();
    private readonly ImageGalleryControl _galleryControl = new();
    private readonly Button _addButton = new();
    private readonly Button _selectAllButton = new();
    private readonly Button _deleteButton = new();
    private readonly Label _countLabel = new();
    private readonly TrackBar _sizeTrackBar = new();
    private readonly ComboBox _styleComboBox = new();
    private readonly Button _typeFilterButton = new();
    private readonly ContextMenuStrip _typeFilterDropDown = new();
    private readonly CheckedListBox _typeFilterCheckedList = new();
    private readonly Button _infoDropDownButton = new();
    private readonly ContextMenuStrip _infoDropDown = new();
    private readonly CheckedListBox _infoCheckedList = new();
    private readonly ToolStripStatusLabel _taskStatusLabel = new();
    private readonly ToolStripStatusLabel _taskDetailLabel = new();
    private readonly ToolStripProgressBar _taskProgressBar = new();
    private readonly string _sessionFilePath = GetSessionFilePath();
    private bool _updatingInfoChoices;
    private bool _updatingTypeFilterChoices;
    private bool _restoringSession;
    private PreviewForm? _previewForm;
    private PreviewForm? _pinnedPreviewForm;

    public MainForm()
    {
        Text = "WinForm Image Gallery";
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = BuildToolbar();
        var statusBar = BuildStatusBar();
        Controls.Add(_galleryControl);
        Controls.Add(statusBar);
        Controls.Add(toolbar);

        toolbar.Dock = DockStyle.Top;
        statusBar.Dock = DockStyle.Bottom;
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
            _typeFilterDropDown.Dispose();
            _infoDropDown.Dispose();
            _previewForm?.Dispose();
            _pinnedPreviewForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            _restoringSession = true;
            var savedState = _sessionStore.LoadState(_sessionFilePath);
            ApplyStyleSelection(savedState.DisplayStyle);

            if (savedState.ImagePaths.Count == 0)
            {
                return;
            }

            BeginTask("\u6b63\u5728\u6062\u590d\u56fe\u7247\u5217\u8868", savedState.ImagePaths.Count);
            var savedItems = await CreateItemsWithProgressAsync("\u6b63\u5728\u6062\u590d\u56fe\u7247\u5217\u8868", savedState.ImagePaths);
            _items.Clear();
            _items.AddRange(savedItems);
            _galleryControl.SetItems(_items);
            UpdateCountLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u6062\u590d\u56fe\u7247\u5217\u8868\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _restoringSession = false;
            EndTask();
        }
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

        _addButton.Text = "\u6dfb\u52a0\u56fe\u7247";
        _addButton.AutoSize = true;
        _addButton.Click += AddButtonOnClick;

        _selectAllButton.Text = "\u5168\u9009";
        _selectAllButton.AutoSize = true;
        _selectAllButton.Click += (_, _) => _galleryControl.SelectAllVisible();

        _deleteButton.Text = "\u5220\u9664\u9009\u4e2d";
        _deleteButton.AutoSize = true;
        _deleteButton.Click += DeleteButtonOnClick;

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
        _styleComboBox.Width = 132;
        _styleComboBox.DataSource = ThumbnailStyleCatalog.All;
        _styleComboBox.DisplayMember = nameof(ThumbnailStyleDefinition.Label);
        _styleComboBox.ValueMember = nameof(ThumbnailStyleDefinition.Value);
        _styleComboBox.SelectedItem = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Crystal);
        _styleComboBox.SelectionChangeCommitted += (_, _) =>
        {
            if (_styleComboBox.SelectedItem is ThumbnailStyleDefinition styleDefinition)
            {
                ApplyStyleSelection(styleDefinition.Value);
            }
        };

        ConfigureTypeFilterDropDown();

        ConfigureInfoDropDown();

        _countLabel.AutoSize = true;
        _countLabel.Margin = new Padding(16, 8, 0, 0);

        toolbar.Controls.Add(_addButton);
        toolbar.Controls.Add(_selectAllButton);
        toolbar.Controls.Add(_deleteButton);
        toolbar.Controls.Add(sizeLabel);
        toolbar.Controls.Add(_sizeTrackBar);
        toolbar.Controls.Add(_styleComboBox);
        toolbar.Controls.Add(_typeFilterButton);
        toolbar.Controls.Add(_infoDropDownButton);
        toolbar.Controls.Add(_countLabel);

        return toolbar;
    }

    private StatusStrip BuildStatusBar()
    {
        var statusBar = new StatusStrip
        {
            SizingGrip = false
        };

        _taskStatusLabel.Text = TaskProgressFormatter.FormatIdle();
        _taskStatusLabel.Spring = false;
        _taskStatusLabel.AutoSize = true;
        _taskStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskStatusLabel.Margin = new Padding(0, 3, 8, 3);

        _taskDetailLabel.AutoSize = false;
        _taskDetailLabel.Width = 260;
        _taskDetailLabel.Spring = true;
        _taskDetailLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskDetailLabel.Margin = new Padding(8, 3, 0, 3);

        _taskProgressBar.Minimum = 0;
        _taskProgressBar.Maximum = 1;
        _taskProgressBar.Value = 0;
        _taskProgressBar.Visible = false;
        _taskProgressBar.Width = 180;

        statusBar.Items.Add(_taskStatusLabel);
        statusBar.Items.Add(_taskDetailLabel);
        statusBar.Items.Add(_taskProgressBar);
        return statusBar;
    }

    private void ConfigureInfoDropDown()
    {
        _infoDropDownButton.Text = "\u4fe1\u606f\uff1a\u5168\u90e8";
        _infoDropDownButton.AutoSize = true;
        _infoDropDownButton.Margin = new Padding(8, 3, 0, 3);
        _infoDropDownButton.Click += (_, _) => _infoDropDown.Show(_infoDropDownButton, 0, _infoDropDownButton.Height);

        _infoCheckedList.BorderStyle = BorderStyle.None;
        _infoCheckedList.CheckOnClick = true;
        _infoCheckedList.Width = 150;
        _infoCheckedList.Height = 118;
        _infoCheckedList.Items.AddRange(
            new object[]
            {
                new ThumbnailInfoChoice("\u53ea\u663e\u793a\u56fe\u7247", ThumbnailInfoFields.None),
                new ThumbnailInfoChoice("\u56fe\u7247\u540d\u79f0", ThumbnailInfoFields.FileName),
                new ThumbnailInfoChoice("\u56fe\u7247\u5927\u5c0f", ThumbnailInfoFields.FileSize),
                new ThumbnailInfoChoice("\u56fe\u7247\u7c7b\u578b", ThumbnailInfoFields.ImageType),
                new ThumbnailInfoChoice("\u56fe\u7247\u5c3a\u5bf8", ThumbnailInfoFields.Dimensions)
            });

        _infoDropDown.Items.Add(new ToolStripControlHost(_infoCheckedList)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty
        });

        _infoCheckedList.ItemCheck += (_, e) =>
        {
            if (_updatingInfoChoices)
            {
                return;
            }

            BeginInvoke(new Action(() => ApplyInfoChoices(e.Index, e.NewValue)));
        };

        SetInfoChoices(ThumbnailInfoFields.All);
    }

    private void ConfigureTypeFilterDropDown()
    {
        _typeFilterButton.Text = "\u7c7b\u578b\uff1a\u5168\u90e8";
        _typeFilterButton.AutoSize = true;
        _typeFilterButton.Margin = new Padding(8, 3, 0, 3);
        _typeFilterButton.Click += (_, _) => _typeFilterDropDown.Show(_typeFilterButton, 0, _typeFilterButton.Height);

        _typeFilterCheckedList.BorderStyle = BorderStyle.None;
        _typeFilterCheckedList.CheckOnClick = true;
        _typeFilterCheckedList.Width = 150;
        _typeFilterCheckedList.Height = 180;
        _typeFilterCheckedList.Items.AddRange(
            new object[]
            {
                new ImageTypeFilterChoice("\u5168\u90e8\u7c7b\u578b", Array.Empty<string>()),
                new ImageTypeFilterChoice("PNG", new[] { ".png" }),
                new ImageTypeFilterChoice("JPG / JPEG", new[] { ".jpg", ".jpeg" }),
                new ImageTypeFilterChoice("BMP", new[] { ".bmp" }),
                new ImageTypeFilterChoice("GIF", new[] { ".gif" }),
                new ImageTypeFilterChoice("TIFF", new[] { ".tif", ".tiff" }),
                new ImageTypeFilterChoice("ICO", new[] { ".ico" }),
                new ImageTypeFilterChoice("WEBP", new[] { ".webp" }),
                new ImageTypeFilterChoice("HEIC / HEIF", new[] { ".heic", ".heif" }),
                new ImageTypeFilterChoice("AVIF", new[] { ".avif" })
            });

        _typeFilterDropDown.Items.Add(new ToolStripControlHost(_typeFilterCheckedList)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty
        });

        _typeFilterCheckedList.ItemCheck += (_, e) =>
        {
            if (_updatingTypeFilterChoices)
            {
                return;
            }

            BeginInvoke(new Action(() => ApplyTypeFilterChoices(e.Index, e.NewValue)));
        };

        SetTypeFilterChoices(Array.Empty<string>());
    }

    private void ApplyTypeFilterChoices(int changedIndex, CheckState newValue)
    {
        if (_updatingTypeFilterChoices)
        {
            return;
        }

        if (changedIndex == 0 && newValue == CheckState.Checked)
        {
            SetTypeFilterChoices(Array.Empty<string>());
            return;
        }

        var extensions = new List<string>();
        for (var index = 1; index < _typeFilterCheckedList.Items.Count; index++)
        {
            if (_typeFilterCheckedList.GetItemChecked(index) && _typeFilterCheckedList.Items[index] is ImageTypeFilterChoice choice)
            {
                extensions.AddRange(choice.Extensions);
            }
        }

        if (extensions.Count == 0)
        {
            SetTypeFilterChoices(Array.Empty<string>());
            return;
        }

        SetTypeFilterChoices(extensions);
    }

    private void SetTypeFilterChoices(IEnumerable<string> extensions)
    {
        var normalized = ImageFilterPolicy.NormalizeExtensions(extensions).ToArray();
        var allSelected = normalized.Length == 0;

        _updatingTypeFilterChoices = true;
        try
        {
            _typeFilterCheckedList.SetItemChecked(0, allSelected);
            for (var index = 1; index < _typeFilterCheckedList.Items.Count; index++)
            {
                if (_typeFilterCheckedList.Items[index] is ImageTypeFilterChoice choice)
                {
                    _typeFilterCheckedList.SetItemChecked(index, choice.Extensions.Any(extension => normalized.Contains(extension, StringComparer.OrdinalIgnoreCase)));
                }
            }
        }
        finally
        {
            _updatingTypeFilterChoices = false;
        }

        _galleryControl.VisibleExtensions = normalized;
        _typeFilterButton.Text = allSelected
            ? "\u7c7b\u578b\uff1a\u5168\u90e8"
            : $"\u7c7b\u578b\uff1a{CountSelectedExtensions(normalized)} \u9879";
    }

    private static int CountSelectedExtensions(IEnumerable<string> extensions)
    {
        return extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private void ApplyInfoChoices(int changedIndex, CheckState newValue)
    {
        if (_updatingInfoChoices)
        {
            return;
        }

        if (changedIndex == 0 && newValue == CheckState.Checked)
        {
            SetInfoChoices(ThumbnailInfoFields.None);
            return;
        }

        var fields = ThumbnailInfoFields.None;

        for (var index = 1; index < _infoCheckedList.Items.Count; index++)
        {
            if (_infoCheckedList.GetItemChecked(index) && _infoCheckedList.Items[index] is ThumbnailInfoChoice choice)
            {
                fields |= choice.Field;
            }
        }

        if (fields == ThumbnailInfoFields.None)
        {
            SetInfoChoices(ThumbnailInfoFields.None);
            return;
        }

        SetInfoChoices(fields);
    }

    private void SetInfoChoices(ThumbnailInfoFields fields)
    {
        _updatingInfoChoices = true;
        try
        {
            _infoCheckedList.SetItemChecked(0, fields == ThumbnailInfoFields.None);
            for (var index = 1; index < _infoCheckedList.Items.Count; index++)
            {
                if (_infoCheckedList.Items[index] is ThumbnailInfoChoice choice)
                {
                    _infoCheckedList.SetItemChecked(index, fields.HasFlag(choice.Field));
                }
            }
        }
        finally
        {
            _updatingInfoChoices = false;
        }

        _galleryControl.ThumbnailInfoFields = fields;
        _infoDropDownButton.Text = fields == ThumbnailInfoFields.None
            ? "\u4fe1\u606f\uff1a\u4ec5\u56fe\u7247"
            : $"\u4fe1\u606f\uff1a{CountInfoFields(fields)} \u9879";
    }

    private static int CountInfoFields(ThumbnailInfoFields fields)
    {
        var count = 0;
        if (fields.HasFlag(ThumbnailInfoFields.FileName))
        {
            count++;
        }

        if (fields.HasFlag(ThumbnailInfoFields.FileSize))
        {
            count++;
        }

        if (fields.HasFlag(ThumbnailInfoFields.ImageType))
        {
            count++;
        }

        if (fields.HasFlag(ThumbnailInfoFields.Dimensions))
        {
            count++;
        }

        return count;
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
            BeginTask("\u6b63\u5728\u6dfb\u52a0\u56fe\u7247", dialog.FileNames.Length);
            var newItems = await CreateItemsWithProgressAsync("\u6b63\u5728\u6dfb\u52a0\u56fe\u7247", dialog.FileNames);
            _items.AddRange(newItems);
            _galleryControl.SetItems(_items);
            UpdateCountLabel();
            SaveCurrentSession();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "\u6dfb\u52a0\u56fe\u7247\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndTask();
        }
    }

    private Task<IReadOnlyList<ImageItem>> CreateItemsWithProgressAsync(string taskName, IReadOnlyList<string> filePaths)
    {
        var progress = new Progress<ImageImportProgress>(progress =>
        {
            UpdateTask(taskName, progress.Completed, progress.Total, progress.CurrentFileName);
        });

        return _imageFileService.CreateItemsAsync(filePaths, progress);
    }

    private void BeginTask(string taskName, int total)
    {
        _addButton.Enabled = false;
        _deleteButton.Enabled = false;
        _taskProgressBar.Visible = true;
        _taskProgressBar.Minimum = 0;
        _taskProgressBar.Maximum = Math.Max(1, total);
        _taskProgressBar.Value = 0;
        _taskStatusLabel.Text = TaskProgressFormatter.Format(taskName, 0, total);
        _taskDetailLabel.Text = string.Empty;
    }

    private void UpdateTask(string taskName, int completed, int total, string currentFileName)
    {
        var maximum = Math.Max(1, total);
        if (_taskProgressBar.Maximum != maximum)
        {
            _taskProgressBar.Maximum = maximum;
        }

        _taskProgressBar.Value = Math.Clamp(completed, _taskProgressBar.Minimum, _taskProgressBar.Maximum);
        _taskStatusLabel.Text = TaskProgressFormatter.Format(taskName, completed, total);
        _taskDetailLabel.Text = TrimTaskDetail(currentFileName);
    }

    private void EndTask()
    {
        _taskProgressBar.Value = 0;
        _taskProgressBar.Visible = false;
        _taskStatusLabel.Text = TaskProgressFormatter.FormatIdle();
        _taskDetailLabel.Text = string.Empty;
        _addButton.Enabled = true;
        _deleteButton.Enabled = true;
    }

    private static string TrimTaskDetail(string currentFileName)
    {
        if (string.IsNullOrWhiteSpace(currentFileName))
        {
            return string.Empty;
        }

        const int maxLength = 42;
        return currentFileName.Length <= maxLength
            ? currentFileName
            : $"{currentFileName[..Math.Max(1, maxLength - 1)]}\u2026";
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

        _galleryControl.SetItems(_items);
        UpdateCountLabel();
        SaveCurrentSession();
        ClosePreview();
    }

    private void ApplyStyleSelection(GalleryDisplayStyle style)
    {
        var visibleStyle = ThumbnailStyleCatalog.ResolveVisibleStyle(style);
        _galleryControl.DisplayStyle = visibleStyle;

        var comboSelection = ThumbnailStyleCatalog.All.FirstOrDefault(def => def.Value == visibleStyle)
            ?? ThumbnailStyleCatalog.All.First(def => def.Value == GalleryDisplayStyle.Default);
        _styleComboBox.SelectedItem = comboSelection;

        if (!_restoringSession)
        {
            SaveCurrentSession();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.A))
        {
            _galleryControl.SelectAllVisible();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void GalleryControlOnPreviewRequested(object? sender, ImageItem item)
    {
        try
        {
            _previewForm ??= new PreviewForm();
            _previewForm.ShowImage(GetVisibleItems(), item, Cursor.Position);
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
            _pinnedPreviewForm.ShowImage(GetVisibleItems(), item, Cursor.Position, pinned: true);
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

    private IReadOnlyList<ImageItem> GetVisibleItems()
    {
        return ImageFilterPolicy.FilterByExtensions(_items, _galleryControl.VisibleExtensions);
    }

    private void SaveCurrentSession()
    {
        _sessionStore.Save(
            _sessionFilePath,
            _items
                .Select(item => item.FilePath)
                .Where(path => !path.StartsWith("virtual://", StringComparison.OrdinalIgnoreCase)),
            _galleryControl.DisplayStyle);
    }

    private static string GetSessionFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;

        return Path.Combine(baseDirectory, "MryaoImageGallery", "session.json");
    }

    private sealed class ThumbnailInfoChoice
    {
        public ThumbnailInfoChoice(string text, ThumbnailInfoFields field)
        {
            Text = text;
            Field = field;
        }

        public string Text { get; }

        public ThumbnailInfoFields Field { get; }

        public override string ToString()
        {
            return Text;
        }
    }

    private sealed class ImageTypeFilterChoice
    {
        public ImageTypeFilterChoice(string text, IReadOnlyCollection<string> extensions)
        {
            Text = text;
            Extensions = extensions;
        }

        public string Text { get; }

        public IReadOnlyCollection<string> Extensions { get; }

        public override string ToString()
        {
            return Text;
        }
    }
}
