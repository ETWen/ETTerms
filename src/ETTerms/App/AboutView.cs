using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ETTerms.Infrastructure;

namespace ETTerms.App;

/// <summary>About page: app info (left) + changelog (right).</summary>
public sealed class AboutView : UserControl
{
    public AboutView()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.WorkspaceBack;
        AutoScroll = true;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.1.0";

        // ── Main layout: left fixed + right scrollable ──
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 380, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(20),
            BackColor = Theme.WorkspaceBack
        };

        // App card
        left.Controls.Add(MakeCard(340, 140, p =>
        {
            p.Controls.Add(MakeLine($"ETTerms", Theme.UiFontBold, Theme.Accent, ContentAlignment.MiddleCenter));
            p.Controls.Add(MakeLine($"v{versionStr}", Theme.UiFont, Theme.TextDim, ContentAlignment.MiddleCenter));
            p.Controls.Add(MakeLine("", Theme.UiFont, Theme.TextDim, ContentAlignment.MiddleCenter));
            p.Controls.Add(MakeLine("A native Windows terminal workspace", Theme.UiFont, Theme.Text, ContentAlignment.MiddleCenter));
            p.Controls.Add(MakeLine("for SSH & Serial Port connections,", Theme.UiFont, Theme.Text, ContentAlignment.MiddleCenter));
            p.Controls.Add(MakeLine("with TTL scripting engine.", Theme.UiFont, Theme.Text, ContentAlignment.MiddleCenter));
        }));

        // Author card
        left.Controls.Add(MakeCard(340, 110, p =>
        {
            p.Controls.Add(MakeLine("👤  Developer", Theme.UiFontBold, Theme.Text));
            p.Controls.Add(MakeRow("Name", "ET Wen"));
            p.Controls.Add(MakeRow("Email", "eric441151893@gmail.com"));
            p.Controls.Add(MakeRow("GitHub", "github.com/ETWen"));
        }));

        // Tech stack card
        left.Controls.Add(MakeCard(340, 120, p =>
        {
            p.Controls.Add(MakeLine("🔧  Tech Stack", Theme.UiFontBold, Theme.Text));
            p.Controls.Add(MakeLine("  .NET 8 · WinForms · C#", Theme.UiFont, Theme.TextDim));
            p.Controls.Add(MakeLine("  SSH.NET · System.IO.Ports", Theme.UiFont, Theme.TextDim));
            p.Controls.Add(MakeLine("  SQLite · Windows Credential Manager", Theme.UiFont, Theme.TextDim));
            p.Controls.Add(MakeLine("  TTL Script Engine (ported from MyTeraTerm)", Theme.UiFont, Theme.TextDim));
        }));

        // Brand card — large app icon + logo
        left.Controls.Add(MakeBrandCard());

        // ═══ RIGHT PANEL — Changelog (scrollable) ═══
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(10, 20, 20, 20),
            BackColor = Theme.WorkspaceBack
        };

        right.Controls.Add(MakeLine("📋  Changelog", Theme.UiFontBold, Theme.Text));
        right.Controls.Add(MakeSpacer(8));

        foreach (var entry in Changelog)
        {
            var header = new Label
            {
                AutoSize = true,
                Text = $"● v{entry.Version}  ·  {entry.Title}    ({entry.Date:yyyy-MM-dd})",
                ForeColor = Theme.Accent, Font = Theme.UiFontBold,
                Margin = new Padding(0, 8, 0, 2)
            };
            right.Controls.Add(header);
            foreach (var change in entry.Changes)
            {
                right.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = $"    • {change}",
                    ForeColor = Theme.Text, Font = Theme.UiFont,
                    Margin = new Padding(0)
                });
            }
        }

        Controls.Add(right);   // Fill first
        Controls.Add(left);    // Left
    }

    // ── Helpers ──

    private static Panel MakeCard(int width, int height, Action<FlowLayoutPanel> build)
    {
        // 卡片本身就是 TopDown 的 AutoSize FlowLayoutPanel：高度永遠長到容得下內容
        // （高 DPI 字變大也不會被裁），MinimumSize 維持 100% 的設計尺寸。
        var card = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(width, height),
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Theme.TabBack, Padding = new Padding(12)
        };
        build(card);
        return card;
    }

    /// <summary>底部品牌卡：大的 Choco 圖示 + ETTerms 標誌圖。</summary>
    private static Panel MakeBrandCard()
    {
        var card = new Panel
        {
            Width = 340, Height = 300, Margin = new Padding(0, 0, 0, 12),
            BackColor = Theme.TabBack, Padding = new Padding(12)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Theme.TabBack, AutoSize = false
        };

        // 大圖示 (256x256)，等比縮放塞滿框、不裁切
        var iconImg = AppAssets.AppIcon(256);
        if (iconImg != null)
        {
            flow.Controls.Add(new PictureBox
            {
                Width = 312, Height = 268, Margin = new Padding(0, 4, 0, 4),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Theme.TabBack, Image = iconImg.ToBitmap()
            });
            iconImg.Dispose();
        }

        card.Controls.Add(flow);
        return card;
    }

    private static Label MakeLine(string text, Font font, Color color, ContentAlignment align = ContentAlignment.MiddleLeft)
    {
        return new Label
        {
            AutoSize = true,
            Text = text, Font = font, ForeColor = color,
            TextAlign = align, Margin = new Padding(0, 1, 0, 1)
        };
    }

    private static Control MakeRow(string label, string value)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Margin = new Padding(0, 2, 0, 0), BackColor = Color.Transparent
        };
        row.Controls.Add(new Label
        {
            Text = label, AutoSize = true, MinimumSize = new Size(70, 0),
            ForeColor = Theme.TextDim, Font = Theme.UiFont,
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 8, 0)
        });
        row.Controls.Add(new Label
        {
            Text = value, AutoSize = true,
            ForeColor = Theme.Text, Font = Theme.UiFont,
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0)
        });
        return row;
    }

    private static Control MakeSpacer(int height) => new Panel { Width = 10, Height = height, Margin = Padding.Empty };

    // ── Changelog Data ──

    private static readonly ChangelogEntry[] Changelog =
    [
        new("0.3.1", new DateOnly(2026, 6, 8), "Bugfix — terminal usability",
        [
            "Added a scrollbar on the right of the terminal — just drag it to look back through long output, instead of spinning the mouse wheel for ages.",
            "Fixed pasting multiple lines into Kiro CLI (and similar tools): the whole block now pastes in one go, instead of each line being sent off immediately.",
            "After you right-click to copy, the highlight now clears so you can tell the copy actually worked.",
        ]),
        new("0.3.0", new DateOnly(2026, 6, 8), "AI power control for PDU outlets",
        [
            "AI assistants (Kiro / Claude) can now switch your PDU power outlets on and off for you.",
            "Great for automated testing — the AI can power-cycle (restart) a connected device by itself.",
            "Works even when the main ETTerms window isn't open.",
            "One click in Settings → AI MCP now sets up both the Serial and PDU AI helpers at the same time.",
        ]),
        new("0.2.2", new DateOnly(2026, 6, 5), "Bugfix — terminal stability",
        [
            "Fixed: the terminal no longer freezes after minimizing or switching tabs (notably in PowerShell / Kiro).",
            "Added: Shift+Enter inserts a newline in the shell, so you can type multi-line commands.",
        ]),
        new("0.2.1", new DateOnly(2026, 6, 4), "AI MCP one-click setup",
        [
            "New Settings → AI MCP tab: register the Serial MCP server into Claude Code or Kiro with one click.",
            "Lets AI agents drive the serial port for you.",
        ]),
        new("0.2.0", new DateOnly(2026, 6, 4), "Beta Version Release",
        [
            "AI agents can now send/receive on the serial port while you watch it live in the GUI.",
            "New app icon and version shown in the title bar.",
        ]),
        new("0.1.0", new DateOnly(2026, 6, 3), "Initial Release",
        [
            "Beta Version: expect bugs and missing features. Feedback welcome!",
        ]),
    ];

    private record ChangelogEntry(string Version, DateOnly Date, string Title, string[] Changes);
}
