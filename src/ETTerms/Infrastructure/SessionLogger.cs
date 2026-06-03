using System.Text;
using System.Text.RegularExpressions;

namespace ETTerms.Infrastructure;

/// <summary>
/// 單一分頁的終端機輸出側錄器。按下 Log 按鈕時建立，再按一次（或關閉分頁）時 Dispose。
///
/// - 檔案位置：&lt;exe 路徑&gt;\logs\
/// - 檔名格式：<c>[連線識別]_yyyyMMdd_HHmmss.log</c>，例如 <c>[PowerShell]_20260603_212400.log</c>
/// - 內容格式：每行 <c>[yyyy-MM-dd HH:mm:ss.fff] 文字</c>
///
/// 收到的位元組會去除 ANSI/VT escape 序列後依換行切行，每完成一行才寫出並補上收到當下的時間戳，
/// 未滿一行的殘段先留在緩衝區，待後續資料或 Dispose 時補寫。
/// </summary>
public sealed class SessionLogger : IDisposable
{
    // 去除 CSI / OSC / 單字元 escape 等 ANSI 控制序列
    private static readonly Regex AnsiRegex = new(
        @"\x1B\][^\x07\x1B]*(\x07|\x1B\\)|\x1B[@-Z\\-_]|\x1B\[[0-?]*[ -/]*[@-~]",
        RegexOptions.Compiled);

    private readonly object _lock = new();
    private readonly StreamWriter _writer;
    private readonly StringBuilder _pending = new();
    private bool _disposed;

    /// <summary>實際寫入的完整檔案路徑。</summary>
    public string FilePath { get; }

    private SessionLogger(string filePath)
    {
        FilePath = filePath;
        _writer = new StreamWriter(filePath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// <summary>
    /// 為某個連線建立側錄器。<paramref name="logName"/> 例如 "PowerShell" / "COM17" / "etwen@192.168.1.50"。
    /// </summary>
    public static SessionLogger Start(string logName)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(dir);

        string safe = Sanitize(logName);
        string fileName = $"[{safe}]_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        string path = Path.Combine(dir, fileName);
        return new SessionLogger(path);
    }

    /// <summary>餵入終端機收到的原始位元組（UTF-8 解碼後側錄）。</summary>
    public void Write(byte[] data)
    {
        if (_disposed || data.Length == 0) return;
        string text = Encoding.UTF8.GetString(data);
        string clean = AnsiRegex.Replace(text, "").Replace("\r", "");
        if (clean.Length == 0) return;

        lock (_lock)
        {
            if (_disposed) return;
            string stamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ";
            int start = 0;
            for (int i = 0; i < clean.Length; i++)
            {
                if (clean[i] != '\n') continue;
                _pending.Append(clean, start, i - start);
                _writer.WriteLine(stamp + _pending);
                _pending.Clear();
                start = i + 1;
            }
            if (start < clean.Length)
                _pending.Append(clean, start, clean.Length - start);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_pending.Length > 0)
                    _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {_pending}");
                _writer.Dispose();
            }
            catch { /* 收尾失敗不可拖垮 App */ }
        }
    }

    /// <summary>把連線識別字串清成合法檔名（移除 \ / : * ? " &lt; &gt; | 等）。</summary>
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "session";
        var sb = new StringBuilder(name.Length);
        var invalid = Path.GetInvalidFileNameChars();
        foreach (char c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }
}
