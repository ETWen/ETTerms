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

        // 用 Dpi 自動縮放：尺寸縮放比例 = 螢幕 DPI 比例，與點數字型的實際渲染比例一致，
        // 避免「字放大了、容器沒放大」造成的高 DPI 文字/按鈕被裁切。
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
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
