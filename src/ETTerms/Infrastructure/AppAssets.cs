using System.Drawing;
using System.Reflection;

namespace ETTerms.Infrastructure;

/// <summary>
/// 存取內嵌進組件的圖示（見 ETTerms.csproj 的 EmbeddedResource）。
/// Manifest 名稱規則：RootNamespace.資料夾.檔名 → ETTerms.Assets.xxx。
/// </summary>
public static class AppAssets
{
    private const string IconResource = "ETTerms.Assets.Choco_256x256.ico";

    /// <summary>視窗 / 工作列圖示。找不到資源時回傳 null。</summary>
    public static Icon? AppIcon() => LoadIcon(IconResource);

    /// <summary>Choco 圖示，指定尺寸（About 用大圖）。</summary>
    public static Icon? AppIcon(int size)
    {
        var icon = LoadIcon(IconResource);
        if (icon == null) return null;
        var sized = new Icon(icon, new Size(size, size));
        icon.Dispose();
        return sized;
    }

    private static Icon? LoadIcon(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        return s == null ? null : new Icon(s);
    }
}
