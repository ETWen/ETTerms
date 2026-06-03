using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ETTerms.Infrastructure;

/// <summary>Windows 原生外觀輔助（深色標題列）。</summary>
public static class NativeTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>把視窗標題列改成深色（Windows 10 1809+ / 11）。</summary>
    public static void ApplyDarkTitleBar(IWin32Window window)
    {
        try
        {
            int useDark = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20（19 為較舊 build 的編號）
            if (DwmSetWindowAttribute(window.Handle, 20, ref useDark, sizeof(int)) != 0)
                DwmSetWindowAttribute(window.Handle, 19, ref useDark, sizeof(int));
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning($"Dark title bar not applied: {ex.Message}");
        }
    }
}
