namespace ETTerms.Sessions;

/// <summary>
/// Host key 指紋的 trust-on-first-use 記錄：%LocalAppData%\ETTerms\known_hosts.txt
/// 每行 "host:port SHA256base64"。
/// </summary>
public static class HostKeyStore
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETTerms", "known_hosts.txt");

    private static readonly object _lock = new();

    /// <summary>取回已信任指紋；無則 null。</summary>
    public static string? Get(string hostKey)
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return null;
            foreach (var line in File.ReadAllLines(_path))
            {
                var sp = line.Split(' ', 2);
                if (sp.Length == 2 && sp[0] == hostKey) return sp[1];
            }
            return null;
        }
    }

    /// <summary>記錄新信任的指紋。</summary>
    public static void Set(string hostKey, string fingerprint)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, $"{hostKey} {fingerprint}{Environment.NewLine}");
        }
    }
}
