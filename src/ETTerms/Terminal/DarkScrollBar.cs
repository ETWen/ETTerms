using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ETTerms.Terminal;

/// <summary>
/// 自繪深色垂直捲軸：細長、無箭頭按鈕、圓角滑塊，配合終端機深色主題。
/// API（Minimum / Maximum / Value / LargeChange / SmallChange / Scroll）對齊 <see cref="VScrollBar"/>，
/// 方便直接替換。<see cref="Scroll"/> 只在「使用者操作」造成值變動時觸發（程式設定 Value 不觸發）。
/// </summary>
public sealed class DarkScrollBar : Control
{
    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 0;
    public int LargeChange { get; set; } = 1;
    public int SmallChange { get; set; } = 1;

    private int _value;
    public int Value
    {
        get => _value;
        set { int v = Clamp(value); if (v != _value) { _value = v; Invalidate(); } }
    }

    /// <summary>使用者拖曳 / 點軌道 / 滾輪造成值變動時觸發。</summary>
    public event EventHandler? Scroll;

    // 配色（KKTerm 深色）
    private static readonly Color TrackColor = Color.FromArgb(24, 24, 28);
    private static readonly Color ThumbColor = Color.FromArgb(70, 70, 82);
    private static readonly Color ThumbHover = Color.FromArgb(100, 100, 118);
    private static readonly Color ThumbDrag = Color.FromArgb(130, 130, 152);

    private bool _hover, _dragging;
    private int _dragOffset;

    public DarkScrollBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Width = 12;
        BackColor = TrackColor;
    }

    // ── 數值範圍工具 ─────────────────────────────────────────
    /// <summary>可達的最大 Value（與 VScrollBar 相同：Maximum - LargeChange + 1）。</summary>
    private int MaxValue => Math.Max(Minimum, Maximum - LargeChange + 1);
    private int Clamp(int v) => Math.Clamp(v, Minimum, MaxValue);
    private bool Scrollable => Enabled && MaxValue > Minimum;

    // ── 滑塊幾何 ─────────────────────────────────────────────
    private const int MinThumb = 24;
    private const int Pad = 2;

    private int TrackHeight => Math.Max(1, Height - Pad * 2);

    private int ThumbHeight()
    {
        int total = Maximum - Minimum + 1;
        if (total <= 0) return TrackHeight;
        int h = (int)((long)TrackHeight * LargeChange / total);
        return Math.Clamp(h, MinThumb, TrackHeight);
    }

    private int ThumbTop()
    {
        int range = MaxValue - Minimum;
        if (range <= 0) return Pad;
        int travel = TrackHeight - ThumbHeight();
        return Pad + (int)((long)travel * (_value - Minimum) / range);
    }

    private Rectangle ThumbRect() => new(Pad, ThumbTop(), Math.Max(1, Width - Pad * 2), ThumbHeight());

    // ── 繪製 ─────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(TrackColor);
        if (!Scrollable) return;          // 不可捲動時只畫底色（與停用態一致）

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = ThumbRect();
        var color = _dragging ? ThumbDrag : _hover ? ThumbHover : ThumbColor;
        int radius = Math.Min(r.Width, 6);
        using var path = RoundedRect(r, radius);
        using var b = new SolidBrush(color);
        g.FillPath(b, path);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        if (d <= 0 || d > r.Width || d > r.Height) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ── 滑鼠互動 ─────────────────────────────────────────────
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Scrollable) return;
        var thumb = ThumbRect();
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = e.Y - thumb.Top;
        }
        else
        {
            // 點軌道：往點擊方向翻一頁
            SetValueFromUser(e.Y < thumb.Top ? _value - LargeChange : _value + LargeChange);
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool over = ThumbRect().Contains(e.Location);
        if (over != _hover) { _hover = over; Invalidate(); }

        if (!_dragging) return;
        int travel = TrackHeight - ThumbHeight();
        if (travel <= 0) return;
        int range = MaxValue - Minimum;
        int y = Math.Clamp(e.Y - _dragOffset - Pad, 0, travel);
        SetValueFromUser(Minimum + (int)((long)y * range / travel));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging) { _dragging = false; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hover) { _hover = false; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Scrollable) return;
        int step = e.Delta / 120 * SmallChange * 3;
        SetValueFromUser(_value - step);
    }

    private void SetValueFromUser(int v)
    {
        int nv = Clamp(v);
        if (nv == _value) return;
        _value = nv;
        Invalidate();
        Scroll?.Invoke(this, EventArgs.Empty);
    }
}
