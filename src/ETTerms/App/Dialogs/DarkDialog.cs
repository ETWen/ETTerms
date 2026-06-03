using System.Drawing;
using System.Windows.Forms;
using ETTerms.Infrastructure;

namespace ETTerms.App.Dialogs;

/// <summary>深色對話框基底：統一配色、深色標題列、固定大小、Enter/Esc。</summary>
public abstract class DarkDialog : Form
{
    protected DarkDialog()
    {
        BackColor = Theme.SidebarBack;
        ForeColor = Theme.Text;
        Font = Theme.UiFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyDarkTitleBar(this);
    }

    protected Button MakeButton(string text, DialogResult result, bool accent = false)
    {
        var b = new Button
        {
            Text = text,
            DialogResult = result,
            Size = new Size(88, 30),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text,
            BackColor = accent ? Theme.Accent : Theme.TabBack,
            Font = Theme.UiFont,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Theme.Border;
        b.FlatAppearance.MouseOverBackColor = accent ? Theme.AccentDim : Theme.Hover;
        return b;
    }

    protected TextBox MakeTextBox(string initial = "")
    {
        return new TextBox
        {
            Text = initial,
            BackColor = Theme.WorkspaceBack,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.UiFont
        };
    }

    protected Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.TextDim,
        Font = Theme.UiFont
    };
}
