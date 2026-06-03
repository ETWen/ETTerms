using System.Windows.Forms;
using ETTerms.App.Workspace;
using ETTerms.Infrastructure;

namespace ETTerms.App;

/// <summary>
/// 主視窗：三欄佈局 = ActivityRail（左圖示列）+ ConnectionSidebar（連線清單）
/// + WorkspaceTabs（分頁工作區）。Phase 1 只做外殼，連線為佔位。
/// </summary>
public partial class MainForm : Form
{
    private readonly ActivityRail _rail = new();
    private readonly ConnectionSidebar _sidebar = new();
    private readonly WorkspaceView _workspace = new();
    private readonly SettingsView _settings = new();
    private readonly AboutView _about = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    public MainForm()
    {
        InitializeComponent();
        BuildLayout();
        WireEvents();
        AppSettings.Instance.ApplyWindowPosition(this);
        AppLogger.Info("MainForm initialized");
    }

    private void BuildLayout()
    {
        Controls.Add(_workspace);   // Fill
        Controls.Add(_settings);    // Fill (hidden)
        Controls.Add(_about);       // Fill (hidden)
        Controls.Add(_sidebar);     // Left (內側)
        Controls.Add(_rail);        // Left (最外側)

        _settings.Visible = false;
        _about.Visible = false;

        _statusLabel.Text = "Ready";
        _statusLabel.ForeColor = Theme.TextDim;
        _status.Items.Add(_statusLabel);
        _status.BackColor = Theme.RailBack;
        _status.SizingGrip = false;
        Controls.Add(_status);
    }

    private void WireEvents()
    {
        _rail.ViewSelected += (_, view) =>
        {
            _statusLabel.Text = $"View: {view}";
            _sidebar.Visible = view == ActivityRail.RailView.Terminal;
            _workspace.Visible = view == ActivityRail.RailView.Terminal;
            _settings.Visible = view == ActivityRail.RailView.Settings;
            _about.Visible = view == ActivityRail.RailView.About;
            AppLogger.LogInfo($"View selected: {view}");
        };

        _sidebar.ConnectionActivated += (_, conn) =>
        {
            _workspace.OpenConnection(conn);
            _statusLabel.Text = $"Opened: {conn.Name}";
            AppLogger.LogInfo($"Open session: {conn.Name} ({(conn.IsSsh ? "SSH" : "Serial")})");
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyDarkTitleBar(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        AppSettings.Instance.SaveWindowPosition(this);
        AppLogger.LogApplicationClose();
        base.OnFormClosed(e);
    }
}
