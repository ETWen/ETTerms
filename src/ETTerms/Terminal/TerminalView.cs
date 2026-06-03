using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ETTerms.Terminal;

/// <summary>
/// 自繪 VT100 終端機控制項：bytes → AnsiParser → ScreenBuffer → 雙緩衝繪製。
/// 支援 scrollback（滑鼠滾輪）、選取 / 複製（Ctrl+C）/ 貼上（Ctrl+V、右鍵、Shift+Insert）、resize 通知。
/// </summary>
public sealed class TerminalView : UserControl
{
    public event Action<byte[]>? SendData;      // 鍵盤 / 貼上 → channel
    public event Action<int, int>? Resized;     // cols, rows（PTY size）

    private readonly ScreenBuffer _buf;
    private readonly AnsiParser _parser;
    private readonly Font _font;
    private int _cellW, _cellH;
    private int _scrollOffset;                  // 0 = 貼底；>0 = 往上看 scrollback
    private int _lastCols = -1, _lastRows = -1;

    // 選取（以絕對行 abs、欄 col 表示）
    private bool _selecting;
    private (int row, int col) _selStart, _selEnd;
    private bool _hasSel;

    public TerminalView(TerminalProfile profile)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(18, 18, 22);
        _font = new Font(profile.FontFamily, profile.FontSize);
        using (var g = CreateGraphics())
        {
            var sz = TextRenderer.MeasureText(g, "W", _font, Size.Empty, TextFormatFlags.NoPadding);
            _cellW = Math.Max(1, sz.Width);
            _cellH = Math.Max(1, _font.Height);
        }
        _buf = new ScreenBuffer(profile.Cols, profile.Rows,
            Color.FromArgb(220, 220, 220), BackColor, profile.ScrollbackLines);
        _parser = new AnsiParser(_buf);
    }

    /// <summary>餵入遠端資料（須在 UI thread 呼叫）。</summary>
    public void Feed(byte[] data)
    {
        _parser.Feed(data);
        _scrollOffset = 0;          // 新輸出貼底
        Invalidate();
    }

    private int VisibleRows => Math.Max(1, ClientSize.Height / _cellH);
    private int VisibleCols => Math.Max(1, ClientSize.Width / _cellW);

    // ── resize → 通知 PTY ────────────────────────────────────
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        int cols = VisibleCols, rows = VisibleRows;
        if (cols == _lastCols && rows == _lastRows) return;
        _lastCols = cols; _lastRows = rows;
        _buf.Resize(cols, rows);
        Resized?.Invoke(cols, rows);
        Invalidate();
    }

    // ── 繪製 ─────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        int rows = VisibleRows;
        int top = _buf.ScrollbackCount - _scrollOffset;   // 視窗第一列的絕對 index
        if (top < 0) top = 0;

        for (int vr = 0; vr < rows; vr++)
        {
            int abs = top + vr;
            if (abs >= _buf.TotalRows) break;
            DrawLine(g, _buf.LineAt(abs), vr, abs);
        }
        DrawCursor(g, top, rows);
    }

    private void DrawLine(Graphics g, Cell[] line, int vr, int abs)
    {
        int y = vr * _cellH;
        int c = 0;
        while (c < line.Length)
        {
            var cell = line[c];
            ResolveColors(cell, out var fg, out var bg, abs, c);
            // 合併同屬性連續格
            int start = c;
            var sb = new StringBuilder();
            while (c < line.Length)
            {
                var cur = line[c];
                ResolveColors(cur, out var f2, out var b2, abs, c);
                if (f2 != fg || b2 != bg || (cur.Attr & CellAttr.Bold) != (cell.Attr & CellAttr.Bold)) break;
                sb.Append(cur.Ch == '\0' ? ' ' : cur.Ch);
                c++;
            }
            var rect = new Rectangle(start * _cellW, y, (c - start) * _cellW, _cellH);
            using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, rect);
            var style = (cell.Attr & CellAttr.Bold) != 0 ? FontStyle.Bold : FontStyle.Regular;
            if ((cell.Attr & CellAttr.Underline) != 0) style |= FontStyle.Underline;
            using var fnt = style == FontStyle.Regular ? _font : new Font(_font, style);
            TextRenderer.DrawText(g, sb.ToString(), fnt, rect, fg,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
        }
    }

    private void ResolveColors(Cell cell, out Color fg, out Color bg, int abs, int col)
    {
        fg = cell.Fg.A == 0 ? _buf.DefaultFg : cell.Fg;
        bg = cell.Bg.A == 0 ? _buf.DefaultBg : cell.Bg;
        if ((cell.Attr & CellAttr.Inverse) != 0) (fg, bg) = (bg, fg);
        if (_hasSel && InSelection(abs, col)) (fg, bg) = (bg, Color.FromArgb(70, 90, 140));
    }

    private void DrawCursor(Graphics g, int top, int rows)
    {
        if (!_buf.CursorVisible || _scrollOffset != 0 || !Focused) return;
        int vr = (_buf.ScrollbackCount + _buf.CursorRow) - top;
        if (vr < 0 || vr >= rows) return;
        var rect = new Rectangle(_buf.CursorCol * _cellW, vr * _cellH, _cellW, _cellH);
        using var b = new SolidBrush(Color.FromArgb(160, 200, 200, 200));
        g.FillRectangle(b, rect);
    }

    // ── 鍵盤 ─────────────────────────────────────────────────
    protected override bool IsInputKey(Keys keyData) => true;   // 攔截方向鍵 / Tab

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C && _hasSel) { CopySelection(); e.Handled = e.SuppressKeyPress = true; return; }
        if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert)) { Paste(); e.Handled = e.SuppressKeyPress = true; return; }

        var bytes = TerminalInput.Map(e, _parser.AppCursorKeys);
        if (bytes != null) { SendData?.Invoke(bytes); e.Handled = e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) || e.KeyChar is '\r' or '\t' or '\b' or '\x1b')
        {
            // \r,\t,\b,\x1b 已由 OnKeyDown 送出，這裡只送一般可列印字元與 Ctrl 組合碼
        }
        if (e.KeyChar >= ' ' && e.KeyChar != '\x7f')
            SendData?.Invoke(Encoding.UTF8.GetBytes(e.KeyChar.ToString()));
        else if (char.IsControl(e.KeyChar) && e.KeyChar is not ('\r' or '\t' or '\b' or '\x1b'))
            SendData?.Invoke(new[] { (byte)e.KeyChar });   // Ctrl+letter 控制碼
        e.Handled = true;
    }

    // ── 滑鼠：scrollback / 選取 / 貼上 ───────────────────────
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int delta = e.Delta / 120 * 3;
        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, _buf.ScrollbackCount);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            _selecting = true; _hasSel = false;
            _selStart = _selEnd = HitCell(e.Location);
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (_hasSel) CopySelection(); else Paste();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_selecting) return;
        _selEnd = HitCell(e.Location);
        _hasSel = _selStart != _selEnd;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) => _selecting = false;

    private (int row, int col) HitCell(Point p)
    {
        int top = _buf.ScrollbackCount - _scrollOffset;
        int row = top + Math.Clamp(p.Y / _cellH, 0, VisibleRows - 1);
        int col = Math.Clamp(p.X / _cellW, 0, _buf.Cols);
        return (row, col);
    }

    private bool InSelection(int abs, int col)
    {
        var (a, b) = Ordered();
        if (abs < a.row || abs > b.row) return false;
        if (abs == a.row && col < a.col) return false;
        if (abs == b.row && col >= b.col) return false;
        return true;
    }

    private ((int row, int col) a, (int row, int col) b) Ordered()
    {
        var s = _selStart; var e = _selEnd;
        bool sFirst = s.row < e.row || (s.row == e.row && s.col <= e.col);
        return sFirst ? (s, e) : (e, s);
    }

    private void CopySelection()
    {
        var (a, b) = Ordered();
        var sb = new StringBuilder();
        for (int abs = a.row; abs <= b.row && abs < _buf.TotalRows; abs++)
        {
            var line = _buf.LineAt(abs);
            int from = abs == a.row ? a.col : 0;
            int to = abs == b.row ? b.col : line.Length;
            for (int c = from; c < Math.Min(to, line.Length); c++)
                sb.Append(line[c].Ch == '\0' ? ' ' : line[c].Ch);
            if (abs < b.row) sb.Append("\r\n");
        }
        var text = sb.ToString();
        if (text.Length > 0) { try { Clipboard.SetText(text); } catch { } }
    }

    private void Paste()
    {
        try
        {
            if (Clipboard.ContainsText())
                SendData?.Invoke(Encoding.UTF8.GetBytes(Clipboard.GetText().Replace("\r\n", "\r")));
        }
        catch { }
    }
}
