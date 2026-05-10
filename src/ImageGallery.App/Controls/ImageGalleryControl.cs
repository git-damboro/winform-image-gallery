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
    private int _hoverIndex = -1;
    private int _thumbnailSize = 128;
    private GalleryDisplayStyle _displayStyle = GalleryDisplayStyle.Crystal;

    private static GalleryLayoutOptions DefaultLayoutOptions => new(128, 56, 10, 14);

    public ImageGalleryControl()
    {
        _canvas = new GalleryCanvas(this);

        BackColor = Color.White;
        DoubleBuffered = true;

        Controls.Add(_canvas);
        Controls.Add(_verticalScrollBar);
        Controls.Add(_horizontalScrollBar);

        _verticalScrollBar.Scroll += (_, _) => InvalidateCanvas();
        _horizontalScrollBar.Scroll += (_, _) => InvalidateCanvas();

        _hoverTimer.Interval = 450;
        _hoverTimer.Tick += HoverTimerOnTick;

        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public event EventHandler<ImageItem>? PreviewRequested;

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
            InvalidateCanvas();
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
        _selectionManager.Clear();
        _hoverIndex = -1;
        RecalculateLayout();
    }

    public IReadOnlyList<int> GetSelectedIndexesDescending()
    {
        return _selectionManager.GetSelectedIndexesDescending();
    }

    public void RemoveSelectedIndexes(IEnumerable<int> indexes)
    {
        _selectionManager.RemoveIndexes(indexes);
        InvalidateCanvas();
    }

    private GalleryLayoutOptions CreateLayoutOptions()
    {
        var textAreaHeight = DisplayStyle == GalleryDisplayStyle.Compact ? 44 : 58;
        var padding = DisplayStyle == GalleryDisplayStyle.Compact ? 6 : 10;
        var gap = DisplayStyle == GalleryDisplayStyle.Compact ? 8 : 14;

        return new GalleryLayoutOptions(_thumbnailSize, textAreaHeight, padding, gap);
    }

    private void RecalculateLayout()
    {
        var vWidth = SystemInformation.VerticalScrollBarWidth;
        var hHeight = SystemInformation.HorizontalScrollBarHeight;

        var viewportWidth = Math.Max(1, Width - vWidth);
        var viewportHeight = Math.Max(1, Height - hHeight);

        _layout = GalleryLayoutCalculator.Calculate(
            _items.Count,
            viewportWidth,
            viewportHeight,
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value,
            CreateLayoutOptions());

        var needsVertical = _layout.TotalHeight > viewportHeight;
        var needsHorizontal = _layout.TotalWidth > viewportWidth;

        viewportWidth = Math.Max(1, Width - (needsVertical ? vWidth : 0));
        viewportHeight = Math.Max(1, Height - (needsHorizontal ? hHeight : 0));

        _layout = GalleryLayoutCalculator.Calculate(
            _items.Count,
            viewportWidth,
            viewportHeight,
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value,
            CreateLayoutOptions());

        ConfigureScrollBar(_verticalScrollBar, _layout.TotalHeight, viewportHeight);
        ConfigureScrollBar(_horizontalScrollBar, _layout.TotalWidth, viewportWidth);

        _layout = GalleryLayoutCalculator.Calculate(
            _items.Count,
            viewportWidth,
            viewportHeight,
            _horizontalScrollBar.Value,
            _verticalScrollBar.Value,
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

        var maxValue = Math.Max(0, scrollBar.Maximum - scrollBar.LargeChange + 1);
        if (scrollBar.Value > maxValue)
        {
            scrollBar.Value = maxValue;
        }
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

        if (_items.Count == 0 || _layout.VisibleRange.IsEmpty)
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

        for (var index = _layout.VisibleRange.FirstIndex; index <= _layout.VisibleRange.LastIndex; index++)
        {
            var itemRect = _layout.GetItemRect(index);
            var screenRect = new Rectangle(
                itemRect.X - _horizontalScrollBar.Value,
                itemRect.Y - _verticalScrollBar.Value,
                itemRect.Width,
                itemRect.Height);

            if (!screenRect.IntersectsWith(_canvas.ClientRectangle))
            {
                continue;
            }

            var item = _items[index];
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
            _items.Count,
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
            PreviewRequested?.Invoke(this, _items[index]);
        }
    }

    private void HoverTimerOnTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();

        if (_hoverIndex >= 0 && _hoverIndex < _items.Count)
        {
            PreviewRequested?.Invoke(this, _items[_hoverIndex]);
        }
    }

    private int HitTest(Point point)
    {
        return _layout.IndexFromPoint(point.X + _horizontalScrollBar.Value, point.Y + _verticalScrollBar.Value);
    }

    private sealed class GalleryCanvas : Control
    {
        private readonly ImageGalleryControl _owner;

        public GalleryCanvas(ImageGalleryControl owner)
        {
            _owner = owner;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
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
            _owner.CanvasOnMouseDown(e);
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
    }
}
