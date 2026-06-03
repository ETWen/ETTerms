using System.IO.Ports;

namespace ETTerms.Connections;

public enum ConnectionType { Ssh = 0, Serial = 1, Shell = 2 }

public enum SshAuthMethod { Password = 0, PrivateKey = 1, KeyboardInteractive = 2 }

/// <summary>SSH 連線設定（Type == Ssh 時有效）。passphrase / password 走 CredentialVault。</summary>
public sealed class SshSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public SshAuthMethod AuthMethod { get; set; } = SshAuthMethod.Password;
    public string? PrivateKeyPath { get; set; }
}

/// <summary>Serial 連線設定（Type == Serial 時有效）。</summary>
public sealed class SerialSettings
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public string NewLine { get; set; } = "\r\n";
}

/// <summary>Local shell settings (Type == Shell).</summary>
public sealed class ShellSettings
{
    public string ShellType { get; set; } = "PowerShell";  // "PowerShell", "Bash", "Cmd"
    public string StartupDirectory { get; set; } = "";
}

/// <summary>連線 metadata。明碼密碼絕不存這裡，只存指向 Credential Manager 的 <see cref="CredentialKey"/>。</summary>
public sealed class Connection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ConnectionType Type { get; set; }
    public int SortOrder { get; set; }
    public string? GroupName { get; set; }          // 資料夾路徑（'/'-join）
    public DateTime LastUsedUtc { get; set; }

    public SshSettings? Ssh { get; set; }
    public SerialSettings? Serial { get; set; }
    public ShellSettings? Shell { get; set; }

    public string CredentialKey => $"ETTerms/{Id}";

    public bool IsSsh => Type == ConnectionType.Ssh;

    /// <summary>sidebar 顯示用摘要。</summary>
    public string Detail => IsSsh
        ? $"{Ssh?.Host}:{Ssh?.Port}"
        : $"{Serial?.PortName} @ {Serial?.BaudRate}";
}
