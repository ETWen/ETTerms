using System.Diagnostics;

namespace ETTerms.Infrastructure;

/// <summary>
/// 應用程式 Logger（Phase 1 精簡版，移植自 MyTeraTerm.AppLogger）。
/// - 永遠寫入檔案：%LocalAppData%\ETTerms\logs\ETTerms_yyyyMMdd.log
/// - 同時輸出到 Debug（VS 輸出視窗）
/// 後續 Phase 可再補 Console 即時視窗 / log 輪替清理。
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static string _logFilePath = "";
    private static bool _initialized;

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ETTerms", "logs");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir, $"ETTerms_{DateTime.Now:yyyyMMdd}.log");
                _initialized = true;

                Info(new string('=', 60));
                Info("=== ETTerms Started ===");
                Info($"Time:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Info($"Version: {GetVersion()}");
                Info($"User:    {Environment.UserName}@{Environment.MachineName}");
                Info($"OS:      {Environment.OSVersion}");
                Info($".NET:    {Environment.Version}");
                Info(new string('=', 60));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] init failed: {ex.Message}");
            }
        }
    }

    public static void LogDebug(string message) => Write("DEBUG", message);
    public static void Info(string message) => Write("INFO", message);
    public static void LogInfo(string message) => Write("INFO", message);
    public static void LogWarning(string message) => Write("WARN", message);

    public static void LogError(string message, Exception? ex = null)
    {
        Write("ERROR", ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");
    }

    public static string GetLogFilePath() => _logFilePath;

    public static void OpenLogsFolder()
    {
        try
        {
            string dir = Path.GetDirectoryName(_logFilePath) ?? "";
            if (Directory.Exists(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogError("Failed to open logs folder", ex);
        }
    }

    public static void LogApplicationClose()
    {
        Info("=== ETTerms Closing ===");
        Info("");
    }

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} [{level,-5}] {message}";
        Debug.WriteLine(line);

        if (!_initialized) return;
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // log 失敗不可拖垮 App
            }
        }
    }

    private static string GetVersion()
    {
        try
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "Unknown" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch { return "Unknown"; }
    }
}
