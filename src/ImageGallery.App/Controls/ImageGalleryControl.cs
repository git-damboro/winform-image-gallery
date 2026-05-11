using System.Drawing;
using System.Windows.Forms;
using ImageGallery.App.Rendering;
using ImageGallery.App.Services;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Controls;

public sealed class ImageGalleryControl : UserControl
{
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
    private int _thumbnailSize = 128;
    private GalleryDisplayStyle _displayStyle = GalleryDisplayStyle.Crystal;
    private ThumbnailInfoFields _thumbnailInfoFields = ThumbnailInfoFields.All;
    private HashSet<string> _visibleExtensions = new(StringComparer.OrdinalIgnoreCase);

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

    public event EventHandler<ImageItem>? PreviewRequested;

    public event EventHandler<ImageItem>? ImageOpenRequested;

    public event EventHandler? PreviewCloseRequested;

    public int ThumbnailSize
    {
        get => _thumbnailSize;
        set
        {
            var normalized = Math.Clamp(value, 64, 256);
            if (_thumbnailSize == normalized)
            {
                return;
            }

            _thumbnailSize = normalized;
            _thumbnailService.Clear();
            RecalculateLayout();
        }
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

    public IReadOnlyCollection<string> VisibleExtensions
    {
        get => _visibleExtensions;
        set
        {
            var normalized = new HashSet<string>(ImageFilterPolicy.NormalizeExtensions(value), StringComparer.OrdinalIgnoreCase);
            if (_visibleExtensions.SetEquals(normalized))
            {
                return;
            }

            _visibleExtensions = normalized;
            RebuildVisibleItems(clearSelection: true);
        }
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

    public void SetItems(IEnumerable<ImageItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _hoverIndex = -1;
        RebuildVisibleItems(clearSelection: true);
    }

    public IReadOnlyList<int> GetSelectedIndexesDescending()
    {
        return _selectionManager
            .GetSelectedIndexesDescending()
            .Select(visibleIndex => _visibleItemIndexes[visibleIndex])
            .ToArray();
    }

    public void RemoveSelectedIndexes(IEnumerable<int> indexes)
    {
        _selectionManager.Clear();
        InvalidateCanvas();
    }

    public void SelectAllVisible()
    {
        _selectionManager.SelectAll(_visibleItemIndexes.Count);
        InvalidateCanvas();
    }

    private GalleryLayoutOptions CreateLayoutOptions()
    {
        var style = ThumbnailStyleCatalog.Get(DisplayStyle);
        return new GalleryLayoutOptions(
            _thumbnailSize,
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

    private void RebuildVisibleItems(bool clearSelection)
    {
        _visibleItemIndexes.Clear();
        for (var index = 0; index < _items.Count; index++)
        {
            if (IsVisibleItem(_items[index]))
            {
                _visibleItemIndexes.Add(index);
            }
        }

        if (clearSelection)
        {
            _selectionManager.Clear();
            _hoverIndex = -1;
        }

        RecalculateLayout();
    }

    private bool IsVisibleItem(ImageItem item)
    {
        return _visibleExtensions.Count == 0 || _visibleExtensions.Contains(ImageFilterPolicy.NormalizeExtension(item.Extension));
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
            _items.Count,
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
                "\u8bf7\u70b9\u51fb\u201c\u6dfb\u52a0\u56fe\u7247\u201d\u5bfc\u5165\u56fe\u50cf",
                Font,
                _canvas.ClientRectangle,
                Color.FromArgb(120, 126, 138),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

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
            var thumbnail = _thumbnailService.GetOrQueue(item, _thumbnailSize, InvalidateCanvas);
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
                _thumbnailSize);
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
        else
        {
            PreviewCloseRequested?.Invoke(this, EventArgs.Empty);
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
        if (index >= 0)
        {
            ImageOpenRequested?.Invoke(this, _items[_visibleItemIndexes[index]]);
        }
    }

    private void HoverTimerOnTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();

        if (_hoverIndex >= 0 && _hoverIndex < _visibleItemIndexes.Count)
        {
            PreviewRequested?.Invoke(this, _items[_visibleItemIndexes[_hoverIndex]]);
        }
    }

    private int HitTest(Point point)
    {
        return _layout.IndexFromPoint(point.X + _scrollX, point.Y + _scrollY);
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
                _owner.SelectAllVisible();
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
