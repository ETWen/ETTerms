using System.Drawing;
using System.Windows.Forms;

namespace ETTerms.App;

/// <summary>
/// 左側圖示列（仿 KKTerm ActivityRail）。
/// 三個檢視：Terminal / Scripts / Settings。
/// 點擊會觸發 <see cref="ViewSelected"/>。
/// </summary>
public sealed class ActivityRail : UserControl
{
    public enum RailView { Terminal, Status, Settings, About }

    public event EventHandler<RailView>? ViewSelected;

    private RailView _active = RailView.Terminal;
    private RailView? _hover;

    private const int BaseItemSize = 56;
    /// <summary>依目前螢幕 DPI 縮放的格子大小（owner-draw 不會自動縮放像素，需自己換算）。</summary>
    private int ItemSize => (int)Math.Round(BaseItemSize * (DeviceDpi / 96.0));
    private static readonly (RailView view, string glyph, string tip)[] Items =
    {
        (RailView.Terminal, "▤", "Terminal"),
        (RailView.Status, "⚡", "Status"),
        (RailView.Settings, "⚙", "Settings"),
        (RailView.About, "ℹ", "About"),
    };

    private readonly ToolTip _toolTip = new();

    public ActivityRail()
    {
        Width = ItemSize;
        Dock = DockStyle.Left;
        BackColor = Theme.RailBack;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Width = ItemSize;   // handle 建立後 DeviceDpi 才正確，依實際 DPI 重設寬度
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Width = ItemSize;   // 拖到不同縮放的螢幕時跟著調整
        Invalidate();
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public RailView ActiveView
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    private int IndexAt(int y)
    {
        int idx = y / ItemSize;
        return (idx >= 0 && idx < Items.Length) ? idx : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = IndexAt(e.Y);
        RailView? newHover = idx >= 0 ? Items[idx].view : null;
        if (newHover != _hover)
        {
            _hover = newHover;
            _toolTip.SetToolTip(this, idx >= 0 ? Items[idx].tip : "");
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int idx = IndexAt(e.Y);
        if (idx < 0) return;
        ActiveView = Items[idx].view;
        ViewSelected?.Invoke(this, Items[idx].view);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.RailBack);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var glyphFont = new Font("Segoe UI Symbol", 17f);
        var fmt = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        for (int i = 0; i < Items.Length; i++)
        {
            var rect = new Rectangle(0, i * ItemSize, ItemSize, ItemSize);
            bool isActive = Items[i].view == _active;
            bool isHover = _hover == Items[i].view;

            if (isHover && !isActive)
                using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, rect);

            if (isActive)
            {
                using var ab = new SolidBrush(Theme.AccentDim);
                g.FillRectangle(ab, rect);
                using var bar = new SolidBrush(Theme.Accent);
                g.FillRectangle(bar, new Rectangle(0, rect.Top, 3, ItemSize));
            }

            using var tb = new SolidBrush(isActive ? Theme.Text : Theme.TextDim);
            g.DrawString(Items[i].glyph, glyphFont, tb, rect, fmt);
        }
    }
}
