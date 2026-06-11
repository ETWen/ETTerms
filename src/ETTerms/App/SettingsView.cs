using System.Drawing;
using System.Windows.Forms;
using ETTerms.Infrastructure;

namespace ETTerms.App;

/// <summary>Settings page with tabs: Terminal / AI MCP.</summary>
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

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.WorkspaceBack };
        var pages = new List<Panel>();

        Button? activeBtn = null;
        Button MakeTab(string text, Panel page)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            body.Controls.Add(page);
            pages.Add(page);

            var b = new Button
            {
                Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(70, 26), Padding = new Padding(10, 2, 10, 2),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
                Margin = new Padding(0, 0, 4, 0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.Click += (_, _) =>
            {
                foreach (var p in pages) p.Visible = p == page;
                if (activeBtn != null) activeBtn.BackColor = Theme.TabBack;
                b.BackColor = Theme.TabActiveBack;
                activeBtn = b;
            };
            return b;
        }

        var termBtn = MakeTab("Terminal", BuildTerminalTab());
        tabBar.Controls.Add(termBtn);
        tabBar.Controls.Add(MakeTab("AI MCP", BuildAiMcpTab()));

        Controls.Add(body);
        Controls.Add(tabBar);

        // Set initial active tab
        termBtn.PerformClick();
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

    // ═══ AI MCP Tab ═══
    private Panel BuildAiMcpTab()
    {
        var page = new Panel { BackColor = Theme.WorkspaceBack, Padding = new Padding(20) };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Theme.WorkspaceBack, AutoScroll = true
        };

        flow.Controls.Add(new Label
        {
            Text = "AI MCP Integration", AutoSize = true,
            ForeColor = Theme.Accent, Font = Theme.UiFontBold, Margin = new Padding(0, 0, 0, 4)
        });
        flow.Controls.Add(new Label
        {
            Text = "One-click register the ETTerms MCP servers (Serial + PDU) into your AI CLI's\n" +
                   "user-level config. Serial: ETTerms owns the COM port, the AI drives it through a\n" +
                   "local named pipe (open a Serial session first). PDU: the AI controls outlets\n" +
                   "directly over SNMP — no GUI session required.",
            AutoSize = false, Width = 600, Height = 64,
            ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 8)
        });

        // Resolved MCP server exes (serial + pdu)
        foreach (var (name, exe, exists) in McpRegistrar.ServerInfos())
        {
            flow.Controls.Add(new Label
            {
                Text = $"{name}:  {exe}",
                AutoSize = false, Width = 600, Height = 20,
                ForeColor = exists ? Theme.SerialColor : Color.FromArgb(210, 150, 120),
                Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 2)
            });
        }
        if (!McpRegistrar.ServerExeExists())
        {
            flow.Controls.Add(new Label
            {
                Text = "⚠ Some servers not built yet — publish the app (or build the MCP projects). Setup still writes the expected paths.",
                AutoSize = false, Width = 600, Height = 20,
                ForeColor = Color.FromArgb(210, 150, 120), Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 4)
            });
        }

        flow.Controls.Add(MakeSpacer(10));
        flow.Controls.Add(BuildMcpTargetCard(McpTarget.Claude));
        flow.Controls.Add(MakeSpacer(10));
        flow.Controls.Add(BuildMcpTargetCard(McpTarget.Kiro));

        page.Controls.Add(flow);
        return page;
    }

    /// <summary>單一 AI 目標（Claude / Kiro）的設定卡：狀態 + Setup / Remove + CLI 驗證指令。</summary>
    private Panel BuildMcpTargetCard(McpTarget target)
    {
        var card = new Panel
        {
            Width = 600, Height = 196, BackColor = Theme.TabBack,
            Padding = new Padding(14), Margin = new Padding(0, 0, 0, 4)
        };
        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Theme.TabBack, AutoSize = false
        };

        var title = new Label
        {
            Text = McpRegistrar.DisplayName(target), AutoSize = true,
            ForeColor = Theme.Text, Font = Theme.UiFontBold, Margin = new Padding(0, 0, 0, 2)
        };
        var status = new Label { AutoSize = true, Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 2) };
        var pathLbl = new Label
        {
            Text = $"Config:  {McpRegistrar.ConfigPath(target)}",
            AutoSize = false, Width = 560, Height = 18,
            ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 6)
        };

        var setupBtn = MakeButton("Setup", Theme.Accent);
        var removeBtn = MakeButton("Remove", Color.FromArgb(210, 120, 120));
        setupBtn.Margin = new Padding(0, 0, 8, 0);
        var btnRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, AutoSize = true,
            WrapContents = false, BackColor = Theme.TabBack, Margin = new Padding(0, 0, 0, 8)
        };
        btnRow.Controls.Add(setupBtn);
        btnRow.Controls.Add(removeBtn);

        var verifyLbl = new Label
        {
            Text = "Verify in your CLI:", AutoSize = true,
            ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(0, 0, 0, 2)
        };
        var verifyBox = new TextBox
        {
            Multiline = true, ReadOnly = true, Width = 560, Height = 56,
            BackColor = Color.FromArgb(20, 20, 24), ForeColor = Color.FromArgb(200, 200, 200),
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Cascadia Mono", 9f),
            Text = McpRegistrar.VerifyHint(target)
        };

        void Refresh()
        {
            bool reg = McpRegistrar.IsRegistered(target);
            status.Text = reg ? "● Configured" : "○ Not configured";
            status.ForeColor = reg ? Theme.SerialColor : Theme.TextDim;
            removeBtn.Enabled = reg;
        }

        setupBtn.Click += (_, _) =>
        {
            try
            {
                McpRegistrar.Register(target);
                Refresh();
                MessageBox.Show(this,
                    $"{McpRegistrar.DisplayName(target)} is now configured.\n\n" +
                    "Restart your AI CLI (or open a new session), then run the verify command shown below.",
                    "AI MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to write config:\n{ex.Message}",
                    "AI MCP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        removeBtn.Click += (_, _) =>
        {
            try
            {
                McpRegistrar.Unregister(target);
                Refresh();
                MessageBox.Show(this,
                    $"Removed from {McpRegistrar.DisplayName(target)}.\nRestart your AI CLI for it to take effect.",
                    "AI MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to update config:\n{ex.Message}",
                    "AI MCP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        Refresh();

        col.Controls.Add(title);
        col.Controls.Add(status);
        col.Controls.Add(pathLbl);
        col.Controls.Add(btnRow);
        col.Controls.Add(verifyLbl);
        col.Controls.Add(verifyBox);
        card.Controls.Add(col);
        return card;
    }

    // ── Helpers ──
    private const int LabelWidth = 150;   // 標籤欄最小寬度
    private const int InputWidth = 200;   // 所有輸入框統一寬度

    private static Control MakeRow(string label, Control control)
    {
        // 用 FlowLayout + AutoSize 標籤取代絕對定位，讓高 DPI 下標籤變寬也不會被裁、且對齊穩定
        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Margin = new Padding(0, 3, 0, 3), BackColor = Color.Transparent
        };
        var lbl = new Label
        {
            Text = label, AutoSize = true, MinimumSize = new Size(LabelWidth, 0),
            ForeColor = Theme.Text, Font = Theme.UiFont,
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 8, 0)
        };
        if (control.Width <= 0) control.Width = InputWidth;
        row.Controls.Add(lbl);
        row.Controls.Add(control);
        return row;
    }

    private static Button MakeButton(string text, Color borderColor)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(90, 28), Padding = new Padding(10, 2, 10, 2),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
            Cursor = Cursors.Hand, Margin = new Padding(8, 0, 0, 0)
        };
        b.FlatAppearance.BorderColor = borderColor;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        return b;
    }

    private static Control MakeSpacer(int h) => new Panel { Width = 10, Height = h, Margin = Padding.Empty };
}
