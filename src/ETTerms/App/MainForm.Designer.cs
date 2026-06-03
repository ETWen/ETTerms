#nullable enable
using System.Drawing;

namespace ETTerms.App;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(720, 480);
        BackColor = Theme.WorkspaceBack;
        ForeColor = Theme.Text;
        Font = Theme.UiFont;
        Text = "ETTerms";
        StartPosition = FormStartPosition.CenterScreen;

        ResumeLayout(false);
    }
}
