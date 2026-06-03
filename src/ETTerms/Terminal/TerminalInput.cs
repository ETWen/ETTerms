using System.Text;
using System.Windows.Forms;

namespace ETTerms.Terminal;

/// <summary>把特殊鍵（方向鍵 / Fn / Home/End… / Enter/Tab/Backspace/Esc）對應成終端機 byte 序列。
/// 一般可列印字元回傳 null，交給 KeyPress 處理。</summary>
public static class TerminalInput
{
    public static byte[]? Map(KeyEventArgs e, bool appCursor)
    {
        string? seq = e.KeyCode switch
        {
            Keys.Up => appCursor ? "\x1bOA" : "\x1b[A",
            Keys.Down => appCursor ? "\x1bOB" : "\x1b[B",
            Keys.Right => appCursor ? "\x1bOC" : "\x1b[C",
            Keys.Left => appCursor ? "\x1bOD" : "\x1b[D",
            Keys.Home => "\x1b[H",
            Keys.End => "\x1b[F",
            Keys.Insert => "\x1b[2~",
            Keys.Delete => "\x1b[3~",
            Keys.PageUp => "\x1b[5~",
            Keys.PageDown => "\x1b[6~",
            Keys.Enter => "\r",
            Keys.Tab => "\t",
            Keys.Escape => "\x1b",
            Keys.Back => "\x7f",
            Keys.F1 => "\x1bOP",
            Keys.F2 => "\x1bOQ",
            Keys.F3 => "\x1bOR",
            Keys.F4 => "\x1bOS",
            Keys.F5 => "\x1b[15~",
            Keys.F6 => "\x1b[17~",
            Keys.F7 => "\x1b[18~",
            Keys.F8 => "\x1b[19~",
            Keys.F9 => "\x1b[20~",
            Keys.F10 => "\x1b[21~",
            Keys.F11 => "\x1b[23~",
            Keys.F12 => "\x1b[24~",
            _ => null
        };
        return seq is null ? null : Encoding.ASCII.GetBytes(seq);
    }
}
