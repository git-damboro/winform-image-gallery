using System.Drawing;
using System.Windows.Forms;
using ImageGallery.App.Rendering;
using ImageGallery.App.Services;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Controls;

public sealed class ImageGalleryControl : UserControl
{
    private const float BasePixelSize = 128f;
    private const float MinScale = 0.1f;
    private const float MaxScale = 50f;
    private const int MinPixelSize = 16;
    private const int MaxPixelSize = 4096;

    private readonly GalleryCanvas _canvas;
    private readonly VScrollBar _verticalScrollBar = new();
    private readonly HScrollBar _horizontalScrollBar = new();
    private readonly List<ImageItem> _items = new();
    private readonly SelectionManager _selectionManager = new();
    private readonly ThumbnailService _thumbnailService = new();
    private readonly GalleryRenderer _renderer = new();
    private readonly System.Windows.Forms.Timer _hoverTimer = new();

    private GalleryLayout _layout = GalleryLayoutCalculator.Calculate(0, 1, 1, 0, 0, DefaultLayoutOptions);
    private readonly List<int> _visibleItemIndexes = new();
    private int _hoverIndex = -1;
    private int _scrollX;
    private int _scrollY;
    private float _thumbnailScale = 1.0f;
    private GalleryDisplayStyle _displayStyle = GalleryDisplayStyle.Crystal;
    private ThumbnailInfoFields _thumbnailInfoFields = ThumbnailInfoFields.All;

    private static GalleryLayoutOptions DefaultLayoutOptions => new(128, 56, 10, 14);

    public ImageGalleryControl()
    {
        _canvas = new GalleryCanvas(this);

        BackColor = Color.White;
        DoubleBuffered = true;

        Controls.Add(_canvas);
        Controls.Add(_verticalScrollBar);
        Controls.Add(_horizontalScrollBar);

        _verticalScrollBar.Scroll += (_, e) => ScrollTo(_scrollX, e.NewValue);
        _horizontalScrollBar.Scroll += (_, e) => ScrollTo(e.NewValue, _scrollY);
        _verticalScrollBar.ValueChanged += (_, _) => ScrollTo(_scrollX, _verticalScrollBar.Value);
        _horizontalScrollBar.ValueChanged += (_, _) => ScrollTo(_horizontalScrollBar.Value, _scrollY);

        _hoverTimer.Interval = 450;
        _hoverTimer.Tick += HoverTimerOnTick;

        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public event EventHandler<ImageDeletedEventArgs>? ImageDeleted;

    public event EventHandler<ImageSelectedEventArgs>? ImageSelected;

    public event EventHandler? PreviewCloseRequested;

    public float ThumbnailScale
    {
        get => _thumbnailScale;
        set
        {
            var normalized = Math.Clamp(value, MinScale, MaxScale);
            if (Math.Abs(_thumbnailScale - normalized) < 1e-6f)
            {
                return;
            }

            _thumbnailScale = normalized;
            _thumbnailService.Clear();
            RecalculateLayout();
        }
    }

    public int ThumbnailPixelSize => Math.Clamp((int)Math.Round(BasePixelSize * _thumbnailScale), MinPixelSize, MaxPixelSize);

    [Obsolete("Use ThumbnailScale instead.")]
    public int ThumbnailSize
    {
        get => ThumbnailPixelSize;
        set => ThumbnailScale = value / BasePixelSize;
    }

    public GalleryDisplayStyle DisplayStyle
    {
        get => _displayStyle;
        set
        {
            if (_displayStyle == value)
            {
                return;
            }

            _displayStyle = value;
            RecalculateLayout();
        }
    }

    public ThumbnailInfoFields ThumbnailInfoFields
    {
        get => _thumbnailInfoFields;
        set
        {
            if (_thumbnailInfoFields == value)
            {
                return;
            }

            _thumbnailInfoFields = value;
            RecalculateLayout();
        }
    }

    public IReadOnlyList<ImageItem> Items => _items;

    public void LoadImages(IEnumerable<GalleryImageInput> inputs, LoadMode mode)
    {
        var items = inputs.Select(CreateItemFromInput);
        LoadItems(items, mode);
    }

    public void LoadItems(IEnumerable<ImageItem> items, LoadMode mode)
    {
        if (mode == LoadMode.Replace)
        {
            _thumbnailService.CancelPrefetch();
            _items.Clear();
            _selectionManager.Clear();
            _hoverIndex = -1;
        }

        var existingPaths = mode == LoadMode.Append
            ? new HashSet<string>(_items.Select(i => i.FilePath), StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var item in items)
        {
            if (existingPaths != null && existingPaths.Contains(item.FilePath))
            {
                continue;
            }

            _items.Add(item);
            existingPaths?.Add(item.FilePath);
        }

        RebuildVisibleItems(clearSelection: mode == LoadMode.Replace);
        SchedulePrefetchForBacklog();
    }

    public void RemoveImage(string filePath)
    {
        var index = _items.FindIndex(i => string.Equals(i.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        _items.RemoveAt(index);
        _selectionManager.Clear();
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);
    }

    public void RemoveImages(IEnumerable<string> filePaths)
    {
        var toRemove = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
        if (toRemove.Count == 0)
        {
            return;
        }

        _items.RemoveAll(i => toRemove.Contains(i.FilePath));
        _selectionManager.Clear();
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);
    }

    public void ClearImages()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _thumbnailService.CancelPrefetch();
        _items.Clear();
        _selectionManager.Clear();
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);
    }

    public void SelectAll()
    {
        _selectionManager.SelectAll(_visibleItemIndexes.Count);
        InvalidateCanvas();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverTimer.Dispose();
            _thumbnailService.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        RecalculateLayout();
    }

    private static ImageItem CreateItemFromInput(GalleryImageInput input)
    {
        var fileName = Path.GetFileName(input.FilePath);
        var extension = Path.GetExtension(input.FilePath);

        try
        {
            var fileInfo = new FileInfo(input.FilePath);
            if (!fileInfo.Exists)
            {
                return new ImageItem(input.FilePath, fileName, 0, extension,
                    errorMessage: "文件不存在", contentInfo: input.ContentInfo);
            }

            if (!FileFormatPolicy.IsRecognized(extension))
            {
                return new ImageItem(input.FilePath, fileName, fileInfo.Length, extension,
                    errorMessage: FileFormatPolicy.GetSupportMessage(extension), contentInfo: input.ContentInfo);
            }

            try
            {
                using var stream = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                return new ImageItem(input.FilePath, fileName, fileInfo.Length, extension,
                    image.Width, image.Height, contentInfo: input.ContentInfo);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
            {
                var message = FileFormatPolicy.IsNativelyDecodable(extension)
                    ? ex.Message
                    : $"{FileFormatPolicy.GetSupportMessage(extension)}: {ex.Message}";
                return new ImageItem(input.FilePath, fileName, fileInfo.Length, extension,
                    errorMessage: message, contentInfo: input.ContentInfo);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ImageItem(input.FilePath, fileName, 0, extension,
                errorMessage: ex.Message, contentInfo: input.ContentInfo);
        }
    }

    private GalleryLayoutOptions CreateLayoutOptions()
    {
        var style = ThumbnailStyleCatalog.Get(DisplayStyle);
        var pixelSize = ThumbnailPixelSize;
        return new GalleryLayoutOptions(
            pixelSize,
            CalculateTextAreaHeight(style),
            style.Padding,
            style.Gap);
    }

    private int CalculateTextAreaHeight(ThumbnailStyleDefinition style)
    {
        if (_thumbnailInfoFields == ThumbnailInfoFields.None)
        {
            return 0;
        }

        var lineCount = CountSelectedInfoFields(_thumbnailInfoFields);
        return style.TextTopSpacing + lineCount * style.TextLineHeight;
    }

    private static int CountSelectedInfoFields(ThumbnailInfoFields fields)
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

    private void RebuildVisibleItems(bool clearSelection)
    {
        _visibleItemIndexes.Clear();
        for (var index = 0; index < _items.Count; index++)
        {
            _visibleItemIndexes.Add(index);
        }

        if (clearSelection)
        {
            _selectionManager.Clear();
            _hoverIndex = -1;
        }

        RecalculateLayout();
    }

    private void RecalculateLayout()
    {
        var vWidth = SystemInformation.VerticalScrollBarWidth;
        var hHeight = SystemInformation.HorizontalScrollBarHeight;

        var viewportWidth = Math.Max(1, Width - vWidth);
        var viewportHeight = Math.Max(1, Height - hHeight);

        _layout = GalleryLayoutCalculator.Calculate(
            _visibleItemIndexes.Count,
            viewportWidth,
            viewportHeight,
            _scrollX,
            _scrollY,
            CreateLayoutOptions());

        var needsVertical = _layout.TotalHeight > viewportHeight;
        var needsHorizontal = _layout.TotalWidth > viewportWidth;

        viewportWidth = Math.Max(1, Width - (needsVertical ? vWidth : 0));
        viewportHeight = Math.Max(1, Height - (needsHorizontal ? hHeight : 0));

        _layout = GalleryLayoutCalculator.Calculate(
            _visibleItemIndexes.Count,
            viewportWidth,
            viewportHeight,
            _scrollX,
            _scrollY,
            CreateLayoutOptions());

        ConfigureScrollBar(_verticalScrollBar, _layout.TotalHeight, viewportHeight);
        ConfigureScrollBar(_horizontalScrollBar, _layout.TotalWidth, viewportWidth);
        _scrollY = SetScrollBarValue(_verticalScrollBar, _scrollY);
        _scrollX = SetScrollBarValue(_horizontalScrollBar, _scrollX);

        _layout = GalleryLayoutCalculator.Calculate(
            _visibleItemIndexes.Count,
            viewportWidth,
            viewportHeight,
            _scrollX,
            _scrollY,
            CreateLayoutOptions());

        _canvas.Bounds = new Rectangle(0, 0, viewportWidth, viewportHeight);
        _verticalScrollBar.Bounds = new Rectangle(viewportWidth, 0, vWidth, viewportHeight);
        _horizontalScrollBar.Bounds = new Rectangle(0, viewportHeight, viewportWidth, hHeight);

        InvalidateCanvas();
    }

    private static void ConfigureScrollBar(ScrollBar scrollBar, int contentSize, int viewportSize)
    {
        scrollBar.Visible = contentSize > viewportSize;
        if (!scrollBar.Visible)
        {
            scrollBar.Value = 0;
            return;
        }

        scrollBar.Minimum = 0;
        scrollBar.LargeChange = Math.Max(1, viewportSize);
        scrollBar.SmallChange = 48;
        scrollBar.Maximum = Math.Max(0, contentSize - 1);
    }

    private static int SetScrollBarValue(ScrollBar scrollBar, int value)
    {
        if (!scrollBar.Visible)
        {
            scrollBar.Value = 0;
            return 0;
        }

        var clampedValue = ClampScrollValue(scrollBar, value);
        if (scrollBar.Value != clampedValue)
        {
            scrollBar.Value = clampedValue;
        }

        return clampedValue;
    }

    private static int ClampScrollValue(ScrollBar scrollBar, int value)
    {
        var maxValue = Math.Max(0, scrollBar.Maximum - scrollBar.LargeChange + 1);
        return Math.Clamp(value, scrollBar.Minimum, maxValue);
    }

    private void ScrollTo(int scrollX, int scrollY)
    {
        _scrollX = SetScrollBarValue(_horizontalScrollBar, scrollX);
        _scrollY = SetScrollBarValue(_verticalScrollBar, scrollY);
        RecalculateVisibleLayout();
    }

    private void CanvasOnMouseWheel(MouseEventArgs e)
    {
        if (!_verticalScrollBar.Visible || e.Delta == 0)
        {
            return;
        }

        var scrollLines = SystemInformation.MouseWheelScrollLines;
        var lineCount = scrollLines <= 0 ? 3 : scrollLines;
        var wheelSteps = Math.Max(1, Math.Abs(e.Delta) / 120);
        var delta = Math.Sign(e.Delta) * lineCount * _verticalScrollBar.SmallChange * wheelSteps;
        ScrollTo(_scrollX, _scrollY - delta);
    }

    private void RecalculateVisibleLayout()
    {
        _layout = GalleryLayoutCalculator.Calculate(
            _visibleItemIndexes.Count,
            Math.Max(1, _canvas.Width),
            Math.Max(1, _canvas.Height),
            _scrollX,
            _scrollY,
            CreateLayoutOptions());

        InvalidateCanvas();
    }

    private void InvalidateCanvas()
    {
        if (_canvas.IsHandleCreated && _canvas.InvokeRequired)
        {
            _canvas.BeginInvoke(new Action(InvalidateCanvas));
            return;
        }

        _canvas.Invalidate();
    }

    private void CanvasOnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_visibleItemIndexes.Count == 0 || _layout.VisibleRange.IsEmpty)
        {
            TextRenderer.DrawText(
                e.Graphics,
                "请点击“添加图片”导入图像",
                Font,
                _canvas.ClientRectangle,
                Color.FromArgb(120, 126, 138),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var pixelSize = ThumbnailPixelSize;

        for (var index = _layout.VisibleRange.FirstIndex; index <= _layout.VisibleRange.LastIndex; index++)
        {
            var itemRect = _layout.GetItemRect(index);
            var screenRect = new Rectangle(
                itemRect.X - _scrollX,
                itemRect.Y - _scrollY,
                itemRect.Width,
                itemRect.Height);

            if (!screenRect.IntersectsWith(_canvas.ClientRectangle))
            {
                continue;
            }

            var item = _items[_visibleItemIndexes[index]];
            var thumbnail = _thumbnailService.GetOrQueue(item, pixelSize, InvalidateCanvas);
            _renderer.DrawCard(
                e.Graphics,
                Font,
                item,
                thumbnail,
                screenRect,
                _selectionManager.IsSelected(index),
                index == _hoverIndex,
                DisplayStyle,
                _thumbnailInfoFields,
                pixelSize);
        }
    }

    private void CanvasOnMouseDown(MouseEventArgs e)
    {
        Focus();
        var index = HitTest(e.Location);
        if (index < 0)
        {
            _selectionManager.Clear();
            InvalidateCanvas();
            return;
        }

        var modifiers = ModifierKeys;
        _selectionManager.Select(
            index,
            _visibleItemIndexes.Count,
            ctrl: modifiers.HasFlag(Keys.Control),
            shift: modifiers.HasFlag(Keys.Shift));

        InvalidateCanvas();
    }

    private void CanvasOnMouseMove(MouseEventArgs e)
    {
        var index = HitTest(e.Location);
        if (_hoverIndex == index)
        {
            return;
        }

        _hoverIndex = index;
        _hoverTimer.Stop();

        if (_hoverIndex >= 0)
        {
            _hoverTimer.Start();
        }

        InvalidateCanvas();
    }

    private void CanvasOnMouseLeave()
    {
        _hoverTimer.Stop();
        _hoverIndex = -1;
        PreviewCloseRequested?.Invoke(this, EventArgs.Empty);
        InvalidateCanvas();
    }

    private void CanvasOnDoubleClick(MouseEventArgs e)
    {
        var index = HitTest(e.Location);
        if (index >= 0 && index < _visibleItemIndexes.Count)
        {
            var item = _items[_visibleItemIndexes[index]];
            ImageSelected?.Invoke(this, new ImageSelectedEventArgs(item.FilePath, ImageSelectionMode.PinnedOpen));
        }
    }

    private void HoverTimerOnTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();

        if (_hoverIndex >= 0 && _hoverIndex < _visibleItemIndexes.Count)
        {
            var item = _items[_visibleItemIndexes[_hoverIndex]];
            ImageSelected?.Invoke(this, new ImageSelectedEventArgs(item.FilePath, ImageSelectionMode.HoverPreview));
        }
    }

    internal void HandleDeleteSelected()
    {
        var selectedVisibleIndexes = _selectionManager.GetSelectedIndexesDescending();
        if (selectedVisibleIndexes.Count == 0)
        {
            return;
        }

        var filePaths = selectedVisibleIndexes
            .Where(vi => vi >= 0 && vi < _visibleItemIndexes.Count)
            .Select(vi => _items[_visibleItemIndexes[vi]].FilePath)
            .ToList();

        var action = filePaths.Count == 1
            ? ImageDeleteAction.Single
            : ImageDeleteAction.Multiple;

        foreach (var vi in selectedVisibleIndexes.OrderByDescending(x => x))
        {
            if (vi >= 0 && vi < _visibleItemIndexes.Count)
            {
                var itemIndex = _visibleItemIndexes[vi];
                _items.RemoveAt(itemIndex);
            }
        }

        _selectionManager.Clear();
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);

        ImageDeleted?.Invoke(this, new ImageDeletedEventArgs(action, filePaths));
    }

    internal void HandleClearAll()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _thumbnailService.CancelPrefetch();
        _items.Clear();
        _selectionManager.Clear();
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);

        ImageDeleted?.Invoke(this, new ImageDeletedEventArgs(ImageDeleteAction.ClearAll, Array.Empty<string>()));
    }

    private int HitTest(Point point)
    {
        return _layout.IndexFromPoint(point.X + _scrollX, point.Y + _scrollY);
    }

    private void SchedulePrefetchForBacklog()
    {
        if (_visibleItemIndexes.Count == 0)
        {
            return;
        }

        var pixelSize = ThumbnailPixelSize;
        var visibleFirst = _layout.VisibleRange.FirstIndex;
        var visibleLast = _layout.VisibleRange.LastIndex;

        var backlog = new List<(ImageItem Item, int ThumbnailSize)>();
        for (var index = 0; index < _visibleItemIndexes.Count; index++)
        {
            if (index >= visibleFirst && index <= visibleLast)
            {
                continue;
            }

            backlog.Add((_items[_visibleItemIndexes[index]], pixelSize));
        }

        if (backlog.Count > 0)
        {
            _thumbnailService.SchedulePrefetch(backlog);
        }
    }

    private sealed class GalleryCanvas : Control
    {
        private readonly ImageGalleryControl _owner;

        public GalleryCanvas(ImageGalleryControl owner)
        {
            _owner = owner;
            TabStop = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.Selectable
                | ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            _owner.CanvasOnPaint(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _owner.CanvasOnMouseDown(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _owner.CanvasOnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _owner.CanvasOnMouseLeave();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            _owner.CanvasOnDoubleClick(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.A))
            {
                _owner.SelectAll();
                return true;
            }

            if (keyData == Keys.Delete)
            {
                _owner.HandleDeleteSelected();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _owner.CanvasOnMouseWheel(e);
        }
    }
}
