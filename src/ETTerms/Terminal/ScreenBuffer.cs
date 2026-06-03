using System.Drawing;

namespace ETTerms.Terminal;

[Flags]
public enum CellAttr : byte { None = 0, Bold = 1, Underline = 2, Inverse = 4 }

public struct Cell
{
    public char Ch;
    public Color Fg;
    public Color Bg;
    public CellAttr Attr;
}

/// <summary>
/// 終端機字格緩衝：rows×cols 畫面 + scrollback + 滾動區（DECSTBM）+ alt screen。
/// 由 <see cref="AnsiParser"/> 驅動；<see cref="TerminalView"/> 讀取繪製。座標 0-based。
/// </summary>
public sealed class ScreenBuffer
{
    public int Rows { get; private set; }
    public int Cols { get; private set; }
    public Color DefaultFg, DefaultBg;

    // 目前畫筆（SGR）
    public Color PenFg, PenBg;
    public CellAttr PenAttr;

    public int CursorRow { get; private set; }
    public int CursorCol { get; private set; }
    public bool CursorVisible = true;
    public bool AutoWrap = true;

    private Cell[][] _screen;
    private readonly List<Cell[]> _scrollback = new();
    private readonly int _maxScroll;
    private int _top, _bottom;           // 滾動區（含）
    private bool _wrapPending;

    // 儲存游標（ESC 7 / CSI s）
    private int _scx, _scy; private Color _sfg, _sbg; private CellAttr _sattr;

    // alt screen
    private Cell[][]? _mainScreen;
    public bool AltActive { get; private set; }

    public ScreenBuffer(int cols, int rows, Color fg, Color bg, int maxScrollback)
    {
        Cols = Math.Max(1, cols); Rows = Math.Max(1, rows);
        DefaultFg = fg; DefaultBg = bg; PenFg = fg; PenBg = bg;
        _maxScroll = maxScrollback;
        _screen = NewGrid(Rows, Cols);
        _top = 0; _bottom = Rows - 1;
    }

    // ── 給 TerminalView 讀取 ─────────────────────────────────
    public int ScrollbackCount => _scrollback.Count;
    public int TotalRows => _scrollback.Count + Rows;
    public Cell[] LineAt(int abs) => abs < _scrollback.Count ? _scrollback[abs] : _screen[abs - _scrollback.Count];

    // ── 內部建構工具 ─────────────────────────────────────────
    private Cell BlankPen() => new() { Ch = ' ', Fg = PenFg, Bg = PenBg, Attr = CellAttr.None };
    private Cell BlankDefault() => new() { Ch = ' ', Fg = DefaultFg, Bg = DefaultBg, Attr = CellAttr.None };
    private Cell[] BlankLine() { var l = new Cell[Cols]; for (int i = 0; i < Cols; i++) l[i] = BlankPen(); return l; }

    private Cell[][] NewGrid(int r, int c)
    {
        var g = new Cell[r][];
        for (int i = 0; i < r; i++) { g[i] = new Cell[c]; for (int j = 0; j < c; j++) g[i][j] = BlankDefault(); }
        return g;
    }

    private void PushScroll(Cell[] line)
    {
        _scrollback.Add(line);
        if (_scrollback.Count > _maxScroll) _scrollback.RemoveRange(0, _scrollback.Count - _maxScroll);
    }

    // ── 輸出字元 ─────────────────────────────────────────────
    public void Print(char ch)
    {
        if (_wrapPending) { _wrapPending = false; CursorCol = 0; LineFeed(); }
        if (CursorRow < 0 || CursorRow >= Rows) CursorRow = Math.Clamp(CursorRow, 0, Rows - 1);
        _screen[CursorRow][CursorCol] = new Cell { Ch = ch, Fg = PenFg, Bg = PenBg, Attr = PenAttr };
        if (CursorCol >= Cols - 1) { if (AutoWrap) _wrapPending = true; }
        else CursorCol++;
    }

    // ── 游標 / 換行 ──────────────────────────────────────────
    public void CarriageReturn() { _wrapPending = false; CursorCol = 0; }

    public void LineFeed()
    {
        _wrapPending = false;
        if (CursorRow == _bottom) ScrollUp(1);
        else if (CursorRow < Rows - 1) CursorRow++;
    }

    public void ReverseLineFeed()
    {
        _wrapPending = false;
        if (CursorRow == _top) ScrollDown(1);
        else if (CursorRow > 0) CursorRow--;
    }

    public void NextLine() { CarriageReturn(); LineFeed(); }
    public void Backspace() { _wrapPending = false; if (CursorCol > 0) CursorCol--; }
    public void Tab() { _wrapPending = false; CursorCol = Math.Min(Cols - 1, (CursorCol / 8 + 1) * 8); }

    public void MoveCursor(int row, int col)
    {
        _wrapPending = false;
        CursorRow = Math.Clamp(row, 0, Rows - 1);
        CursorCol = Math.Clamp(col, 0, Cols - 1);
    }
    public void CursorUp(int n) { _wrapPending = false; CursorRow = Math.Max(_top, CursorRow - n); }
    public void CursorDown(int n) { _wrapPending = false; CursorRow = Math.Min(_bottom, CursorRow + n); }
    public void CursorLeft(int n) { _wrapPending = false; CursorCol = Math.Max(0, CursorCol - n); }
    public void CursorRight(int n) { _wrapPending = false; CursorCol = Math.Min(Cols - 1, CursorCol + n); }
    public void SetColumn(int col) { _wrapPending = false; CursorCol = Math.Clamp(col, 0, Cols - 1); }
    public void SetRow(int row) { _wrapPending = false; CursorRow = Math.Clamp(row, 0, Rows - 1); }

    public void SaveCursor() { _scx = CursorCol; _scy = CursorRow; _sfg = PenFg; _sbg = PenBg; _sattr = PenAttr; }
    public void RestoreCursor() { CursorCol = _scx; CursorRow = _scy; PenFg = _sfg; PenBg = _sbg; PenAttr = _sattr; _wrapPending = false; }

    // ── 滾動 ─────────────────────────────────────────────────
    public void SetScrollRegion(int top, int bottom)
    {
        _top = Math.Clamp(top, 0, Rows - 1);
        _bottom = Math.Clamp(bottom, _top, Rows - 1);
        MoveCursor(0, 0);
    }

    public void ScrollUp(int n)
    {
        for (int k = 0; k < n; k++)
        {
            var line = _screen[_top];
            if (_top == 0 && !AltActive) PushScroll(line);
            for (int r = _top; r < _bottom; r++) _screen[r] = _screen[r + 1];
            _screen[_bottom] = BlankLine();
        }
    }

    public void ScrollDown(int n)
    {
        for (int k = 0; k < n; k++)
        {
            for (int r = _bottom; r > _top; r--) _screen[r] = _screen[r - 1];
            _screen[_top] = BlankLine();
        }
    }

    public void InsertLines(int n)
    {
        if (CursorRow < _top || CursorRow > _bottom) return;
        for (int k = 0; k < n; k++)
        {
            for (int r = _bottom; r > CursorRow; r--) _screen[r] = _screen[r - 1];
            _screen[CursorRow] = BlankLine();
        }
    }

    public void DeleteLines(int n)
    {
        if (CursorRow < _top || CursorRow > _bottom) return;
        for (int k = 0; k < n; k++)
        {
            for (int r = CursorRow; r < _bottom; r++) _screen[r] = _screen[r + 1];
            _screen[_bottom] = BlankLine();
        }
    }

    // ── 行內字元操作 ─────────────────────────────────────────
    public void InsertChars(int n)
    {
        var row = _screen[CursorRow];
        for (int c = Cols - 1; c >= CursorCol + n; c--) row[c] = row[c - n];
        for (int c = CursorCol; c < Math.Min(Cols, CursorCol + n); c++) row[c] = BlankPen();
    }

    public void DeleteChars(int n)
    {
        var row = _screen[CursorRow];
        for (int c = CursorCol; c < Cols; c++) row[c] = (c + n < Cols) ? row[c + n] : BlankPen();
    }

    public void EraseChars(int n)
    {
        var row = _screen[CursorRow];
        for (int c = CursorCol; c < Math.Min(Cols, CursorCol + n); c++) row[c] = BlankPen();
    }

    // ── 清除 ─────────────────────────────────────────────────
    public void EraseInLine(int mode)
    {
        var row = _screen[CursorRow];
        int from = mode == 1 ? 0 : CursorCol;
        int to = mode == 0 ? Cols - 1 : (mode == 1 ? CursorCol : Cols - 1);
        for (int c = from; c <= to; c++) row[c] = BlankPen();
    }

    public void EraseInDisplay(int mode)
    {
        if (mode == 3) { _scrollback.Clear(); return; }
        if (mode == 2) { for (int r = 0; r < Rows; r++) _screen[r] = BlankLine(); return; }
        if (mode == 0)
        {
            EraseInLine(0);
            for (int r = CursorRow + 1; r < Rows; r++) _screen[r] = BlankLine();
        }
        else // mode 1
        {
            EraseInLine(1);
            for (int r = 0; r < CursorRow; r++) _screen[r] = BlankLine();
        }
    }

    // ── alt screen ───────────────────────────────────────────
    public void EnterAlt()
    {
        if (AltActive) return;
        _mainScreen = _screen;
        _screen = NewGrid(Rows, Cols);
        AltActive = true;
        _top = 0; _bottom = Rows - 1;
        MoveCursor(0, 0);
    }

    public void ExitAlt()
    {
        if (!AltActive || _mainScreen == null) return;
        _screen = _mainScreen;
        _mainScreen = null;
        AltActive = false;
        _top = 0; _bottom = Rows - 1;
    }

    public void ResetAttrs() { PenFg = DefaultFg; PenBg = DefaultBg; PenAttr = CellAttr.None; }

    // ── resize（簡單 reflow：保留左上重疊區）──────────────────
    public void Resize(int cols, int rows)
    {
        cols = Math.Max(1, cols); rows = Math.Max(1, rows);
        if (cols == Cols && rows == Rows) return;
        var ng = new Cell[rows][];
        for (int r = 0; r < rows; r++)
        {
            ng[r] = new Cell[cols];
            for (int c = 0; c < cols; c++)
                ng[r][c] = (r < Rows && c < Cols) ? _screen[r][c] : BlankDefault();
        }
        _screen = ng;
        Rows = rows; Cols = cols;
        _top = 0; _bottom = Rows - 1;
        CursorRow = Math.Clamp(CursorRow, 0, Rows - 1);
        CursorCol = Math.Clamp(CursorCol, 0, Cols - 1);
        _wrapPending = false;
        _mainScreen = null;
        if (AltActive) { /* alt 下 resize：重建 alt 畫面 */ }
    }
}
