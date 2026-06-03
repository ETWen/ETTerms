namespace ETTerms.Sessions;

/// <summary>
/// 連線通道抽象：TerminalView / ScriptEngine 都只認得這個介面，
/// 因此 SSH 與 Serial 對上層一致（同一套腳本可驅動兩種連線）。
/// </summary>
public interface ISessionChannel : IDisposable
{
    /// <summary>收到遠端資料（背景執行緒觸發，訂閱者需自行 Invoke 回 UI thread）。</summary>
    event Action<byte[]>? DataReceived;

    /// <summary>
    /// 用於 log 檔名的連線識別字串：
    /// Shell → "PowerShell"；Serial → "COM17"；SSH → "etwen@192.168.1.50"。
    /// </summary>
    string LogName { get; }

    /// <summary>建立連線；失敗丟例外。</summary>
    void Open();

    /// <summary>送出位元組到遠端。</summary>
    void Write(byte[] data);

    /// <summary>通知遠端終端機大小（PTY size）；Serial 為 no-op。</summary>
    void Resize(int cols, int rows);

    /// <summary>關閉連線並釋放底層資源。</summary>
    void Close();
}
