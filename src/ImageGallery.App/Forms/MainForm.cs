using System.Drawing;
using System.Windows.Forms;
using ImageGallery.App.Controls;
using ImageGallery.App.Services;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Forms;

public sealed class MainForm : Form
{
    private const float MinScale = 0.1f;
    private const float MaxScale = 50f;
    private const int TrackBarMinimum = 0;
    private const int TrackBarMaximum = 1000;

    private readonly ImageFileService _imageFileService = new();
    private readonly GallerySessionStore _sessionStore = new();
    private readonly ImageGalleryControl _galleryControl = new();
    private readonly Button _addButton = new();
    private readonly Button _selectAllButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _clearAllButton = new();
    private readonly Label _countLabel = new();
    private readonly TrackBar _sizeTrackBar = new();
    private readonly Label _scaleValueLabel = new();
    private readonly ComboBox _styleComboBox = new();
    private readonly Button _typeFilterButton = new();
    private readonly ContextMenuStrip _typeFilterDropDown = new();
    private readonly CheckedListBox _typeFilterCheckedList = new();
    private readonly Button _infoDropDownButton = new();
    private readonly ContextMenuStrip _infoDropDown = new();
    private readonly CheckedListBox _infoCheckedList = new();
    private readonly Label _taskStatusLabel = new();
    private readonly Label _taskDetailLabel = new();
    private readonly ProgressBar _taskProgressBar = new();
    private readonly string _sessionFilePath = GetSessionFilePath();
    private readonly List<ImageItem> _allItems = new();
    private readonly HashSet<string> _allItemPaths = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _currentFilterExtensions = new(StringComparer.OrdinalIgnoreCase);
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

        _galleryControl.ImageDeleted += GalleryControlOnImageDeleted;
        _galleryControl.ImageSelected += GalleryControlOnImageSelected;
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

            BeginTask("正在恢复图片列表", savedState.ImagePaths.Count);
            await Task.Yield();

            await ImportIntoGalleryAsync(
                "正在恢复图片列表",
                CreateGalleryInputs(savedState.ImagePaths),
                LoadMode.Replace,
                saveSessionWhenCompleted: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "恢复图片列表失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        _addButton.Text = "添加图片";
        _addButton.AutoSize = true;
        _addButton.Click += AddButtonOnClick;

        _selectAllButton.Text = "全选";
        _selectAllButton.AutoSize = true;
        _selectAllButton.Click += (_, _) => _galleryControl.SelectAll();

        _deleteButton.Text = "删除选中";
        _deleteButton.AutoSize = true;
        _deleteButton.Click += DeleteButtonOnClick;

        _clearAllButton.Text = "清除全部";
        _clearAllButton.AutoSize = true;
        _clearAllButton.Click += ClearAllButtonOnClick;

        var sizeLabel = new Label
        {
            Text = "缩略图",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(16, 8, 0, 0)
        };

        _sizeTrackBar.Minimum = TrackBarMinimum;
        _sizeTrackBar.Maximum = TrackBarMaximum;
        _sizeTrackBar.TickFrequency = 100;
        _sizeTrackBar.Value = ScaleToTrackBar(1.0f);
        _sizeTrackBar.Width = 160;
        _sizeTrackBar.ValueChanged += (_, _) =>
        {
            var scale = TrackBarToScale(_sizeTrackBar.Value);
            _galleryControl.ThumbnailScale = scale;
            UpdateScaleLabel(scale);
        };

        _scaleValueLabel.AutoSize = true;
        _scaleValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scaleValueLabel.Margin = new Padding(4, 8, 0, 0);
        UpdateScaleLabel(TrackBarToScale(_sizeTrackBar.Value));

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
        toolbar.Controls.Add(_clearAllButton);
        toolbar.Controls.Add(sizeLabel);
        toolbar.Controls.Add(_sizeTrackBar);
        toolbar.Controls.Add(_scaleValueLabel);
        toolbar.Controls.Add(_styleComboBox);
        toolbar.Controls.Add(_typeFilterButton);
        toolbar.Controls.Add(_infoDropDownButton);
        toolbar.Controls.Add(_countLabel);

        return toolbar;
    }

    private Control BuildStatusBar()
    {
        var statusBar = new TableLayoutPanel
        {
            AutoSize = false,
            BackColor = Color.FromArgb(245, 247, 250),
            ColumnCount = 3,
            Dock = DockStyle.Bottom,
            Height = 28,
            Padding = new Padding(8, 3, 8, 3),
            RowCount = 1
        };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
        statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _taskStatusLabel.Text = TaskProgressFormatter.FormatIdle();
        _taskStatusLabel.AutoEllipsis = true;
        _taskStatusLabel.Dock = DockStyle.Fill;
        _taskStatusLabel.ForeColor = Color.FromArgb(46, 55, 70);
        _taskStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskStatusLabel.Margin = Padding.Empty;

        _taskDetailLabel.AutoEllipsis = true;
        _taskDetailLabel.Dock = DockStyle.Fill;
        _taskDetailLabel.ForeColor = Color.FromArgb(88, 98, 112);
        _taskDetailLabel.TextAlign = ContentAlignment.MiddleLeft;
        _taskDetailLabel.Margin = new Padding(8, 0, 8, 0);

        _taskProgressBar.Minimum = 0;
        _taskProgressBar.Maximum = 1;
        _taskProgressBar.Value = 0;
        _taskProgressBar.Visible = false;
        _taskProgressBar.Dock = DockStyle.Fill;
        _taskProgressBar.Margin = new Padding(0, 2, 0, 2);

        statusBar.Controls.Add(_taskStatusLabel, 0, 0);
        statusBar.Controls.Add(_taskDetailLabel, 1, 0);
        statusBar.Controls.Add(_taskProgressBar, 2, 0);
        return statusBar;
    }

    private void ConfigureInfoDropDown()
    {
        _infoDropDownButton.Text = "信息：全部";
        _infoDropDownButton.AutoSize = true;
        _infoDropDownButton.Margin = new Padding(8, 3, 0, 3);
        _infoDropDownButton.Click += (_, _) => _infoDropDown.Show(_infoDropDownButton, 0, _infoDropDownButton.Height);

        _infoCheckedList.BorderStyle = BorderStyle.None;
        _infoCheckedList.CheckOnClick = true;
        _infoCheckedList.Width = 150;
        _infoCheckedList.Height = 178;
        _infoCheckedList.Items.AddRange(
            new object[]
            {
                new ThumbnailInfoChoice("只显示图片", ThumbnailInfoFields.None),
                new ThumbnailInfoChoice("图片名称", ThumbnailInfoFields.FileName),
                new ThumbnailInfoChoice("图片大小", ThumbnailInfoFields.FileSize),
                new ThumbnailInfoChoice("图片类型", ThumbnailInfoFields.ImageType),
                new ThumbnailInfoChoice("图片尺寸", ThumbnailInfoFields.Dimensions),
                new ThumbnailInfoChoice("直径", ThumbnailInfoFields.Diameter),
                new ThumbnailInfoChoice("面积", ThumbnailInfoFields.Area),
                new ThumbnailInfoChoice("规格", ThumbnailInfoFields.SizeSpec)
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
        _typeFilterButton.Text = "类型：全部";
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
                new ImageTypeFilterChoice("全部类型", Array.Empty<string>()),
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
                    _typeFilterCheckedList.SetItemChecked(
                        index,
                        choice.Extensions.Any(extension => normalized.Contains(extension, StringComparer.OrdinalIgnoreCase)));
                }
            }
        }
        finally
        {
            _updatingTypeFilterChoices = false;
        }

        _currentFilterExtensions = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _typeFilterButton.Text = allSelected
            ? "类型：全部"
            : $"类型：{CountSelectedExtensions(normalized)} 项";

        ApplyFilterToGallery();
    }

    private void ApplyFilterToGallery()
    {
        var items = _currentFilterExtensions.Count == 0
            ? _allItems
            : _allItems.Where(item =>
            {
                var extension = Path.GetExtension(item.FilePath);
                return _currentFilterExtensions.Contains(ImageFilterPolicy.NormalizeExtension(extension));
            });

        _galleryControl.LoadItems(items, LoadMode.Replace);
    }

    private static IEnumerable<GalleryImageInput> CreateGalleryInputs(IEnumerable<string> filePaths)
    {
        var index = 0;
        foreach (var filePath in filePaths)
        {
            yield return new GalleryImageInput(filePath, CreateSimulatedContentInfo(filePath, index));
            index++;
        }
    }

    // MainForm is only a demo host for the control. Simulate the caller-provided
    // content metadata here so the Diameter/Area/SizeSpec fields can be verified visually.
    private static ImageContentInfo CreateSimulatedContentInfo(string filePath, int index)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var seed = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(fileName));
        var diameter = 8d + (seed % 240) / 10d;
        var area = 20d + (seed % 5000) / 10d;
        var spec = $"S{(index % 3) + 1}-{(seed % 90) + 10}";
        return new ImageContentInfo(diameter, area, spec);
    }

    private async Task ImportIntoGalleryAsync(
        string taskName,
        IEnumerable<GalleryImageInput> inputs,
        LoadMode mode,
        bool saveSessionWhenCompleted)
    {
        var inputArray = inputs as GalleryImageInput[] ?? inputs.ToArray();

        if (mode == LoadMode.Replace)
        {
            _allItems.Clear();
            _allItemPaths.Clear();
            _galleryControl.LoadItems(Array.Empty<ImageItem>(), LoadMode.Replace);
            UpdateCountLabel();
        }

        var batchSize = ComputeUiBatchSize(inputArray.Length);
        var completed = 0;

        for (var offset = 0; offset < inputArray.Length; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, inputArray.Length - offset);
            var batch = new ArraySegment<GalleryImageInput>(inputArray, offset, currentBatchSize);
            var importedItems = await _imageFileService.CreateItemsAsync(batch, progress: null);
            var visibleItems = new List<ImageItem>(importedItems.Count);

            foreach (var item in importedItems)
            {
                if (!_allItemPaths.Add(item.FilePath))
                {
                    continue;
                }

                _allItems.Add(item);
                if (MatchesCurrentFilter(item.FilePath))
                {
                    visibleItems.Add(item);
                }
            }

            if (visibleItems.Count > 0)
            {
                _galleryControl.LoadItems(visibleItems, LoadMode.Append);
            }

            completed += currentBatchSize;
            var currentFileName = importedItems.Count > 0
                ? importedItems[^1].FileName
                : Path.GetFileName(batch.Array![offset + currentBatchSize - 1].FilePath);
            UpdateCountLabel();
            UpdateTask(taskName, completed, inputArray.Length, currentFileName);
            await Task.Yield();
        }

        if (saveSessionWhenCompleted)
        {
            SaveCurrentSession();
        }
    }

    private bool MatchesCurrentFilter(string filePath)
    {
        if (_currentFilterExtensions.Count == 0)
        {
            return true;
        }

        var extension = Path.GetExtension(filePath);
        return _currentFilterExtensions.Contains(ImageFilterPolicy.NormalizeExtension(extension));
    }

    private static int ComputeUiBatchSize(int total)
    {
        const int targetRefreshes = 48;
        if (total <= 0)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(total / (double)targetRefreshes));
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

        SetInfoChoices(fields == ThumbnailInfoFields.None ? ThumbnailInfoFields.None : fields);
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
            ? "信息：仅图片"
            : $"信息：{CountInfoFields(fields)} 项";
    }

    private static int CountInfoFields(ThumbnailInfoFields fields)
    {
        var count = 0;
        if (fields.HasFlag(ThumbnailInfoFields.FileName)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.FileSize)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.ImageType)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.Dimensions)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.Diameter)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.Area)) count++;
        if (fields.HasFlag(ThumbnailInfoFields.SizeSpec)) count++;
        return count;
    }

    private async void AddButtonOnClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = FileFormatPolicy.FileDialogFilter,
            Multiselect = true,
            Title = "选择图片"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
        {
            return;
        }

        try
        {
            BeginTask("正在添加图片", dialog.FileNames.Length);
            await Task.Yield();

            await ImportIntoGalleryAsync(
                "正在添加图片",
                CreateGalleryInputs(dialog.FileNames),
                LoadMode.Append,
                saveSessionWhenCompleted: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "添加图片失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndTask();
        }
    }

    private void DeleteButtonOnClick(object? sender, EventArgs e)
    {
        _galleryControl.HandleDeleteSelected();
    }

    private void ClearAllButtonOnClick(object? sender, EventArgs e)
    {
        _galleryControl.HandleClearAll();
    }

    private void GalleryControlOnImageDeleted(object? sender, ImageDeletedEventArgs e)
    {
        if (e.Action == ImageDeleteAction.ClearAll)
        {
            _allItems.Clear();
            _allItemPaths.Clear();
        }
        else
        {
            var deletedPaths = e.FilePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allItems.RemoveAll(item => deletedPaths.Contains(item.FilePath));
            _allItemPaths.ExceptWith(deletedPaths);
        }

        UpdateCountLabel();
        SaveCurrentSession();
        ClosePreview();
    }

    private void GalleryControlOnImageSelected(object? sender, ImageSelectedEventArgs e)
    {
        try
        {
            var item = _galleryControl.Items.FirstOrDefault(i => i.FilePath == e.FilePath);
            if (item != null)
            {
                if (e.Mode == ImageSelectionMode.PinnedOpen)
                {
                    _pinnedPreviewForm = EnsurePreviewForm(_pinnedPreviewForm);
                    _pinnedPreviewForm.ShowImage(_galleryControl.Items, item, Cursor.Position, pinned: true);
                }
                else
                {
                    _previewForm = EnsurePreviewForm(_previewForm);
                    _previewForm.ShowImage(_galleryControl.Items, item, Cursor.Position);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "大图查看失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void GalleryControlOnPreviewCloseRequested(object? sender, EventArgs e)
    {
        ClosePreview();
    }

    private static PreviewForm EnsurePreviewForm(PreviewForm? previewForm)
    {
        if (previewForm is null || previewForm.IsDisposed)
        {
            return new PreviewForm();
        }

        return previewForm;
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
            _galleryControl.SelectAll();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ClosePreview()
    {
        _previewForm?.HidePreview();
    }

    private void UpdateCountLabel()
    {
        _countLabel.Text = $"共 {_allItems.Count:N0} 张";
    }

    private void SaveCurrentSession()
    {
        _sessionStore.Save(
            _sessionFilePath,
            _allItems
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

    private static int ScaleToTrackBar(float scale)
    {
        var logMin = Math.Log(MinScale);
        var logMax = Math.Log(MaxScale);
        var logScale = Math.Log(Math.Clamp(scale, MinScale, MaxScale));
        var t = (logScale - logMin) / (logMax - logMin);
        return (int)Math.Round(t * TrackBarMaximum);
    }

    private static float TrackBarToScale(int trackBarValue)
    {
        var logMin = Math.Log(MinScale);
        var logMax = Math.Log(MaxScale);
        var t = (double)trackBarValue / TrackBarMaximum;
        return (float)Math.Exp(logMin + t * (logMax - logMin));
    }

    private void UpdateScaleLabel(float scale)
    {
        _scaleValueLabel.Text = FormatScaleText(scale);
    }

    private static string FormatScaleText(float scale)
    {
        return $"{scale:0.##}x";
    }

    private void BeginTask(string taskName, int total)
    {
        _addButton.Enabled = false;
        _deleteButton.Enabled = false;
        _clearAllButton.Enabled = false;
        _taskProgressBar.Visible = true;
        _taskProgressBar.Minimum = 0;
        _taskProgressBar.Maximum = Math.Max(1, total);
        _taskProgressBar.Value = 0;
        _taskStatusLabel.Text = TaskProgressFormatter.Format(taskName, 0, total);
        _taskDetailLabel.Text = string.Empty;
        RefreshTaskStatus();
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
        RefreshTaskStatus();
    }

    private void EndTask()
    {
        _taskProgressBar.Value = 0;
        _taskProgressBar.Visible = false;
        _taskStatusLabel.Text = TaskProgressFormatter.FormatIdle();
        _taskDetailLabel.Text = string.Empty;
        _addButton.Enabled = true;
        _deleteButton.Enabled = true;
        _clearAllButton.Enabled = true;
        RefreshTaskStatus();
    }

    private void RefreshTaskStatus()
    {
        _taskStatusLabel.Refresh();
        _taskDetailLabel.Refresh();
        _taskProgressBar.Refresh();
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
            : $"{currentFileName[..Math.Max(1, maxLength - 1)]}…";
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
