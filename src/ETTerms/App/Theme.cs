using System.Drawing;

namespace ETTerms.App;

/// <summary>
/// 全域深色配色（KKTerm 風格）。Phase 1 先集中在這裡，
/// 後續 Phase 5 的 TerminalProfile / Settings 可覆寫。
/// </summary>
public static class Theme
{
    public static readonly Color RailBack      = Color.FromArgb(24, 24, 28);
    public static readonly Color SidebarBack   = Color.FromArgb(32, 32, 38);
    public static readonly Color WorkspaceBack = Color.FromArgb(40, 40, 46);
    public static readonly Color TabBack       = Color.FromArgb(48, 48, 56);
    public static readonly Color TabActiveBack = Color.FromArgb(40, 40, 46);

    public static readonly Color Accent        = Color.FromArgb(138, 99, 210);  // KKTerm 紫
    public static readonly Color AccentDim     = Color.FromArgb(86, 64, 130);

    public static readonly Color Text          = Color.FromArgb(222, 222, 226);
    public static readonly Color TextDim       = Color.FromArgb(150, 150, 158);
    public static readonly Color Border        = Color.FromArgb(58, 58, 66);
    public static readonly Color Hover         = Color.FromArgb(60, 60, 70);

    public static readonly Color SshColor      = Color.FromArgb(120, 180, 255);
    public static readonly Color SerialColor   = Color.FromArgb(140, 210, 140);

    public static readonly Font  UiFont        = new("Segoe UI", 9.5f);
    public static readonly Font  UiFontBold    = new("Segoe UI", 9.5f, FontStyle.Bold);
}
