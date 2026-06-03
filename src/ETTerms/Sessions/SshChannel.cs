using System.Reflection;
using System.Text;
using ETTerms.Connections;
using ETTerms.Infrastructure;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace ETTerms.Sessions;

/// <summary>
/// SSH 連線通道：SSH.NET SshClient + ShellStream，實作 <see cref="ISessionChannel"/>。
/// 支援 password / private key（passphrase 走 CredentialVault）/ keyboard-interactive；
/// host key 指紋 TOFU（首次記錄、之後比對、不符中止）。連線在背景執行緒進行避免凍結 UI。
/// </summary>
public sealed class SshChannel : ISessionChannel
{
    private readonly SshSettings _ssh;
    private readonly string? _secret;
    private SshClient? _client;
    private ShellStream? _shell;
    private int _cols = 80, _rows = 24;
    private bool _closed;

    public event Action<byte[]>? DataReceived;

    public string LogName => $"{_ssh.Username}@{_ssh.Host}";

    public SshChannel(Connection conn)
    {
        _ssh = conn.Ssh ?? new SshSettings();
        _secret = CredentialVault.Get(conn.CredentialKey);
    }

    public void Open() => Task.Run(Connect);

    private void Connect()
    {
        try
        {
            var ci = BuildConnectionInfo();
            _client = new SshClient(ci);
            _client.HostKeyReceived += OnHostKey;
            _client.Connect();
            _shell = _client.CreateShellStream("xterm-256color", (uint)_cols, (uint)_rows, 0, 0, 4096);
            _shell.DataReceived += (_, e) => DataReceived?.Invoke(e.Data);
            AppLogger.Info($"SSH connected: {_ssh.Username}@{_ssh.Host}:{_ssh.Port}");
        }
        catch (Exception ex)
        {
            Emit($"\r\n[SSH 連線失敗] {ex.Message}\r\n");
            AppLogger.LogError($"SSH connect failed {_ssh.Host}", ex);
        }
    }

    private ConnectionInfo BuildConnectionInfo()
    {
        AuthenticationMethod method = _ssh.AuthMethod switch
        {
            SshAuthMethod.PrivateKey => new PrivateKeyAuthenticationMethod(_ssh.Username, LoadKey()),
            SshAuthMethod.KeyboardInteractive => KeyboardInteractive(),
            _ => new PasswordAuthenticationMethod(_ssh.Username, _secret ?? "")
        };
        return new ConnectionInfo(_ssh.Host, _ssh.Port, _ssh.Username, method);
    }

    private PrivateKeyFile LoadKey() => string.IsNullOrEmpty(_secret)
        ? new PrivateKeyFile(_ssh.PrivateKeyPath!)
        : new PrivateKeyFile(_ssh.PrivateKeyPath!, _secret);

    private KeyboardInteractiveAuthenticationMethod KeyboardInteractive()
    {
        var ki = new KeyboardInteractiveAuthenticationMethod(_ssh.Username);
        ki.AuthenticationPrompt += (_, e) =>
        {
            foreach (AuthenticationPrompt p in e.Prompts) p.Response = _secret ?? "";
        };
        return ki;
    }

    private void OnHostKey(object? sender, HostKeyEventArgs e)
    {
        string key = $"{_ssh.Host}:{_ssh.Port}";
        string fp = e.FingerPrintSHA256;
        string? known = HostKeyStore.Get(key);
        if (known == null)
        {
            HostKeyStore.Set(key, fp);
            Emit($"\r\n[host key TOFU] 首次連線，已記錄 {key}\r\nSHA256:{fp}\r\n");
            e.CanTrust = true;
        }
        else if (known == fp)
        {
            e.CanTrust = true;
        }
        else
        {
            Emit($"\r\n[警告] {key} host key 不符，可能遭中間人攻擊！連線中止。\r\n預期 SHA256:{known}\r\n實際 SHA256:{fp}\r\n");
            e.CanTrust = false;
        }
    }

    public void Write(byte[] data)
    {
        if (_shell == null) return;
        _shell.Write(data, 0, data.Length);
        _shell.Flush();
    }

    public void Resize(int cols, int rows)
    {
        _cols = cols; _rows = rows;
        if (_shell == null) return;
        // SSH.NET ShellStream 未公開 resize；反射呼叫底層 channel 的 SendWindowChangeRequest
        try
        {
            var channel = _shell.GetType()
                .GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_shell);
            channel?.GetType().GetMethod("SendWindowChangeRequest", BindingFlags.Public | BindingFlags.Instance)?
                .Invoke(channel, new object[] { (uint)cols, (uint)rows, 0u, 0u });
        }
        catch (Exception ex) { AppLogger.LogWarning($"SSH resize failed: {ex.Message}"); }
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        try { _shell?.Dispose(); } catch { }
        try { _client?.Disconnect(); _client?.Dispose(); } catch { }
    }

    public void Dispose() => Close();

    private void Emit(string text) => DataReceived?.Invoke(Encoding.UTF8.GetBytes(text));
}
