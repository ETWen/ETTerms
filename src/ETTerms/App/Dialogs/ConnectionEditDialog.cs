using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using ETTerms.Connections;

namespace ETTerms.App.Dialogs;

/// <summary>
/// 連線新增 / 編輯對話框：依類型切換 SSH（host/port/user/auth/key/password）
/// 與 Serial（COM/baud/databits/parity/stopbits/handshake）表單。
/// 輸出 <see cref="Result"/>（Connection）與 <see cref="Password"/>（空 = 不變更）。
/// </summary>
public sealed class ConnectionEditDialog : DarkDialog
{
    private readonly Connection? _existing;

    private readonly TextBox _name;
    private readonly ComboBox _type;

    // SSH（於 BuildSshPanel 指派）
    private readonly Panel _sshPanel;
    private TextBox _host = null!, _user = null!, _keyPath = null!, _password = null!;
    private NumericUpDown _port = null!;
    private ComboBox _auth = null!;

    // Serial（於 BuildSerialPanel 指派）
    private readonly Panel _serialPanel;
    private ComboBox _com = null!, _baud = null!, _parity = null!, _stopBits = null!, _handshake = null!;
    private NumericUpDown _dataBits = null!;

    public string Password => _password.Text;
    public Connection Result { get; private set; } = new();

    public ConnectionEditDialog(string title, Connection? existing = null)
    {
        _existing = existing;
        Text = title;
        ClientSize = new Size(420, 452);

        var lblName = MakeLabel("Name"); lblName.Location = new Point(16, 12);
        _name = MakeTextBox(existing?.Name ?? ""); _name.SetBounds(16, 34, 388, 26);

        var lblType = MakeLabel("Type"); lblType.Location = new Point(16, 66);
        _type = MakeCombo(false); _type.Items.AddRange(new object[] { "SSH", "Serial" });
        _type.SetBounds(16, 88, 388, 26);
        _type.SelectedIndexChanged += (_, _) => ToggleType();

        _sshPanel = new Panel { Bounds = new Rectangle(12, 122, 404, 300), BackColor = Theme.SidebarBack };
        _serialPanel = new Panel { Bounds = new Rectangle(12, 122, 404, 300), BackColor = Theme.SidebarBack };
        BuildSshPanel(existing?.Ssh);
        BuildSerialPanel(existing?.Serial);

        var ok = MakeButton("OK", DialogResult.OK, accent: true); ok.Location = new Point(224, 414);
        var cancel = MakeButton("Cancel", DialogResult.Cancel); cancel.Location = new Point(316, 414);

        Controls.AddRange(new Control[] { lblName, _name, lblType, _type, _sshPanel, _serialPanel, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;

        _type.SelectedIndex = (existing?.Type ?? ConnectionType.Ssh) == ConnectionType.Ssh ? 0 : 1;
        ToggleType();
    }

    private void ToggleType()
    {
        bool ssh = _type.SelectedIndex == 0;
        _sshPanel.Visible = ssh;
        _serialPanel.Visible = !ssh;
    }

    private void BuildSshPanel(SshSettings? s)
    {
        var lblHost = MakeLabel("Host"); lblHost.Location = new Point(4, 4);
        _host = MakeTextBox(s?.Host ?? ""); _host.SetBounds(4, 26, 250, 26);
        var lblPort = MakeLabel("Port"); lblPort.Location = new Point(264, 4);
        _port = MakeNumeric(1, 65535, s?.Port ?? 22); _port.SetBounds(264, 26, 136, 26);

        var lblUser = MakeLabel("Username"); lblUser.Location = new Point(4, 58);
        _user = MakeTextBox(s?.Username ?? ""); _user.SetBounds(4, 80, 396, 26);

        var lblAuth = MakeLabel("Auth Method"); lblAuth.Location = new Point(4, 112);
        _auth = MakeCombo(false);
        _auth.Items.AddRange(new object[] { "Password", "Private Key", "Keyboard Interactive" });
        _auth.SelectedIndex = (int)(s?.AuthMethod ?? SshAuthMethod.Password);
        _auth.SetBounds(4, 134, 396, 26);

        var lblKey = MakeLabel("Private Key Path (optional)"); lblKey.Location = new Point(4, 166);
        _keyPath = MakeTextBox(s?.PrivateKeyPath ?? ""); _keyPath.SetBounds(4, 188, 396, 26);

        var lblPwd = MakeLabel("Password / Passphrase (blank = unchanged)"); lblPwd.Location = new Point(4, 220);
        _password = MakeTextBox(""); _password.UseSystemPasswordChar = true; _password.SetBounds(4, 242, 396, 26);

        _sshPanel.Controls.AddRange(new Control[]
        {
            lblHost, _host, lblPort, _port, lblUser, _user,
            lblAuth, _auth, lblKey, _keyPath, lblPwd, _password
        });
    }

    private void BuildSerialPanel(SerialSettings? s)
    {
        var lblCom = MakeLabel("COM Port"); lblCom.Location = new Point(4, 4);
        _com = MakeCombo(true); _com.Items.AddRange(SerialPort.GetPortNames());
        _com.Text = s?.PortName ?? (_com.Items.Count > 0 ? _com.Items[0]!.ToString()! : "COM1");
        _com.SetBounds(4, 26, 196, 26);

        var lblBaud = MakeLabel("Baud Rate"); lblBaud.Location = new Point(208, 4);
        _baud = MakeCombo(true);
        _baud.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 });
        _baud.Text = (s?.BaudRate ?? 115200).ToString();
        _baud.SetBounds(208, 26, 192, 26);

        var lblData = MakeLabel("Data Bits"); lblData.Location = new Point(4, 58);
        _dataBits = MakeNumeric(5, 8, s?.DataBits ?? 8); _dataBits.SetBounds(4, 80, 196, 26);

        var lblParity = MakeLabel("Parity"); lblParity.Location = new Point(208, 58);
        _parity = MakeEnumCombo<Parity>(s?.Parity ?? Parity.None); _parity.SetBounds(208, 80, 192, 26);

        var lblStop = MakeLabel("Stop Bits"); lblStop.Location = new Point(4, 112);
        _stopBits = MakeEnumCombo<StopBits>(s?.StopBits ?? StopBits.One); _stopBits.SetBounds(4, 134, 196, 26);

        var lblHand = MakeLabel("Handshake"); lblHand.Location = new Point(208, 112);
        _handshake = MakeEnumCombo<Handshake>(s?.Handshake ?? Handshake.None); _handshake.SetBounds(208, 134, 192, 26);

        _serialPanel.Controls.AddRange(new Control[]
        {
            lblCom, _com, lblBaud, _baud, lblData, _dataBits,
            lblParity, _parity, lblStop, _stopBits, lblHand, _handshake
        });
    }

    private Connection BuildResult()
    {
        var c = new Connection
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = _name.Text.Trim(),
            SortOrder = _existing?.SortOrder ?? 0,
            GroupName = _existing?.GroupName,
            LastUsedUtc = _existing?.LastUsedUtc ?? DateTime.UtcNow
        };
        if (_type.SelectedIndex == 0)
        {
            c.Type = ConnectionType.Ssh;
            c.Ssh = new SshSettings
            {
                Host = _host.Text.Trim(),
                Port = (int)_port.Value,
                Username = _user.Text.Trim(),
                AuthMethod = (SshAuthMethod)_auth.SelectedIndex,
                PrivateKeyPath = string.IsNullOrWhiteSpace(_keyPath.Text) ? null : _keyPath.Text.Trim()
            };
        }
        else
        {
            c.Type = ConnectionType.Serial;
            c.Serial = new SerialSettings
            {
                PortName = _com.Text.Trim(),
                BaudRate = int.TryParse(_baud.Text, out int b) ? b : 115200,
                DataBits = (int)_dataBits.Value,
                Parity = Enum.TryParse<Parity>(_parity.Text, out var p) ? p : Parity.None,
                StopBits = Enum.TryParse<StopBits>(_stopBits.Text, out var sb) ? sb : StopBits.One,
                Handshake = Enum.TryParse<Handshake>(_handshake.Text, out var h) ? h : Handshake.None
            };
        }
        return c;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            if (_name.Text.Trim().Length == 0) { e.Cancel = true; _name.Focus(); return; }
            Result = BuildResult();
        }
        base.OnFormClosing(e);
    }

    // ── 控制項工廠 ───────────────────────────────────────────
    private ComboBox MakeCombo(bool editable) => new()
    {
        DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
        BackColor = Theme.WorkspaceBack,
        ForeColor = Theme.Text,
        FlatStyle = FlatStyle.Flat,
        Font = Theme.UiFont
    };

    private ComboBox MakeEnumCombo<TEnum>(TEnum selected) where TEnum : struct, Enum
    {
        var cb = MakeCombo(false);
        cb.Items.AddRange(Enum.GetNames<TEnum>());
        cb.SelectedItem = selected.ToString();
        return cb;
    }

    private static NumericUpDown MakeNumeric(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        BackColor = Theme.WorkspaceBack,
        ForeColor = Theme.Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = Theme.UiFont
    };
}
