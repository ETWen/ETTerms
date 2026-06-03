using ETTerms.Infrastructure;

namespace ETTerms.Terminal;

/// <summary>終端機顯示偏好，從 AppSettings 載入。</summary>
public sealed class TerminalProfile
{
    public string FontFamily { get; set; } = AppSettings.Instance.FontFamily;
    public float FontSize { get; set; } = AppSettings.Instance.FontSize;
    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
    public int ScrollbackLines { get; set; } = AppSettings.Instance.ScrollbackLines;
}
