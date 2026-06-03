namespace ETTerms.Sessions;

/// <summary>
/// 全域 session 登錄表 + Serial COM port 互斥管理。
/// 一個 COM port 同時只能被一個 session 開啟。
/// </summary>
public static class SessionManager
{
    private static readonly object _lock = new();
    private static readonly HashSet<string> _busyPorts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ISessionChannel> _active = new();

    /// <summary>目前 active 的 session 快照。</summary>
    public static IReadOnlyList<ISessionChannel> Active
    {
        get { lock (_lock) return _active.ToArray(); }
    }

    /// <summary>嘗試占用 COM port；已被占用回 false。</summary>
    public static bool TryReservePort(string portName)
    {
        lock (_lock) return _busyPorts.Add(portName);
    }

    public static void ReleasePort(string portName)
    {
        lock (_lock) _busyPorts.Remove(portName);
    }

    public static void Register(ISessionChannel channel)
    {
        lock (_lock) _active.Add(channel);
    }

    public static void Unregister(ISessionChannel channel)
    {
        lock (_lock) _active.Remove(channel);
    }
}
