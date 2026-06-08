using System.Drawing;
using System.Runtime.InteropServices;
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
    private readonly DarkScrollBar _vscroll;     // 右側深色垂直捲軸（可拖曳捲動 scrollback）
    private bool _suppressScroll;                // 程式設定捲軸值時抑制回呼
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
        ImeMode = ImeMode.NoControl;   // 容器控制項預設關 IME，這裡明確開啟讓使用者可切中文
        _font = new Font(profile.FontFamily, profile.FontSize);
        using (var g = CreateGraphics())
        {
            var sz = TextRenderer.MeasureText(g, "W", _font, Size.Empty, TextFormatFlags.NoPadding);
            _cellW = Math.Max(1, sz.Width);
            _cellH = Math.Max(1, _font.Height + 2);   // +2 行距，避免 g/y 下緣與中文底部被裁切
        }
        _buf = new ScreenBuffer(profile.Cols, profile.Rows,
            Color.FromArgb(220, 220, 220), BackColor, profile.ScrollbackLines);
        _parser = new AnsiParser(_buf);

        // 右側深色垂直捲軸：拖曳即捲動 scrollback。內容寬度會扣掉捲軸寬，故文字不會被蓋住。
        _vscroll = new DarkScrollBar { Dock = DockStyle.Right, SmallChange = 1, Minimum = 0, Maximum = 0 };
        _vscroll.Scroll += OnVScroll;
        Controls.Add(_vscroll);
    }

    /// <summary>餵入遠端資料（須在 UI thread 呼叫）。</summary>
    public void Feed(byte[] data)
    {
        _parser.Feed(data);
        _scrollOffset = 0;          // 新輸出貼底
        UpdateScrollBar();
        Invalidate();
    }

    private int ContentWidth => Math.Max(_cellW, ClientSize.Width - (_vscroll?.Width ?? 0));
    private int VisibleRows => Math.Max(1, ClientSize.Height / _cellH);
    private int VisibleCols => Math.Max(1, ContentWidth / _cellW);

    // ── resize → 通知 PTY ────────────────────────────────────
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        // 視窗最小化、或分頁/分割切換的瞬間，ClientSize 會變成 0/極小。此時若把退化尺寸
        // （會被算成 1×1）送給 PTY，ConPTY 下的全螢幕 TUI（如 Kiro CLI）會誤以為終端機只剩
        // 1×1 而停止重繪、輸入也像「卡住」。直接忽略這種尺寸，還原後再以正常尺寸重繪即可。
        if (ClientSize.Width < _cellW || ClientSize.Height < _cellH) return;
        if (FindForm() is { WindowState: FormWindowState.Minimized }) return;

        int cols = VisibleCols, rows = VisibleRows;
        if (cols == _lastCols && rows == _lastRows) return;
        _lastCols = cols; _lastRows = rows;
        _buf.Resize(cols, rows);
        Resized?.Invoke(cols, rows);
        UpdateScrollBar();
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
            if ((cell.Attr & CellAttr.WideTrail) != 0) { c++; continue; }   // 寬字第二格，由前格覆蓋
            ResolveColors(cell, out var fg, out var bg, abs, c);

            // 寬字(全形 CJK)：單獨繪製，占 2 格寬
            if ((cell.Attr & CellAttr.Wide) != 0)
            {
                var wr = new Rectangle(c * _cellW, y, _cellW * 2, _cellH);
                using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, wr);
                DrawRun(g, cell.Ch == '\0' ? " " : cell.Ch.ToString(), cell.Attr, wr, fg);
                c++;
                continue;
            }

            // 合併同屬性連續的窄字
            int start = c;
            var sb = new StringBuilder();
            while (c < line.Length)
            {
                var cur = line[c];
                if ((cur.Attr & (CellAttr.Wide | CellAttr.WideTrail)) != 0) break;
                ResolveColors(cur, out var f2, out var b2, abs, c);
                if (f2 != fg || b2 != bg || (cur.Attr & CellAttr.Bold) != (cell.Attr & CellAttr.Bold)) break;
                sb.Append(cur.Ch == '\0' ? ' ' : cur.Ch);
                c++;
            }
            var rect = new Rectangle(start * _cellW, y, (c - start) * _cellW, _cellH);
            using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, rect);
            DrawRun(g, sb.ToString(), cell.Attr, rect, fg);
        }
    }

    private void DrawRun(Graphics g, string text, CellAttr attr, Rectangle rect, Color fg)
    {
        var style = (attr & CellAttr.Bold) != 0 ? FontStyle.Bold : FontStyle.Regular;
        if ((attr & CellAttr.Underline) != 0) style |= FontStyle.Underline;
        using var fnt = style == FontStyle.Regular ? _font : new Font(_font, style);
        TextRenderer.DrawText(g, text, fnt, rect, fg,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left);
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
        UpdateScrollBar();
        Invalidate();
    }

    // ── 垂直捲軸 ─────────────────────────────────────────────
    private void OnVScroll(object? sender, EventArgs e)
    {
        if (_suppressScroll) return;
        // 捲軸值 = 視窗第一列的絕對 index；_scrollOffset = 距底部的列數。
        _scrollOffset = Math.Clamp(_buf.ScrollbackCount - _vscroll.Value, 0, _buf.ScrollbackCount);
        Invalidate();
    }

    /// <summary>依目前 scrollback / 捲動位置同步捲軸的範圍與滑塊位置。</summary>
    private void UpdateScrollBar()
    {
        if (_vscroll is null) return;
        _suppressScroll = true;
        try
        {
            int visible = VisibleRows;
            int total = _buf.TotalRows;
            if (_buf.ScrollbackCount <= 0)
            {
                _vscroll.Enabled = false;
                _vscroll.LargeChange = 1;
                _vscroll.Maximum = 0;
                _vscroll.Value = 0;
                return;
            }
            _vscroll.Enabled = true;
            // 先設 LargeChange/Maximum 再設 Value，避免 Value 超出可達範圍被夾掉。
            _vscroll.LargeChange = Math.Max(1, visible);
            _vscroll.Maximum = Math.Max(0, total - 1);
            int top = _buf.ScrollbackCount - _scrollOffset;          // 視窗第一列絕對 index
            int maxValue = Math.Max(0, _vscroll.Maximum - _vscroll.LargeChange + 1);
            _vscroll.Value = Math.Clamp(top, 0, maxValue);
        }
        finally { _suppressScroll = false; }
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
            if (_hasSel) { CopySelection(); _hasSel = false; Invalidate(); }   // 複製後清掉反白，讓 user 知道已動作
            else Paste();
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
            {
                if ((line[c].Attr & CellAttr.WideTrail) != 0) continue;
                sb.Append(line[c].Ch == '\0' ? ' ' : line[c].Ch);
            }
            if (abs < b.row) sb.Append("\r\n");
        }
        var text = sb.ToString();
        if (text.Length > 0) { try { Clipboard.SetText(text); } catch { } }
    }

    private void Paste()
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            // 換行統一成 CR：剪貼簿多為 \r\n，也處理單獨的 \n。
            var text = Clipboard.GetText().Replace("\r\n", "\r").Replace('\n', '\r');

            // 應用程式啟用 bracketed paste（PSReadLine / Kiro CLI 等）時，以 ESC[200~ … ESC[201~
            // 包夾整段貼上內容，讓對方視為「單次貼上」而非逐行 Enter 立即送出；
            // 未啟用時才退回原本逐字送出（一般 shell 的預期行為）。
            var data = _parser.BracketedPaste
                ? Encoding.UTF8.GetBytes("\x1b[200~" + text + "\x1b[201~")
                : Encoding.UTF8.GetBytes(text);
            SendData?.Invoke(data);
        }
        catch { }
    }

    // ── IME（中文 / 日文 / 韓文輸入）─────────────────────────
    // 自繪控制項預設不處理 IME 組字，故攔截 WM_IME_COMPOSITION 取「結果字串」直接送出 UTF-8。
    private const int WM_IME_STARTCOMPOSITION = 0x010D;
    private const int WM_IME_COMPOSITION = 0x010F;
    private const int GCS_RESULTSTR = 0x0800;
    private const int CFS_POINT = 0x0002;
    private const int IACE_DEFAULT = 0x0010;

    // UserControl(容器)預設不啟用 IME，使用者無法切中文。把預設 IME context 綁回此視窗，
    // 並在取得焦點後(WinForms 可能於 OnGotFocus 內關掉)再綁一次，確保可切換輸入法。
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ImmAssociateContextEx(Handle, IntPtr.Zero, IACE_DEFAULT);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        ImmAssociateContextEx(Handle, IntPtr.Zero, IACE_DEFAULT);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_IME_STARTCOMPOSITION) MoveImeWindowToCursor();
        else if (m.Msg == WM_IME_COMPOSITION && ((long)m.LParam & GCS_RESULTSTR) != 0)
        {
            var s = ReadImeResult();
            if (!string.IsNullOrEmpty(s))
            {
                SendData?.Invoke(Encoding.UTF8.GetBytes(s));
                return;   // 消費此訊息，避免預設再轉成 WM_CHAR 造成重複輸入
            }
        }
        base.WndProc(ref m);
    }

    private string ReadImeResult()
    {
        IntPtr hImc = ImmGetContext(Handle);
        if (hImc == IntPtr.Zero) return "";
        try
        {
            int len = ImmGetCompositionStringW(hImc, GCS_RESULTSTR, null, 0);
            if (len <= 0) return "";
            var buf = new byte[len];
            ImmGetCompositionStringW(hImc, GCS_RESULTSTR, buf, len);
            return Encoding.Unicode.GetString(buf);
        }
        finally { ImmReleaseContext(Handle, hImc); }
    }

    private void MoveImeWindowToCursor()
    {
        IntPtr hImc = ImmGetContext(Handle);
        if (hImc == IntPtr.Zero) return;
        try
        {
            int vr = _buf.CursorRow + _scrollOffset;
            var cf = new COMPOSITIONFORM
            {
                dwStyle = CFS_POINT,
                ptCurrentPos = new POINT { x = _buf.CursorCol * _cellW, y = vr * _cellH }
            };
            ImmSetCompositionWindow(hImc, ref cf);
        }
        finally { ImmReleaseContext(Handle, hImc); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct COMPOSITIONFORM { public int dwStyle; public POINT ptCurrentPos; public RECT rcArea; }

    [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr hWnd);
    [DllImport("imm32.dll")] private static extern bool ImmAssociateContextEx(IntPtr hWnd, IntPtr hIMC, int dwFlags);
    [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
    [DllImport("imm32.dll", CharSet = CharSet.Unicode)] private static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);
    [DllImport("imm32.dll")] private static extern bool ImmSetCompositionWindow(IntPtr hIMC, ref COMPOSITIONFORM lpCompForm);
}
