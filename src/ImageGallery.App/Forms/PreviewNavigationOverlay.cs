using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ImageGallery.App.Rendering;
using ImageGallery.Core.Models;

namespace ImageGallery.App.Forms;

internal sealed class PreviewNavigationOverlay : Control
{
    private const int FadeDelayMs = 650;
    private const float FadeStep = 0.08f;

    private readonly System.Windows.Forms.Timer _fadeTimer = new();
    private IReadOnlyList<ImageItem> _items = Array.Empty<ImageItem>();
    private int _currentIndex = -1;
    private float _opacity = 1f;
    private int _idleElapsedMs;

    public PreviewNavigationOverlay()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;

        _fadeTimer.Interval = 50;
        _fadeTimer.Tick += FadeTimerOnTick;
    }

    public event EventHandler<int>? NavigateRequested;

    public void UpdateState(IReadOnlyList<ImageItem> items, int currentIndex)
    {
        _items = items;
        _currentIndex = currentIndex;
        Reveal();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Reveal();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Reveal();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _idleElapsedMs = FadeDelayMs;
        _fadeTimer.Start();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _items.Count <= 1 || _currentIndex < 0)
        {
            return;
        }

        switch (GetHitArea(e.Location))
        {
            case PreviewHitArea.Left:
                NavigateRequested?.Invoke(this, -1);
                break;
            case PreviewHitArea.Right:
                NavigateRequested?.Invoke(this, 1);
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_opacity <= 0.01f || _items.Count == 0 || _currentIndex < 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawChrome(e.Graphics);
    }

    private void Reveal()
    {
        _opacity = 1f;
        _idleElapsedMs = 0;
        _fadeTimer.Start();
        Invalidate();
    }

    private void FadeTimerOnTick(object? sender, EventArgs e)
    {
        if (_items.Count == 0 || _currentIndex < 0)
        {
            _opacity = 0f;
            _fadeTimer.Stop();
            Invalidate();
            return;
        }

        if (IsPointerOnPersistentArea())
        {
            _opacity = 1f;
            _idleElapsedMs = 0;
            Invalidate();
            return;
        }

        if (_idleElapsedMs < FadeDelayMs)
        {
            _idleElapsedMs += _fadeTimer.Interval;
            return;
        }

        _opacity = Math.Max(0f, _opacity - FadeStep);
        if (_opacity <= 0.01f)
        {
            _opacity = 0f;
            _fadeTimer.Stop();
        }

        Invalidate();
    }

    private bool IsPointerOnPersistentArea()
    {
        var client = PointToClient(Cursor.Position);
        return GetHitArea(client) is PreviewHitArea.Left or PreviewHitArea.Right;
    }

    private PreviewHitArea GetHitArea(Point point)
    {
        if (GetLeftZone().Contains(point))
        {
            return PreviewHitArea.Left;
        }

        if (GetRightZone().Contains(point))
        {
            return PreviewHitArea.Right;
        }

        return PreviewHitArea.None;
    }

    private Rectangle GetLeftZone()
    {
        return new Rectangle(0, 0, Math.Max(96, Width / 5), Height);
    }

    private Rectangle GetRightZone()
    {
        var width = Math.Max(96, Width / 5);
        return new Rectangle(Math.Max(0, Width - width), 0, width, Height);
    }

    private void DrawChrome(Graphics graphics)
    {
        var alpha = (int)Math.Round(255 * _opacity);
        if (alpha <= 0)
        {
            return;
        }

        DrawTitleBar(graphics, alpha);
        if (_items.Count > 1)
        {
            DrawEdgeArrow(graphics, alpha, true);
            DrawEdgeArrow(graphics, alpha, false);
        }
    }

    private void DrawTitleBar(Graphics graphics, int alpha)
    {
        var title = _items.Count > 0 && _currentIndex >= 0 && _currentIndex < _items.Count
            ? $"{_items[_currentIndex].FileName} ({_currentIndex + 1}/{_items.Count})"
            : string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var barRect = new Rectangle(16, 14, Math.Max(1, Width - 32), 38);
        using var barBrush = new SolidBrush(Color.FromArgb(Clamp(alpha * 200 / 255), 18, 22, 30));
        graphics.FillRoundedRectangle(barBrush, barRect, 14);

        using var borderPen = new Pen(Color.FromArgb(Clamp(alpha * 120 / 255), 255, 255, 255), 1f);
        graphics.DrawRoundedRectangle(borderPen, barRect, 14);

        var textRect = Rectangle.Inflate(barRect, -12, -8);
        using var titleFont = new Font(Font.FontFamily, 10f, FontStyle.Bold);
        TextRenderer.DrawText(
            graphics,
            title,
            titleFont,
            textRect,
            Color.FromArgb(alpha, 245, 248, 252),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DrawSideButton(Graphics graphics, int alpha, bool left)
    {
        var zone = left ? GetLeftZone() : GetRightZone();
        var hovered = zone.Contains(PointToClient(Cursor.Position));
        var arrow = left ? "\u25c0" : "\u25b6";
        var arrowFontSize = hovered ? 34f : 28f;
        var arrowColor = Color.FromArgb(Clamp(hovered ? alpha : alpha * 210 / 255), 245, 248, 252);
        using var arrowFont = new Font(Font.FontFamily, arrowFontSize, FontStyle.Bold);

        var arrowRect = left
            ? new Rectangle(10, (Height - 42) / 2, 28, 42)
            : new Rectangle(Width - 38, (Height - 42) / 2, 28, 42);

        TextRenderer.DrawText(
            graphics,
            arrow,
            arrowFont,
            arrowRect,
            arrowColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void DrawEdgeArrow(Graphics graphics, int alpha, bool left)
    {
        DrawSideButton(graphics, alpha, left);
    }

    private static int Clamp(int value)
    {
        return Math.Clamp(value, 0, 255);
    }

    private enum PreviewHitArea
    {
        None,
        Left,
        Right
    }
}
