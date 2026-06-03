using System.Drawing;
using System.Windows.Forms;
using ETTerms.Infrastructure;
using ETTerms.Scripting.Pdu;

namespace ETTerms.App;

/// <summary>Settings page with tabs: Terminal / PDU.</summary>
public sealed class SettingsView : UserControl
{
    public SettingsView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.WorkspaceBack;

        // Use a custom tab strip + panel swapping instead of TabControl to avoid white borders
        var tabBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 32, BackColor = Theme.RailBack,
            Padding = new Padding(4, 4, 4, 0), WrapContents = false
        };

        var terminalPage = BuildTerminalTab();
        var pduPage = BuildPduTab();
        terminalPage.Dock = DockStyle.Fill;
        pduPage.Dock = DockStyle.Fill;
        pduPage.Visible = false;

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack };
        body.Controls.Add(terminalPage);
        body.Controls.Add(pduPage);

        Button? activeBtn = null;
        Button MakeTab(string text, Panel page)
        {
            var b = new Button
            {
                Text = text, AutoSize = false, Width = 80, Height = 26, FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
                Margin = new Padding(0, 0, 4, 0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.Click += (_, _) =>
            {
                terminalPage.Visible = page == terminalPage;
                pduPage.Visible = page == pduPage;
                if (activeBtn != null) activeBtn.BackColor = Theme.TabBack;
                b.BackColor = Theme.TabActiveBack;
                activeBtn = b;
            };
            return b;
        }

        var termBtn = MakeTab("Terminal", terminalPage);
        var pduBtn = MakeTab("PDU", pduPage);
        tabBar.Controls.Add(termBtn);
        tabBar.Controls.Add(pduBtn);

        // Set initial active
        termBtn.BackColor = Theme.TabActiveBack;
        activeBtn = termBtn;

        Controls.Add(body);
        Controls.Add(tabBar);
    }

    // ═══ Terminal Tab ═══
    private Panel BuildTerminalTab()
    {
        var page = new Panel { BackColor = Theme.WorkspaceBack, Padding = new Padding(20) };
        var s = AppSettings.Instance;

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Theme.WorkspaceBack, AutoScroll = true
        };

        var font = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDown, BackColor = Theme.TabBack, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        font.Items.AddRange(new object[] { "Cascadia Mono", "Consolas", "Courier New", "Lucida Console", "JetBrains Mono" });
        font.Text = s.FontFamily;

        var fontSize = new NumericUpDown { Width = 80, Minimum = 8, Maximum = 24, DecimalPlaces = 1, Increment = 0.5m, Value = (decimal)s.FontSize, BackColor = Theme.TabBack, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
        var scrollback = new NumericUpDown { Width = 100, Minimum = 500, Maximum = 50000, Increment = 500, Value = s.ScrollbackLines, BackColor = Theme.TabBack, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };

        var scheme = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.TabBack, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        scheme.Items.AddRange(new object[] { "Dark", "Solarized Dark", "Monokai" });
        scheme.Text = s.ColorScheme;

        var newline = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.TabBack, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        newline.Items.AddRange(new object[] { "\\r\\n", "\\r", "\\n" });
        newline.Text = s.DefaultNewLine;

        flow.Controls.Add(MakeRow("Font Family", font));
        flow.Controls.Add(MakeRow("Font Size", fontSize));
        flow.Controls.Add(MakeRow("Scrollback Lines", scrollback));
        flow.Controls.Add(MakeRow("Color Scheme", scheme));
        flow.Controls.Add(MakeRow("Default Newline", newline));
        flow.Controls.Add(MakeSpacer(16));

        // Shell settings
        flow.Controls.Add(new Label { Text = "Shell Settings", AutoSize = true, ForeColor = Theme.Accent, Font = Theme.UiFontBold, Margin = new Padding(0, 0, 0, 4) });

        var shellType = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.TabBack, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        shellType.Items.AddRange(new object[] { "PowerShell", "Bash", "Cmd" });
        shellType.Text = s.ShellType;
        flow.Controls.Add(MakeRow("Terminal Shell", shellType));

        // 用一個帶邊框的容器包住「輸入框 + 瀏覽鈕」，讓兩者看起來像同一個欄位
        var dirPanel = new Panel { Width = InputWidth, Height = 24, BackColor = Theme.TabBack, BorderStyle = BorderStyle.FixedSingle };
        var shellDir = new TextBox { BackColor = Theme.TabBack, ForeColor = Theme.Text, Font = Theme.UiFont, Text = s.ShellStartupDir, BorderStyle = BorderStyle.None };
        var browseBtn = new Button
        {
            Text = "…", Width = 26, FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.TextDim, BackColor = Theme.TabBack, Font = Theme.UiFont, Cursor = Cursors.Hand
        };
        // 無邊框、暗色文字、與輸入框同底色 → 不再突兀
        browseBtn.FlatAppearance.BorderSize = 0;
        browseBtn.FlatAppearance.MouseOverBackColor = Theme.Hover;
        browseBtn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = shellDir.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK) shellDir.Text = dlg.SelectedPath;
        };
        // 文字框置中於容器、瀏覽鈕貼右
        shellDir.Dock = DockStyle.Fill;
        shellDir.Margin = new Padding(0);
        browseBtn.Dock = DockStyle.Right;
        var dirInner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.TabBack, Padding = new Padding(4, 3, 0, 0) };
        dirInner.Controls.Add(shellDir);
        // 先加靠右的按鈕、再加 Fill 容器，避免 Fill 蓋到按鈕下方
        dirPanel.Controls.Add(browseBtn);
        dirPanel.Controls.Add(dirInner);
        flow.Controls.Add(MakeRow("Startup Directory", dirPanel));
        flow.Controls.Add(MakeSpacer(12));

        // ── Live Preview ──
        var preview = new Panel { Width = 460, Height = 100, BackColor = Color.FromArgb(20, 20, 24), Margin = new Padding(0, 0, 0, 8) };
        var previewLabel = new Label
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 24), ForeColor = Color.FromArgb(200, 200, 200),
            Text = "admin@switch01:~$ show version\nCisco IOS v15.2 — 設定預覽\nABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789",
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 5, 10, 5)
        };
        preview.Controls.Add(previewLabel);
        flow.Controls.Add(preview);

        void UpdatePreview()
        {
            try { previewLabel.Font = new Font(font.Text, (float)fontSize.Value); } catch { }
        }
        font.TextChanged += (_, _) => UpdatePreview();
        fontSize.ValueChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        flow.Controls.Add(MakeSpacer(8));

        var save = MakeButton("Save", Theme.Accent);
        save.Click += (_, _) =>
        {
            s.FontFamily = font.Text;
            s.FontSize = (float)fontSize.Value;
            s.ScrollbackLines = (int)scrollback.Value;
            s.ColorScheme = scheme.Text;
            s.DefaultNewLine = newline.Text;
            s.ShellType = shellType.Text;
            s.ShellStartupDir = shellDir.Text;
            s.Save();
            MessageBox.Show(this, "Settings saved.\nNew sessions will use these settings.\nExisting sessions keep their current font.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        flow.Controls.Add(save);

        page.Controls.Add(flow);
        return page;
    }

    // ═══ PDU Tab ═══
    private Panel BuildPduTab()
    {
        var page = new Panel { BackColor = Theme.WorkspaceBack, Padding = new Padding(20) };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Theme.WorkspaceBack, AutoScroll = true
        };

        // Connection row
        var ipBox = new TextBox { Width = 160, Text = "192.168.1.21", BackColor = Theme.TabBack, ForeColor = Theme.Text, Font = Theme.UiFont };
        var connectBtn = MakeButton("Connect", Theme.SerialColor);
        var statusLabel = new Label { AutoSize = true, Text = "Disconnected", ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(8, 8, 0, 0) };

        var connRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 500, Height = 36, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
        connRow.Controls.Add(new Label { Text = "PDU IP:", AutoSize = true, ForeColor = Theme.Text, Font = Theme.UiFont, Margin = new Padding(0, 6, 8, 0) });
        connRow.Controls.Add(ipBox);
        connRow.Controls.Add(connectBtn);
        connRow.Controls.Add(statusLabel);
        flow.Controls.Add(connRow);

        // Port status grid
        var grid = new DataGridView
        {
            Width = 480, Height = 310, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Theme.WorkspaceBack, ForeColor = Theme.Text, GridColor = Theme.Border,
            BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            DefaultCellStyle = { BackColor = Theme.TabBack, ForeColor = Theme.Text, SelectionBackColor = Theme.Hover, SelectionForeColor = Theme.Text },
            ColumnHeadersDefaultCellStyle = { BackColor = Theme.RailBack, ForeColor = Theme.Accent, Font = Theme.UiFontBold },
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
            AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.None, Font = Theme.UiFont,
            RowTemplate = { Height = 24 },
            Margin = new Padding(0, 8, 0, 8)
        };
        grid.Columns.Add("Port", "Port");
        grid.Columns.Add("Status", "Status");
        grid.Columns.Add("Current", "Current (mA)");
        grid.Columns.Add("Power", "Power (W)");
        for (int i = 1; i <= 12; i++)
            grid.Rows.Add($"Port {i}", "—", "—", "—");
        flow.Controls.Add(grid);

        // Refresh button
        var refreshBtn = MakeButton("Refresh", Theme.Accent);
        flow.Controls.Add(refreshBtn);

        // Logic
        PduController? pdu = null;

        connectBtn.Click += (_, _) =>
        {
            if (pdu != null) { pdu.Dispose(); pdu = null; statusLabel.Text = "Disconnected"; statusLabel.ForeColor = Theme.TextDim; connectBtn.Text = "Connect"; return; }
            var ip = ipBox.Text.Trim();
            var p = new PduController(ip);
            if (p.CheckConnection())
            {
                pdu = p;
                statusLabel.Text = $"Connected to {ip}";
                statusLabel.ForeColor = Theme.SerialColor;
                connectBtn.Text = "Disconnect";
                RefreshPduGrid(pdu, grid);
            }
            else
            {
                p.Dispose();
                MessageBox.Show(this, $"Failed to connect to PDU at {ip}", "PDU", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        refreshBtn.Click += (_, _) =>
        {
            if (pdu == null) { MessageBox.Show(this, "Connect to PDU first.", "PDU", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            RefreshPduGrid(pdu, grid);
        };

        page.Controls.Add(flow);
        return page;
    }

    private static void RefreshPduGrid(PduController pdu, DataGridView grid)
    {
        for (int i = 0; i < 12; i++)
        {
            int port = i + 1;
            var state = pdu.GetPortState(port);
            var current = pdu.GetPortCurrent(port);
            var power = pdu.GetPortPowerWatts(port);
            var row = grid.Rows[i];
            row.Cells["Status"].Value = state == true ? "ON" : state == false ? "OFF" : "—";
            row.Cells["Current"].Value = current.HasValue ? $"{current.Value}" : "—";
            row.Cells["Power"].Value = power.HasValue ? $"{power.Value:F1}" : "—";
            row.DefaultCellStyle.BackColor = state == true ? Color.FromArgb(40, 80, 40) : state == false ? Color.FromArgb(60, 40, 40) : Theme.TabBack;
        }
    }

    // ── Helpers ──
    private const int LabelWidth = 150;   // 標籤欄固定寬度
    private const int InputWidth = 200;   // 所有輸入框統一寬度

    private static Panel MakeRow(string label, Control control)
    {
        var row = new Panel { Width = LabelWidth + InputWidth, Height = 32, Margin = new Padding(0, 3, 0, 3) };

        // 統一輸入框寬度 + 左緣對齊（不再 Dock.Right，避免右對齊造成左緣參差）
        control.Width = InputWidth;
        int top = (row.Height - control.Height) / 2;
        control.Location = new Point(LabelWidth, top < 0 ? 0 : top);

        row.Controls.Add(control);
        row.Controls.Add(new Label
        {
            Text = label, AutoSize = false, Width = LabelWidth, Height = row.Height,
            Location = new Point(0, 0), ForeColor = Theme.Text, Font = Theme.UiFont,
            TextAlign = ContentAlignment.MiddleLeft
        });
        return row;
    }

    private static Button MakeButton(string text, Color borderColor)
    {
        var b = new Button
        {
            Text = text, Width = 90, Height = 28, FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
            Cursor = Cursors.Hand, Margin = new Padding(8, 0, 0, 0)
        };
        b.FlatAppearance.BorderColor = borderColor;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        return b;
    }

    private static Control MakeSpacer(int h) => new Panel { Width = 10, Height = h, Margin = Padding.Empty };
}
