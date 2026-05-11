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
    private PreviewHitArea _activeSide = PreviewHitArea.None;

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
        Reveal(_activeSide);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var side = GetHitArea(e.Location);
        if (side is PreviewHitArea.Left or PreviewHitArea.Right)
        {
            Reveal(side);
            return;
        }

        if (_activeSide != PreviewHitArea.None)
        {
            _activeSide = PreviewHitArea.None;
            StartFade();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _activeSide = PreviewHitArea.None;
        StartFade();
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

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawChrome(e.Graphics);
    }

    private void Reveal(PreviewHitArea side)
    {
        _activeSide = side;
        _opacity = 1f;
        _idleElapsedMs = 0;
        _fadeTimer.Start();
        Invalidate();
    }

    private void StartFade()
    {
        _idleElapsedMs = FadeDelayMs;
        _fadeTimer.Start();
        Invalidate();
    }

    private void FadeTimerOnTick(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            _fadeTimer.Stop();
            return;
        }

        if (_items.Count == 0 || _currentIndex < 0)
        {
            _opacity = 0f;
            _fadeTimer.Stop();
            Invalidate();
            return;
        }

        if (IsPointerOnActiveSide())
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

    private bool IsPointerOnActiveSide()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return false;
        }

        return GetHitArea(PointToClient(Cursor.Position)) == _activeSide && _activeSide != PreviewHitArea.None;
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
        return new Rectangle(0, 0, Math.Max(64, Width / 6), Height);
    }

    private Rectangle GetRightZone()
    {
        var width = Math.Max(64, Width / 6);
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

        if (_items.Count > 1 && _activeSide != PreviewHitArea.None)
        {
            DrawCircularButton(graphics, alpha, _activeSide);
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

    private void DrawCircularButton(Graphics graphics, int alpha, PreviewHitArea side)
    {
        var hovered = GetHitArea(PointToClient(Cursor.Position)) == side;
        var diameter = hovered ? 38 : 34;
        var y = (Height - diameter) / 2;
        var x = side == PreviewHitArea.Left ? 10 : Width - diameter - 10;
        var rect = new Rectangle(x, y, diameter, diameter);

        using var fillBrush = new LinearGradientBrush(
            rect,
            Color.FromArgb(Clamp(hovered ? alpha : alpha * 190 / 255), 27, 34, 46),
            Color.FromArgb(Clamp(alpha * 130 / 255), 12, 18, 26),
            LinearGradientMode.Vertical);
        graphics.FillEllipse(fillBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(Clamp(hovered ? alpha : alpha * 170 / 255), 255, 255, 255), hovered ? 1.6f : 1.2f);
        graphics.DrawEllipse(borderPen, rect);

        var arrow = side == PreviewHitArea.Left ? "\u2190" : "\u2192";
        var arrowFontSize = hovered ? 16f : 14f;
        using var arrowFont = new Font(Font.FontFamily, arrowFontSize, FontStyle.Bold);

        var arrowRect = Rectangle.Inflate(rect, -3, -3);
        TextRenderer.DrawText(
            graphics,
            arrow,
            arrowFont,
            arrowRect,
            Color.FromArgb(alpha, 248, 250, 252),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
