using System.Drawing;
using System.Windows.Forms;
using ETTerms.App;
using ETTerms.Infrastructure;
using ETTerms.Scripting;
using ETTerms.Terminal;

namespace ETTerms.Sessions;

/// <summary>
/// 一個連線分頁的內容：頂部腳本列（狀態 + 載入執行 / 停止）+ 自繪 VT100 <see cref="TerminalView"/>
/// + 綁定的 <see cref="ISessionChannel"/>。每個分頁各自對自己的 channel 跑 TTL 腳本。
/// channel 在 handle 建立後才 Open，確保 DataReceived 能安全 Invoke 回 UI thread。
/// </summary>
public sealed class SessionPage : UserControl
{
    private const int MaxStatus = 40;   // 狀態列指令最大字數，超過以 ... 顯示

    private readonly ISessionChannel _channel;
    private readonly string _title;
    private readonly TerminalView _term;
    private readonly ScriptRunner _runner = new();
    private readonly Label _status;
    private readonly Button _run, _stop;
    private bool _started;

    /// <summary>底層連線通道。</summary>
    public ISessionChannel Channel => _channel;

    /// <summary>此分頁是否為 Serial 連線。</summary>
    public bool IsSerial => _channel is SerialChannel;

    /// <summary>目前是否正在跑腳本。</summary>
    public bool IsScriptRunning => _runner.IsRunning;

    /// <summary>所屬 Group（0=無, 1/2/3）。</summary>
    public int Group { get; set; }

    /// <summary>Group 內的編號標籤，如 "Group1-A"。外部設定。</summary>
    public string GroupLabel { get; set; } = "";

    /// <summary>連線標題（COM名稱）。</summary>
    public string Title => _title;

    /// <summary>外部觸發腳本執行（供 Run All 使用）。</summary>
    public async Task RunScriptAsync(string content, string fileName)
    {
        if (_runner.IsRunning) return;
        SetRunning(true);
        await _runner.RunAsync(content, fileName, _channel);
    }

    /// <summary>以 Group 模式執行腳本（支援 waitall/sendlnall/sendlngroup 同步）。</summary>
    public async Task RunGroupScriptAsync(string content, string fileName, GroupSyncContext sync, string memberLabel)
    {
        if (_runner.IsRunning) return;
        SetRunning(true);
        await _runner.RunGroupAsync(content, fileName, _channel, sync, memberLabel);
    }

    /// <summary>同步開啟失敗（主要是 Serial 連不上 / 被占用）時觸發，附帶訊息。</summary>
    public event Action<string>? ConnectFailed;

    public SessionPage(ISessionChannel channel, string title)
    {
        _channel = channel;
        _title = title;
        Dock = DockStyle.Fill;

        // ── 頂部腳本列 ──
        var bar = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Theme.RailBack };
        _status = new Label
        {
            Dock = DockStyle.Fill, Text = "Idle",
            ForeColor = Theme.TextDim, Font = Theme.UiFont,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
        };
        _run = MakeBarButton("▶ Script", OnRunScript);
        _stop = MakeBarButton("■ Stop", (_, _) => _runner.Cancel());
        _stop.Enabled = false;
        bar.Controls.Add(_status);  // Fill 先加
        bar.Controls.Add(_run);     // Right
        bar.Controls.Add(_stop);    // Right

        _term = new TerminalView(new TerminalProfile()) { Dock = DockStyle.Fill };
        _term.SendData += data => _channel.Write(data);
        _term.Resized += (cols, rows) => _channel.Resize(cols, rows);

        Controls.Add(_term);  // Fill 先加
        Controls.Add(bar);    // Top

        _runner.StatusChanged += (_, line, cmd) => Ui(() => _status.Text = $"line {line}: {Trunc(cmd)}");
        _runner.Finished += (_, msg) => Ui(() => { _status.Text = msg; SetRunning(false); });
    }

    private async void OnRunScript(object? sender, EventArgs e)
    {
        string content, name;
        using (var dlg = new OpenFileDialog { Filter = "TTL scripts (*.ttl)|*.ttl|All files (*.*)|*.*" })
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            content = File.ReadAllText(dlg.FileName);
            name = Path.GetFileName(dlg.FileName);
        }
        if (ContainsGroupCommands(content))
        {
            MessageBox.Show(this,
                "Script contains 'waitall' or 'sendlnall' which require Group execution.\nPlease use Run Group buttons instead.",
                "Script", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        SetRunning(true);
        await _runner.RunAsync(content, name, _channel);
    }

    private static bool ContainsGroupCommands(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var t = line.Trim().ToLower();
            if (t.StartsWith("waitall") || t.StartsWith("sendlnall") || t.StartsWith("sendlngroup"))
                return true;
        }
        return false;
    }

    private void SetRunning(bool running)
    {
        _run.Enabled = !running;
        _stop.Enabled = running;
    }

    private static string Trunc(string s) => s.Length <= MaxStatus ? s : s.Substring(0, MaxStatus) + "...";

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private static Button MakeBarButton(string text, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text, Dock = DockStyle.Right, Width = 72, FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont, Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Theme.Border;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        b.Click += onClick;
        return b;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_started) return;
        _started = true;
        _channel.DataReceived += OnDataReceived;
        try
        {
            _channel.Open();
            SessionManager.Register(_channel);
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"Open channel failed: {_title}", ex);
            ConnectFailed?.Invoke(ex.Message);
        }
    }

    private void OnDataReceived(byte[] data)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(() => _term.Feed(data));
        else _term.Feed(data);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _started)
        {
            _runner.Cancel();
            _channel.DataReceived -= OnDataReceived;
            SessionManager.Unregister(_channel);
            _channel.Dispose();
        }
        base.Dispose(disposing);
    }
}
