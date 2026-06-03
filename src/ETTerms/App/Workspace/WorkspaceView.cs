using System.Drawing;
using System.Windows.Forms;
using ETTerms.Connections;
using ETTerms.Scripting;
using ETTerms.Sessions;

namespace ETTerms.App.Workspace;

/// <summary>
/// 工作區（MobaXterm 風格）：所有連線是頂部「分頁」，預設單一視圖（1×1）。
/// 點 Layout（1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3）會把已開好的分頁自動鋪進格子，
/// 每格一個 session（下方顯示標題）。無 per-pane 動作鈕，開分頁只能從側欄連線。
/// </summary>
public sealed class WorkspaceView : UserControl
{
    private sealed class Session
    {
        public required string Title;
        public required bool IsSsh;
        public required SessionPage Page;
        public Rectangle TabBounds;
        public Rectangle CloseRect;
    }

    private readonly FlowLayoutPanel _toolbar;
    private readonly Button _logAll;
    private readonly Panel _tabStrip;
    private readonly Panel _body;
    private readonly Label _empty;

    private readonly List<Session> _sessions = new();
    private Session? _active;
    private int _rows = 1, _cols = 1;
    private Session? _hoverTab;

    // 拖曳排序狀態
    private Session? _dragTab;     // 按住中的分頁（候選拖曳對象）
    private int _dragStartX;       // 按下時的滑鼠 X，用來判斷是否超過拖曳門檻
    private bool _dragging;        // 是否已進入拖曳
    private const int DragThreshold = 5;

    private const int StripH = 30;
    private const int TabW = 180;
    private const int CloseSz = 14;

    private static readonly (string label, int r, int c)[] Presets =
    { ("1×1", 1, 1), ("1×2", 1, 2), ("2×1", 2, 1), ("2×2", 2, 2), ("2×3", 2, 3), ("3×3", 3, 3) };

    public WorkspaceView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.WorkspaceBack;

        // 頂部工具列容器：左側為 Layout/Run 群組（Fill），右側為 Log All（Dock Right）
        var topBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Theme.RailBack };

        _toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Theme.RailBack,
            Padding = new Padding(8, 6, 8, 6), WrapContents = false
        };
        _toolbar.Controls.Add(new Label
        {
            Text = "Layout", AutoSize = true, ForeColor = Theme.TextDim,
            Font = Theme.UiFont, Margin = new Padding(0, 6, 8, 0)
        });
        foreach (var p in Presets) _toolbar.Controls.Add(MakePresetButton(p.label, p.r, p.c));

        // ── Run All / Run Group 按鈕 ──
        _toolbar.Controls.Add(MakeActionButton("▶ Run All", 80, 16, OnRunAllSerial));
        _toolbar.Controls.Add(MakeActionButton("▶ Group1", 90, 8, (_, _) => OnRunGroup(1)));
        _toolbar.Controls.Add(MakeActionButton("▶ Group2", 90, 4, (_, _) => OnRunGroup(2)));
        _toolbar.Controls.Add(MakeActionButton("▶ Group3", 90, 4, (_, _) => OnRunGroup(3)));

        // ── 右上角：Log All（一次開/關所有分頁側錄）──
        var rightBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, AutoSize = true, BackColor = Theme.RailBack,
            Padding = new Padding(0, 6, 8, 6), WrapContents = false
        };
        _logAll = MakeActionButton("⏺ Log All", 96, 0, OnToggleLogAll);
        rightBar.Controls.Add(_logAll);

        topBar.Controls.Add(_toolbar);  // Fill（先加 → 後 dock → 吃剩餘空間）
        topBar.Controls.Add(rightBar);  // Right（後加 → 先 dock → 佔右側）

        _tabStrip = new Panel { Dock = DockStyle.Top, Height = StripH, BackColor = Theme.RailBack };
        _tabStrip.Paint += OnStripPaint;
        _tabStrip.MouseDown += OnStripMouseDown;
        _tabStrip.MouseMove += OnStripMouseMove;
        _tabStrip.MouseUp += OnStripMouseUp;
        _tabStrip.MouseLeave += (_, _) => { if (!_dragging) { _hoverTab = null; _tabStrip.Invalidate(); } };
        SetDoubleBuffered(_tabStrip);

        _body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack };
        _empty = new Label
        {
            Dock = DockStyle.Fill,
            Text = "No sessions\nDouble-click a connection to open",
            ForeColor = Theme.TextDim, BackColor = Theme.WorkspaceBack,
            Font = Theme.UiFont, TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.Add(_body);        // Fill 先加
        Controls.Add(_tabStrip);    // Top
        Controls.Add(topBar);       // Top（最上）
        _body.Controls.Add(_empty);
    }

    /// <summary>從側欄開啟連線：新增一個分頁並設為 active。</summary>
    public void OpenConnection(Connection conn)
    {
        var page = BuildPage(conn);
        var s = new Session { Title = conn.Name, IsSsh = conn.IsSsh, Page = page };
        page.ConnectFailed += msg => OnConnectFailed(s, msg);
        _sessions.Add(s);
        _active = s;
        Relayout();
    }

    private void OnConnectFailed(Session s, string msg)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            MessageBox.Show(this, msg, "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            CloseSession(s);
        });
    }

    private static SessionPage BuildPage(Connection conn)
    {
        if (conn.Type == ConnectionType.Serial && conn.Serial != null)
            return new SessionPage(new SerialChannel(conn.Serial), conn.Name);
        if (conn.Type == ConnectionType.Shell && conn.Shell != null)
            return new SessionPage(new ShellChannel(conn.Shell), conn.Name);
        return new SessionPage(new SshChannel(conn), conn.Name);
    }

    private void SetLayout(int r, int c) { _rows = r; _cols = c; Relayout(); }

    private void CloseSession(Session s)
    {
        int idx = _sessions.IndexOf(s);
        s.Page.Parent = null;
        _sessions.Remove(s);
        s.Page.Dispose();
        if (_active == s) _active = _sessions.Count > 0 ? _sessions[Math.Min(idx, _sessions.Count - 1)] : null;
        RefreshGroupLabels();
        Relayout();
    }

    // ── Group 管理 ───────────────────────────────────────────
    private void SetSessionGroup(Session s, int group)
    {
        s.Page.Group = group;
        RefreshGroupLabels();
        Relayout();
    }

    private void RefreshGroupLabels()
    {
        for (int g = 1; g <= 3; g++)
        {
            char letter = 'A';
            foreach (var s in _sessions.Where(x => x.Page.Group == g))
                s.Page.GroupLabel = $"Group{g}-{letter++}";
        }
        foreach (var s in _sessions.Where(x => x.Page.Group == 0))
            s.Page.GroupLabel = "";
    }

    // ── 依目前 Layout 把分頁鋪進格子 ─────────────────────────
    private void Relayout()
    {
        foreach (var s in _sessions) s.Page.Parent = null;     // 先卸下（保留存活）
        _body.SuspendLayout();
        for (int i = _body.Controls.Count - 1; i >= 0; i--)
        {
            var c = _body.Controls[i];
            if (c != _empty) { _body.Controls.Remove(c); c.Dispose(); }
        }

        if (_sessions.Count == 0)
        {
            _empty.Visible = true;
            _body.ResumeLayout();
            _tabStrip.Invalidate();
            return;
        }
        _empty.Visible = false;
        _active ??= _sessions[0];

        int cells = _rows * _cols;
        int activeIdx = _sessions.IndexOf(_active);
        int start = (activeIdx / cells) * cells;               // 含 active 的那一頁

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack,
            ColumnCount = _cols, RowCount = _rows, Margin = Padding.Empty, Padding = Padding.Empty
        };
        for (int c = 0; c < _cols; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _cols));
        for (int r = 0; r < _rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _rows));

        for (int i = 0; i < cells; i++)
        {
            int si = start + i;
            Control cell = si < _sessions.Count ? MakeCell(_sessions[si], cells > 1) : new Panel { Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack };
            grid.Controls.Add(cell, i % _cols, i / _cols);
        }

        _body.Controls.Add(grid);
        grid.BringToFront();
        _body.ResumeLayout();
        _tabStrip.Invalidate();
        FocusActive();
    }

    private Control MakeCell(Session s, bool withLabel)
    {
        var cell = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack, Margin = Padding.Empty, Padding = new Padding(1) };
        s.Page.Dock = DockStyle.Fill;
        s.Page.Visible = true;
        cell.Controls.Add(s.Page);                              // Fill 先加
        if (withLabel)
        {
            string labelText = string.IsNullOrEmpty(s.Page.GroupLabel)
                ? $"{(s.IsSsh ? "🖧" : "🔌")}  {s.Title}"
                : $"{(s.IsSsh ? "🖧" : "🔌")}  {s.Title}  [{s.Page.GroupLabel}]";
            var lbl = new Label
            {
                Dock = DockStyle.Bottom, Height = 22,
                Text = labelText,
                ForeColor = s == _active ? Theme.Text : Theme.TextDim,
                BackColor = Theme.RailBack, Font = Theme.UiFont,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            };
            lbl.Click += (_, _) => { _active = s; Relayout(); };
            cell.Controls.Add(lbl);                             // Bottom
        }
        return cell;
    }

    private void FocusActive()
    {
        if (_active?.Page is { IsDisposed: false } p && p.IsHandleCreated)
            p.Focus();
    }

    // ── 頂部 Tab 列 ──────────────────────────────────────────
    private void LayoutTabs()
    {
        int x = 0;
        foreach (var s in _sessions)
        {
            s.TabBounds = new Rectangle(x, 0, TabW, StripH);
            s.CloseRect = new Rectangle(s.TabBounds.Right - CloseSz - 6, (StripH - CloseSz) / 2, CloseSz, CloseSz);
            x += TabW;
        }
    }

    private void OnStripMouseDown(object? sender, MouseEventArgs e)
    {
        LayoutTabs();
        if (e.Button == MouseButtons.Right)
        {
            foreach (var s in _sessions)
            {
                if (s.TabBounds.Contains(e.Location)) { ShowGroupMenu(s, e.Location); return; }
            }
            return;
        }
        foreach (var s in _sessions)
        {
            if (s.CloseRect.Contains(e.Location)) { CloseSession(s); return; }
            if (s.TabBounds.Contains(e.Location))
            {
                _active = s;
                _dragTab = s;            // 記下候選拖曳對象，超過門檻才真的開始拖
                _dragStartX = e.X;
                _dragging = false;
                Relayout();
                return;
            }
        }
    }

    private void ShowGroupMenu(Session s, Point pt)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("No Group", null, (_, _) => SetSessionGroup(s, 0));
        menu.Items.Add("Group 1", null, (_, _) => SetSessionGroup(s, 1));
        menu.Items.Add("Group 2", null, (_, _) => SetSessionGroup(s, 2));
        menu.Items.Add("Group 3", null, (_, _) => SetSessionGroup(s, 3));
        // Check current
        int current = s.Page.Group;
        ((ToolStripMenuItem)menu.Items[current]).Checked = true;
        menu.Show(_tabStrip, pt);
    }

    private void OnStripMouseMove(object? sender, MouseEventArgs e)
    {
        // 拖曳排序：按住某分頁並橫向移動，依滑鼠 X 即時換位
        if (e.Button == MouseButtons.Left && _dragTab != null)
        {
            if (!_dragging && Math.Abs(e.X - _dragStartX) > DragThreshold)
            {
                _dragging = true;
                _tabStrip.Cursor = Cursors.SizeWE;
            }
            if (_dragging)
            {
                int target = Math.Clamp(e.X / TabW, 0, _sessions.Count - 1);
                int cur = _sessions.IndexOf(_dragTab);
                if (cur >= 0 && target != cur)
                {
                    _sessions.RemoveAt(cur);
                    _sessions.Insert(target, _dragTab);
                    _tabStrip.Invalidate();
                }
            }
            return;
        }

        LayoutTabs();
        Session? ht = null;
        foreach (var s in _sessions) if (s.TabBounds.Contains(e.Location)) { ht = s; break; }
        if (ht != _hoverTab) { _hoverTab = ht; _tabStrip.Invalidate(); }
    }

    private void OnStripMouseUp(object? sender, MouseEventArgs e)
    {
        bool reordered = _dragging;
        _dragTab = null;
        _dragging = false;
        _tabStrip.Cursor = Cursors.Default;
        if (reordered)
        {
            RefreshGroupLabels();   // Group 內編號（A/B/C…）依新順序更新
            Relayout();             // 重鋪格子，讓 layout 位置跟著新分頁順序
        }
    }

    private void OnStripPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.RailBack);
        LayoutTabs();
        foreach (var s in _sessions)
        {
            bool active = s == _active, hover = s == _hoverTab;
            bool drag = _dragging && s == _dragTab;
            using (var b = new SolidBrush(drag ? Theme.Hover : active ? Theme.TabActiveBack : hover ? Theme.Hover : Theme.TabBack))
                g.FillRectangle(b, s.TabBounds);
            if (active)
                using (var bar = new SolidBrush(Theme.Accent))
                    g.FillRectangle(bar, new Rectangle(s.TabBounds.Left, s.TabBounds.Bottom - 2, s.TabBounds.Width, 2));
            if (drag)
                using (var pen = new Pen(Theme.Accent, 1))
                    g.DrawRectangle(pen, new Rectangle(s.TabBounds.Left, s.TabBounds.Top, s.TabBounds.Width - 1, s.TabBounds.Height - 1));
            using (var dot = new SolidBrush(s.IsSsh ? Theme.SshColor : Theme.SerialColor))
                g.FillEllipse(dot, s.TabBounds.Left + 9, StripH / 2 - 4, 8, 8);
            var tr = new Rectangle(s.TabBounds.Left + 22, s.TabBounds.Top, s.TabBounds.Width - 22 - CloseSz - 10, StripH);
            TextRenderer.DrawText(g, s.Title, Theme.UiFont, tr, active ? Theme.Text : Theme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "✕", Theme.UiFont, s.CloseRect, hover ? Theme.Text : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ── Layout 按鈕 ──────────────────────────────────────────
    private Button MakePresetButton(string label, int rows, int cols)
    {
        var b = new Button
        {
            Text = label, AutoSize = false, Size = new Size(44, 26), FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
            Margin = new Padding(3, 0, 3, 0), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Theme.Border;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        b.Click += (_, _) => SetLayout(rows, cols);
        return b;
    }

    private static Button MakeActionButton(string text, int width, int leftMargin, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text, AutoSize = false, Size = new Size(width, 26), FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
            Margin = new Padding(leftMargin, 0, 3, 0), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Theme.SerialColor;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        b.Click += onClick;
        return b;
    }

    // ── Log All（一次開/關所有分頁側錄） ──────────────────────
    private void OnToggleLogAll(object? sender, EventArgs e)
    {
        if (_sessions.Count == 0)
        {
            MessageBox.Show(this, "No open sessions to log.", "Log All", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 只要還有分頁沒在側錄 → 全部開始；否則全部停止。
        bool startAll = _sessions.Any(s => !s.Page.IsLogging);
        if (startAll)
        {
            int failed = 0;
            foreach (var s in _sessions)
                if (!s.Page.StartLog()) failed++;
            SetLogAllActive(true);
            if (failed > 0)
                MessageBox.Show(this, $"{failed} session(s) failed to start logging. See app log for details.",
                    "Log All", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            foreach (var s in _sessions) s.Page.StopLog();
            SetLogAllActive(false);
        }
    }

    private void SetLogAllActive(bool on)
    {
        _logAll.Text = on ? "⏺ Logging All" : "⏺ Log All";
        _logAll.Width = on ? 110 : 96;
        _logAll.ForeColor = on ? Theme.SerialColor : Theme.Text;
    }

    // ── Run All（個別執行，拒絕 waitall/sendlnall） ──────────
    private async void OnRunAllSerial(object? sender, EventArgs e)
    {
        var serials = _sessions
            .Where(s => !s.IsSsh && s.Page.IsSerial && !s.Page.IsScriptRunning)
            .Select(s => s.Page)
            .ToList();

        if (serials.Count == 0)
        {
            MessageBox.Show(this, "No available Serial sessions.", "Run All", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string content, name;
        using (var dlg = new OpenFileDialog { Filter = "TTL scripts (*.ttl)|*.ttl|All files (*.*)|*.*" })
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            content = File.ReadAllText(dlg.FileName);
            name = Path.GetFileName(dlg.FileName);
        }

        // Run All 拒絕含 waitall / sendlnall 的腳本
        if (ScriptContainsGroupCommands(content))
        {
            MessageBox.Show(this,
                "Script contains 'waitall' or 'sendlnall' which require Group execution.\nPlease use Run Group buttons instead.",
                "Run All", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await Task.WhenAll(serials.Select(sp => sp.RunScriptAsync(content, name)));
    }

    // ── Run Group（支援 waitall/sendlnall 同步） ─────────────
    private async void OnRunGroup(int group)
    {
        var members = _sessions
            .Where(s => s.Page.Group == group && !s.Page.IsScriptRunning)
            .Select(s => s.Page)
            .ToList();

        if (members.Count == 0)
        {
            MessageBox.Show(this, $"No available sessions in Group {group}.", $"Run Group{group}", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string content, name;
        using (var dlg = new OpenFileDialog { Filter = "TTL scripts (*.ttl)|*.ttl|All files (*.*)|*.*" })
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            content = File.ReadAllText(dlg.FileName);
            name = Path.GetFileName(dlg.FileName);
        }

        var sync = new GroupSyncContext(members.Count);
        await Task.WhenAll(members.Select((sp, i) => sp.RunGroupScriptAsync(content, name, sync, ((char)('A' + i)).ToString())));
    }

    private static bool ScriptContainsGroupCommands(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var t = line.Trim().ToLower();
            if (t.StartsWith("waitall") || t.StartsWith("sendlnall") || t.StartsWith("sendlngroup"))
                return true;
        }
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var s in _sessions) { try { s.Page.Dispose(); } catch { } }
        base.Dispose(disposing);
    }

    private static void SetDoubleBuffered(Control c) =>
        typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(c, true, null);
}
