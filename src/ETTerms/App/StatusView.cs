using System.Drawing;
using System.Windows.Forms;
using ETTerms.Scripting.Pdu;

namespace ETTerms.App;

/// <summary>
/// Status page with tabs: PDU (more views to come).
/// 風格參考 <see cref="SettingsView"/>：自繪 tab strip + panel 切換，避免 TabControl 白邊。
/// PDU 分頁連線後每 3 秒於背景自動輪詢插座狀態（不需手動 Refresh）。
/// </summary>
public sealed class StatusView : UserControl
{
    public StatusView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.WorkspaceBack;

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

        var pduBtn = MakeTab("PDU", BuildPduTab());
        tabBar.Controls.Add(pduBtn);

        Controls.Add(body);
        Controls.Add(tabBar);

        pduBtn.PerformClick();
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

        // 控制按鈕欄：按一下切換該 Port 的 ON/OFF
        var actionCol = new DataGridViewButtonColumn
        {
            Name = "Action", HeaderText = "Control",
            UseColumnTextForButtonValue = false, FlatStyle = FlatStyle.Flat,
            FillWeight = 80,
            DefaultCellStyle =
            {
                BackColor = Theme.TabBack, ForeColor = Theme.Text,
                SelectionBackColor = Theme.Hover, SelectionForeColor = Theme.Text,
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        };
        grid.Columns.Add(actionCol);

        for (int i = 1; i <= 12; i++)
            grid.Rows.Add($"Port {i}", "—", "—", "—", "—");
        flow.Controls.Add(grid);

        // 連線後每 3 秒自動輪詢（背景執行緒讀 SNMP，Invoke 回 UI 更新）
        var pollLabel = new Label { AutoSize = true, Text = "", ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(0, 4, 0, 0) };
        flow.Controls.Add(pollLabel);

        // Logic
        PduController? pdu = null;
        System.Threading.Timer? timer = null;
        int polling = 0;   // 0=idle, 1=in-flight（避免上一次未讀完又疊一次）

        void StopPolling()
        {
            timer?.Dispose();
            timer = null;
        }

        void PollOnce()
        {
            // 已在輪詢中就跳過這一輪
            if (System.Threading.Interlocked.Exchange(ref polling, 1) == 1) return;
            var current = pdu;
            if (current == null) { System.Threading.Volatile.Write(ref polling, 0); return; }

            try
            {
                var rows = new (bool? state, int? current, double? power)[12];
                for (int i = 0; i < 12; i++)
                {
                    int port = i + 1;
                    rows[i] = (current.GetPortState(port), current.GetPortCurrent(port), current.GetPortPowerWatts(port));
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (pdu != current) return;   // 期間已斷線
                        ApplyPduRows(grid, rows);
                        pollLabel.Text = $"Auto-refresh every 3s · last update {DateTime.Now:HH:mm:ss}";
                    }));
                }
            }
            catch { /* 輪詢失敗忽略，下一輪再試 */ }
            finally { System.Threading.Volatile.Write(ref polling, 0); }
        }

        connectBtn.Click += (_, _) =>
        {
            if (pdu != null)
            {
                StopPolling();
                pdu.Dispose(); pdu = null;
                statusLabel.Text = "Disconnected"; statusLabel.ForeColor = Theme.TextDim;
                connectBtn.Text = "Connect";
                pollLabel.Text = "";
                // 斷線後清空表格，避免顯示過時的狀態
                foreach (DataGridViewRow row in grid.Rows)
                {
                    row.Cells["Status"].Value = "—";
                    row.Cells["Current"].Value = "—";
                    row.Cells["Power"].Value = "—";
                    row.Cells["Action"].Value = "—";
                    row.DefaultCellStyle.BackColor = Theme.TabBack;
                }
                return;
            }

            var ip = ipBox.Text.Trim();
            var p = new PduController(ip);
            if (p.CheckConnection())
            {
                pdu = p;
                statusLabel.Text = $"Connected to {ip}";
                statusLabel.ForeColor = Theme.SerialColor;
                connectBtn.Text = "Disconnect";
                // 立即讀一次，之後每 3 秒一次
                timer = new System.Threading.Timer(_ => PollOnce(), null,
                    dueTime: 0, period: 3000);
            }
            else
            {
                p.Dispose();
                MessageBox.Show(this, $"Failed to connect to PDU at {ip}", "PDU", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        // 控制項銷毀時收掉計時器與連線
        Disposed += (_, _) => { StopPolling(); pdu?.Dispose(); };

        // 按下 Control 欄按鈕：切換該 Port 的 ON/OFF（SNMP Set 在背景執行，避免卡 UI）
        grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name != "Action") return;

            var current = pdu;
            if (current == null)
            {
                MessageBox.Show(this, "PDU is not connected. Please connect first.",
                    "PDU", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int port = e.RowIndex + 1;
            var statusVal = grid.Rows[e.RowIndex].Cells["Status"].Value?.ToString();
            if (statusVal != "ON" && statusVal != "OFF") return;   // 狀態未知時不動作
            bool turnOn = statusVal != "ON";   // 目前 ON → 關；其餘 → 開

            var actionCell = grid.Rows[e.RowIndex].Cells["Action"];
            actionCell.Value = "…";

            System.Threading.Tasks.Task.Run(() =>
            {
                bool ok = turnOn ? current.SetPortOn(port) : current.SetPortOff(port);
                System.Threading.Thread.Sleep(400);   // 等 PDU 套用後再讀回確認
                PollOnce();                            // 背景讀 SNMP 後 Invoke 回 UI 更新整張表

                if (!ok && !IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                        MessageBox.Show(this,
                            $"Failed to turn {(turnOn ? "ON" : "OFF")} Port {port}",
                            "PDU", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        };

        page.Controls.Add(flow);
        return page;
    }

    private static void ApplyPduRows(DataGridView grid, (bool? state, int? current, double? power)[] rows)
    {
        for (int i = 0; i < rows.Length && i < grid.Rows.Count; i++)
        {
            var (state, current, power) = rows[i];
            var row = grid.Rows[i];
            row.Cells["Status"].Value = state == true ? "ON" : state == false ? "OFF" : "—";
            row.Cells["Current"].Value = current.HasValue ? $"{current.Value}" : "—";
            row.Cells["Power"].Value = power.HasValue ? $"{power.Value:F1}" : "—";
            // 按鈕文字代表「按下後會做的動作」：ON 時顯示 Turn OFF，反之亦然
            row.Cells["Action"].Value = state == true ? "Turn OFF" : state == false ? "Turn ON" : "—";
            row.DefaultCellStyle.BackColor = state == true ? Color.FromArgb(40, 80, 40) : state == false ? Color.FromArgb(60, 40, 40) : Theme.TabBack;
        }
    }

    // ── Helpers ──
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
}
