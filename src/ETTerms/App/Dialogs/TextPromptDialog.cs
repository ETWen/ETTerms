using System.Drawing;
using System.Windows.Forms;

namespace ETTerms.App.Dialogs;

/// <summary>單行文字輸入對話框（資料夾命名 / 改名用）。</summary>
public sealed class TextPromptDialog : DarkDialog
{
    private readonly TextBox _input;

    public string Value => _input.Text.Trim();

    public TextPromptDialog(string title, string prompt, string initial = "")
    {
        Text = title;
        ClientSize = new Size(360, 130);

        var lbl = MakeLabel(prompt);
        lbl.Location = new Point(16, 16);

        _input = MakeTextBox(initial);
        _input.SetBounds(16, 42, 328, 26);
        _input.SelectAll();

        var ok = MakeButton("OK", DialogResult.OK, accent: true);
        ok.Location = new Point(164, 84);
        var cancel = MakeButton("Cancel", DialogResult.Cancel);
        cancel.Location = new Point(256, 84);

        Controls.AddRange(new Control[] { lbl, _input, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
    }

    /// <summary>顯示對話框；回傳輸入值，取消或空白回傳 null。</summary>
    public static string? Ask(IWin32Window owner, string title, string prompt, string initial = "")
    {
        using var d = new TextPromptDialog(title, prompt, initial);
        return d.ShowDialog(owner) == DialogResult.OK && d.Value.Length > 0 ? d.Value : null;
    }
}
