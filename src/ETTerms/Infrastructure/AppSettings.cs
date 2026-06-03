using System.Drawing;
using System.Text.Json;

namespace ETTerms.Infrastructure;

/// <summary>
/// User settings persisted to %LocalAppData%\ETTerms\settings.json.
/// Loaded once at startup; saved on change.
/// </summary>
public sealed class AppSettings
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETTerms");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static AppSettings? _instance;
    public static AppSettings Instance => _instance ??= Load();

    // ── Terminal ──
    public string FontFamily { get; set; } = "Cascadia Mono";
    public float FontSize { get; set; } = 11f;
    public int ScrollbackLines { get; set; } = 5000;
    public string DefaultNewLine { get; set; } = "\\r\\n";
    public string ColorScheme { get; set; } = "Dark";

    // ── Shell ──
    public string ShellType { get; set; } = "PowerShell";       // PowerShell, Bash, Cmd
    public string ShellStartupDir { get; set; } = "";

    // ── Window ──
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowW { get; set; } = 1280;
    public int WindowH { get; set; } = 800;
    public bool WindowMaximized { get; set; }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public void ApplyWindowPosition(System.Windows.Forms.Form form)
    {
        if (WindowMaximized)
        {
            form.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            return;
        }
        if (WindowX >= 0 && WindowY >= 0)
        {
            form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            form.Location = new Point(WindowX, WindowY);
        }
        form.Size = new Size(WindowW, WindowH);
    }

    public void SaveWindowPosition(System.Windows.Forms.Form form)
    {
        WindowMaximized = form.WindowState == System.Windows.Forms.FormWindowState.Maximized;
        if (!WindowMaximized)
        {
            WindowX = form.Location.X;
            WindowY = form.Location.Y;
            WindowW = form.Size.Width;
            WindowH = form.Size.Height;
        }
        Save();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch { }
        return new();
    }
}
