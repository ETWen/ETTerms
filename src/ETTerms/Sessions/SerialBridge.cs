namespace ETTerms.Sessions;

/// <summary>
/// 一個可被 MCP 橋接的 serial session 端點：提供寫入（含 [AI] echo 回 GUI）與 RX 轉發。
/// 由 <see cref="SessionPage"/> 建立並註冊到 <see cref="SerialBridge"/>。
/// </summary>
public sealed class SerialBridgeEndpoint
{
    private readonly Action<string, bool> _write;

    public string Name { get; }
    public int BaudRate { get; }

    /// <summary>實體 port 收到的資料（背景緒觸發），由 pipe server 廣播給 MCP client。</summary>
    public event Action<byte[]>? Rx;

    public SerialBridgeEndpoint(string name, int baudRate, Action<string, bool> write)
    {
        Name = name;
        BaudRate = baudRate;
        _write = write;
    }

    public void Write(string text, bool appendNewline) => _write(text, appendNewline);

    public void FeedRx(byte[] data) => Rx?.Invoke(data);
}

/// <summary>
/// GUI 目前開著、可供 MCP 橋接的 serial session 登錄表。port 永遠由 GUI 持有，
/// MCP（ETTerms.SerialMcp）經 named pipe attach 到這裡的端點來收發。
/// </summary>
public static class SerialBridge
{
    private static readonly object _lock = new();
    private static readonly List<SerialBridgeEndpoint> _eps = new();

    public static void Register(SerialBridgeEndpoint ep) { lock (_lock) _eps.Add(ep); }
    public static void Unregister(SerialBridgeEndpoint ep) { lock (_lock) _eps.Remove(ep); }
    public static SerialBridgeEndpoint[] All { get { lock (_lock) return _eps.ToArray(); } }

    public static SerialBridgeEndpoint? Find(string name) =>
        All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
