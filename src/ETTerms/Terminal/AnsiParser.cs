using System.Drawing;
using System.Text;

namespace ETTerms.Terminal;

/// <summary>VT100 / 常見 ANSI escape 狀態機，驅動 <see cref="ScreenBuffer"/>。</summary>
public sealed class AnsiParser
{
    private enum State { Ground, Esc, Csi, Osc }

    private readonly ScreenBuffer _b;
    private readonly Decoder _dec = Encoding.UTF8.GetDecoder();
    private State _state = State.Ground;
    private readonly StringBuilder _params = new();
    private bool _priv;
    private bool _oscEsc;
    private int _skipCharset;

    /// <summary>DECCKM：application cursor keys（影響方向鍵送出序列）。</summary>
    public bool AppCursorKeys { get; private set; }

    public AnsiParser(ScreenBuffer buffer) => _b = buffer;

    public void Feed(byte[] data)
    {
        var chars = new char[data.Length];
        int n = _dec.GetChars(data, 0, data.Length, chars, 0);
        for (int i = 0; i < n; i++) Step(chars[i]);
    }

    private void Step(char ch)
    {
        if (_skipCharset > 0) { _skipCharset--; return; }
        switch (_state)
        {
            case State.Ground: Ground(ch); break;
            case State.Esc: Esc(ch); break;
            case State.Csi: Csi(ch); break;
            case State.Osc: Osc(ch); break;
        }
    }

    private void Ground(char ch)
    {
        switch (ch)
        {
            case '\x1b': _state = State.Esc; break;
            case '\r': _b.CarriageReturn(); break;
            case '\n': case '\v': case '\f': _b.LineFeed(); break;
            case '\b': _b.Backspace(); break;
            case '\t': _b.Tab(); break;
            case '\a': break;
            default: if (ch >= ' ') _b.Print(ch); break;
        }
    }

    private void Esc(char ch)
    {
        switch (ch)
        {
            case '[': _params.Clear(); _priv = false; _state = State.Csi; break;
            case ']': _oscEsc = false; _state = State.Osc; break;
            case '(': case ')': case '*': case '+': _skipCharset = 1; _state = State.Ground; break;
            case '7': _b.SaveCursor(); _state = State.Ground; break;
            case '8': _b.RestoreCursor(); _state = State.Ground; break;
            case 'M': _b.ReverseLineFeed(); _state = State.Ground; break;
            case 'D': _b.LineFeed(); _state = State.Ground; break;
            case 'E': _b.NextLine(); _state = State.Ground; break;
            case 'c': _b.ResetAttrs(); _b.EraseInDisplay(2); _b.MoveCursor(0, 0); _state = State.Ground; break;
            default: _state = State.Ground; break;
        }
    }

    private void Csi(char ch)
    {
        if (ch == '?' || ch == '>' || ch == '!') { _priv = true; return; }
        if ((ch >= '0' && ch <= '9') || ch == ';') { _params.Append(ch); return; }
        if (ch >= 0x40 && ch <= 0x7e) { Dispatch(ch); _state = State.Ground; return; }
        // 其餘 intermediate 忽略
    }

    private void Osc(char ch)
    {
        if (ch == '\a') { _state = State.Ground; return; }
        if (_oscEsc && ch == '\\') { _state = State.Ground; return; }
        _oscEsc = ch == '\x1b';
    }

    private int[] Params()
    {
        if (_params.Length == 0) return Array.Empty<int>();
        var parts = _params.ToString().Split(';');
        var r = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) r[i] = int.TryParse(parts[i], out var v) ? v : 0;
        return r;
    }

    private int P(int[] p, int i, int def) => (i < p.Length && p[i] > 0) ? p[i] : def;

    private void Dispatch(char f)
    {
        var p = Params();
        switch (f)
        {
            case 'A': _b.CursorUp(P(p, 0, 1)); break;
            case 'B': _b.CursorDown(P(p, 0, 1)); break;
            case 'C': _b.CursorRight(P(p, 0, 1)); break;
            case 'D': _b.CursorLeft(P(p, 0, 1)); break;
            case 'E': _b.MoveCursor(_b.CursorRow + P(p, 0, 1), 0); break;
            case 'F': _b.MoveCursor(_b.CursorRow - P(p, 0, 1), 0); break;
            case 'G': case '`': _b.SetColumn(P(p, 0, 1) - 1); break;
            case 'd': _b.SetRow(P(p, 0, 1) - 1); break;
            case 'H': case 'f': _b.MoveCursor(P(p, 0, 1) - 1, P(p, 1, 1) - 1); break;
            case 'J': _b.EraseInDisplay(p.Length > 0 ? p[0] : 0); break;
            case 'K': _b.EraseInLine(p.Length > 0 ? p[0] : 0); break;
            case 'L': _b.InsertLines(P(p, 0, 1)); break;
            case 'M': _b.DeleteLines(P(p, 0, 1)); break;
            case 'P': _b.DeleteChars(P(p, 0, 1)); break;
            case '@': _b.InsertChars(P(p, 0, 1)); break;
            case 'X': _b.EraseChars(P(p, 0, 1)); break;
            case 'S': _b.ScrollUp(P(p, 0, 1)); break;
            case 'T': _b.ScrollDown(P(p, 0, 1)); break;
            case 'r': _b.SetScrollRegion(P(p, 0, 1) - 1, p.Length > 1 && p[1] > 0 ? p[1] - 1 : _b.Rows - 1); break;
            case 's': _b.SaveCursor(); break;
            case 'u': _b.RestoreCursor(); break;
            case 'h': SetMode(p, true); break;
            case 'l': SetMode(p, false); break;
            case 'm': Sgr(p); break;
        }
    }

    private void SetMode(int[] p, bool set)
    {
        if (!_priv) return;
        foreach (var code in p)
            switch (code)
            {
                case 25: _b.CursorVisible = set; break;
                case 7: _b.AutoWrap = set; break;
                case 1: AppCursorKeys = set; break;
                case 47: case 1047: case 1049:
                    if (set) _b.EnterAlt(); else _b.ExitAlt(); break;
            }
    }

    private void Sgr(int[] p)
    {
        if (p.Length == 0) { _b.ResetAttrs(); return; }
        for (int i = 0; i < p.Length; i++)
        {
            int c = p[i];
            switch (c)
            {
                case 0: _b.ResetAttrs(); break;
                case 1: _b.PenAttr |= CellAttr.Bold; break;
                case 4: _b.PenAttr |= CellAttr.Underline; break;
                case 7: _b.PenAttr |= CellAttr.Inverse; break;
                case 22: _b.PenAttr &= ~CellAttr.Bold; break;
                case 24: _b.PenAttr &= ~CellAttr.Underline; break;
                case 27: _b.PenAttr &= ~CellAttr.Inverse; break;
                case 39: _b.PenFg = _b.DefaultFg; break;
                case 49: _b.PenBg = _b.DefaultBg; break;
                case >= 30 and <= 37: _b.PenFg = Palette.Ansi(c - 30); break;
                case >= 40 and <= 47: _b.PenBg = Palette.Ansi(c - 40); break;
                case >= 90 and <= 97: _b.PenFg = Palette.Ansi(c - 90 + 8); break;
                case >= 100 and <= 107: _b.PenBg = Palette.Ansi(c - 100 + 8); break;
                case 38: i = Ext(p, i, true); break;
                case 48: i = Ext(p, i, false); break;
            }
        }
    }

    private int Ext(int[] p, int i, bool fg)
    {
        if (i + 1 < p.Length && p[i + 1] == 5 && i + 2 < p.Length)
        {
            var col = Palette.Xterm256(p[i + 2]);
            if (fg) _b.PenFg = col; else _b.PenBg = col;
            return i + 2;
        }
        if (i + 1 < p.Length && p[i + 1] == 2 && i + 4 < p.Length)
        {
            var col = Color.FromArgb(p[i + 2], p[i + 3], p[i + 4]);
            if (fg) _b.PenFg = col; else _b.PenBg = col;
            return i + 4;
        }
        return i;
    }
}

/// <summary>ANSI 16 色 + xterm-256 色盤。</summary>
public static class Palette
{
    private static readonly Color[] Base16 =
    {
        Color.FromArgb(0,0,0),       Color.FromArgb(205,49,49),   Color.FromArgb(13,188,121),  Color.FromArgb(229,229,16),
        Color.FromArgb(36,114,200),  Color.FromArgb(188,63,188),  Color.FromArgb(17,168,205),  Color.FromArgb(229,229,229),
        Color.FromArgb(102,102,102), Color.FromArgb(241,76,76),   Color.FromArgb(35,209,139),  Color.FromArgb(245,245,67),
        Color.FromArgb(59,142,234),  Color.FromArgb(214,112,214), Color.FromArgb(41,184,219),  Color.FromArgb(255,255,255),
    };

    public static Color Ansi(int i) => Base16[Math.Clamp(i, 0, 15)];

    public static Color Xterm256(int n)
    {
        if (n < 16) return Base16[n];
        if (n < 232)
        {
            n -= 16;
            int r = n / 36, g = (n / 6) % 6, b = n % 6;
            static int V(int x) => x == 0 ? 0 : 55 + x * 40;
            return Color.FromArgb(V(r), V(g), V(b));
        }
        int v = 8 + (n - 232) * 10;
        return Color.FromArgb(v, v, v);
    }
}
