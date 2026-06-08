# ETTerms

[![English](https://img.shields.io/badge/English-2ea043.svg)](README.md) [![繁體中文](https://img.shields.io/badge/%E7%B9%81%E9%AB%94%E4%B8%AD%E6%96%87-lightgrey.svg)](README.zh-TW.md)

> A native Windows terminal workspace (C# .NET 8 WinForms) — **SSH**, **Serial Port**, and **local Shell (ConPTY)** in one window, with a **TTL scripting engine** ported from MyTeraTerm for automation, plus an optional **Serial MCP server** that lets AI agents (Kiro CLI / Claude CLI) drive the serial port directly. Standalone, no cloud, no login.

![version](https://img.shields.io/badge/version-0.3.1-blue.svg) ![platform](https://img.shields.io/badge/platform-Windows-0078D6.svg?logo=windows&logoColor=white) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg?logo=dotnet&logoColor=white) ![UI](https://img.shields.io/badge/UI-WinForms-5C2D91.svg) ![SSH](https://img.shields.io/badge/SSH-SSH.NET-success.svg) ![Serial](https://img.shields.io/badge/Serial-System.IO.Ports-success.svg) ![status](https://img.shields.io/badge/status-beta-yellow.svg) ![license](https://img.shields.io/badge/license-MIT-green.svg)

---

## 📖 Table of Contents

- [✨ Features](#-features)
- [🖼️ Layout](#️-layout)
- [💻 System Requirements](#-system-requirements)
- [📥 Installation](#-installation)
- [🚀 Quick Start](#-quick-start)
- [📚 Usage Guide](#-usage-guide)
- [🤖 TTL Scripting](#-ttl-scripting)
- [👥 Group Sync Execution](#-group-sync-execution)
- [⚡ PDU Power Control](#-pdu-power-control)
- [🤖 AI / MCP Integration](#-ai--mcp-integration)
- [🔐 Data & Security](#-data--security)
- [🔧 Troubleshooting](#-troubleshooting)
- [🔨 Building from Source](#-building-from-source)
- [📁 Project Structure](#-project-structure)
- [🤝 Contributing](#-contributing)
- [📜 Version History](#-version-history)
- [📄 License](#-license)
- [🙏 Acknowledgments](#-acknowledgments)

---

## ✨ Features

### Core Functionality

- 🖥️ **Multiple protocols in one workspace**
  - **SSH** (`SSH.NET`): password / private key / keyboard-interactive auth, with built-in SFTP
  - **Serial Port** (`System.IO.Ports`): configurable COM port, baud, data bits, parity, stop bits, handshake
  - **Local Shell** (Windows ConPTY): launch PowerShell / Cmd / Bash directly

- 🪟 **Tiling tabbed workspace**
  - One-click grid layouts: `1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3`
  - Per-pane manual split: `↔` horizontal / `↕` vertical, drag splitters to resize proportionally
  - Each pane hosts its own mini tab strip for multiple connections

- 🗂️ **Editable connection sidebar (Saved Connections)**
  - Nested folders with connection-count badges, expand / collapse all
  - Live filter by name / host, matching branches auto-expand
  - Context menu & toolbar: add / rename / delete, drag to re-categorize
  - Double-click to open in the active pane; ad-hoc quick connect (not saved)

- 🤖 **TTL script automation**
  - TTL (Tera Term Language) interpreter ported and extended from MyTeraTerm
  - Runs `.ttl` against the active session, showing file / line / current command live
  - Supports `send` / `sendln` / `wait` / `if` / `while` / variable math / file logging
  - Long-running `wait` / `while` can be aborted anytime with **■ Stop**

- 👥 **Group sync execution**
  - Assign tabs to Group 1/2/3 and run one script across the whole group at once
  - Sync commands: `waitall` (barrier), `sendlnall` (send after all arrive), `sendlngroup` (target a member)

- ⚡ **PDU power control (optional)**
  - Control PDU outlets over SNMP (`SnmpSharpNet`) to power-cycle devices during tests
  - Script commands: `pduconnect` / `pductrl`

- 🤖 **AI / MCP integration (optional)**
  - The **GUI owns the COM port**; a standalone stdio **Serial MCP server** bridges to it over a local named pipe for **Kiro CLI / Claude CLI**
  - Tools: `serial_list` / `serial_attach` / `serial_write` / `serial_read` / `serial_detach`
  - AI's serial TX/RX shows live in the GUI tagged `[AI]` — open the port in the GUI first, then let the AI attach
  - **One-click setup** in **Settings → AI MCP**: register the server into Claude Code / Kiro with a single button

- 📊 **Session RX logging**
  - `logopen` / `logwrite` / `logclose` write session output to file

### Terminal Rendering

- 🎨 Owner-drawn VT100 / ANSI control, double-buffered cell grid
- 🌑 KKTerm-style dark theme (incl. DWM dark title bar + dark terminal scrollbar)
- 🔤 Configurable font / size / palette / scrollback
- 🖱️ Scroll back through long output with the mouse wheel or the dark scrollbar
- 📋 Select / copy / paste — right-click copy clears the highlight; multi-line paste uses **bracketed paste** so it isn't submitted line-by-line

---

## 🖼️ Layout

```
┌──────┬─────────────────────┬───────────────────────────────────────┐
│ ▣ T  │ Saved Connections   │  Workspace (tiling)                    │
│ ▣ S  │  🔍 search          │  ┌─────────────────┬─────────────────┐ │
│ ▣ ⚙  │  ▾ 📁 Servers (2)   │  │ [tab1][tab2] +  │ [tab1] +        │ │
│      │     ▸ SSH srv-01    │  │                 │                 │ │
│ icon │     ▸ SSH nas       │  │   TerminalView  │   TerminalView  │ │
│ rail │  ▾ 📁 Boards (2)    │  │   (owner-drawn) │   (owner-drawn) │ │
│      │     ▸ COM3 @115200  │  │                 │                 │ │
│      │     ▸ COM7 @9600    │  ├─────────────────┴─────────────────┤ │
│      │                     │  │ [Group1-A]   ▶ Script  ■ Stop     │ │
└──────┴─────────────────────┴───────────────────────────────────────┘
  Activity Rail        Sidebar              Tabbed / Tiling Workspace
```

> The Activity Rail switches the three main views — **Terminal / Scripts / Settings**. The sidebar manages saved connections; the workspace holds multiple sessions across tabs and tiled panes.

---

## 💻 System Requirements

| Item | Requirement |
|------|-------------|
| OS | Windows 10 (1809+) / Windows 11 |
| Runtime | .NET 8 Desktop Runtime (`Microsoft.WindowsDesktop.App 8.0.x`) |
| Display | 1600×900 or higher recommended |
| Optional hardware | USB-to-Serial adapter (Serial), SNMP-capable PDU (power control) |

Check the runtime:

```powershell
dotnet --list-runtimes | findstr WindowsDesktop
```

---

## 📥 Installation

Source build for now (no binary release yet).

```powershell
git clone <repo-url> ETTerms
cd ETTerms
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj
```

Full steps in [Building from Source](#-building-from-source).

---

## 🚀 Quick Start

### Create an SSH connection

1. Launch ETTerms; on the left Activity Rail switch to **Terminal**
2. In the sidebar click **＋ New Connection** and pick type **SSH**
3. Enter `Host` / `Port` (default 22) / `Username`, choose auth (password / private key)
4. Double-click the connection → opens a tab in the active pane

### Create a Serial connection

1. Sidebar **＋ New Connection**, type **Serial**
2. Pick `COM port` and `BaudRate` (e.g. `COM3` / `115200`)
3. Double-click to open

### Run a TTL script

1. With a tab connected, click **▶ Script** on the script bar and pick a `.ttl`
2. The left status bar shows the running line and command live
3. Press **■ Stop** to abort if needed

---

## 📚 Usage Guide

### Activity Rail

Switches the three main views: **Terminal** (workspace), **Scripts** (edit & run), **Settings** (preferences).

### Connection Sidebar (Saved Connections)

| Action | How |
|--------|-----|
| Add folder / connection | Toolbar button or context menu |
| Rename / delete | Context menu |
| Categorize | Drag a connection or folder into a target folder (cannot drop into its own descendant) |
| Search | Top search box, live filter by name / host |
| Open connection | Double-click → opens in the active pane |

### Workspace Tiling

- One-click grid layouts: `1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3`
- Per-pane actions (top-right): `↔` split horizontally, `↕` split vertically, `＋` new tab, `✕` close
- Closing a pane collapses the split so siblings fill the space; at least one pane remains
- The active pane is outlined with an accent border

### Terminal

- Incoming channel bytes → ANSI parser → screen buffer → painted
- Keyboard input is encoded to byte sequences sent to the remote
- Supports scrollback, select / copy / paste; font and palette under **Settings**

---

## 🤖 TTL Scripting

On any connection tab's script bar, click **▶ Script** to load a `.ttl` and run it against that connection. Full syntax and examples in
[docs/ttl-script-reference.md](docs/ttl-script-reference.md); sample scripts in `tools/scripts/`.

### Supported Commands

| Command | Description |
|---------|-------------|
| `send 'text'` | Send text (no newline) |
| `sendln 'text'` | Send text plus `\r\n` |
| `wait 'string'` | Wait until the string appears in the RX buffer (infinite by default, cancellable via Stop); sets `result=1` on match |
| `flushrecv` | Clear the RX buffer |
| `pause seconds` | Pause N seconds (cancellable via Stop) |
| `timeout = seconds` | `wait` timeout; `0` = infinite, `N>0` aborts the script with an error on timeout |
| `if … then` / `elseif … then` / `else` / `endif` | Conditional branching (nestable) |
| `while …` / `endwhile` | Loop (nestable, cancellable via Stop) |
| `name = value` | Variable assignment; integer `+ - * /` and strings; built-in `result` |
| `logopen 'file'` | Open a log file (overwrite) |
| `logwrite 'text'` | Write one line to the log |
| `logclose` | Close the log (auto-closed at script end) |
| `messagebox 'msg'` | Show a topmost dialog |
| `; comment` | Inline comment (from `;` to end of line) |
| `:label` | Label line (skipped) |

**Comparison operators** (`if` / `elseif` / `while`): `>=` `<=` `>` `<` `==` `!=` `=`, or none (non-zero = true).

### Example: auto login

```ttl
; tools/scripts/login.ttl
timeout = 10
wait 'login: '
sendln 'root'
wait 'Password: '
sendln 'toor'
wait '# '
sendln 'uname -a'
```

---

## 👥 Group Sync Execution

Assign multiple tabs to one Group and run a single script across all of them — ideal for syncing multiple DUTs.

1. **Right-click a tab** → set as Group 1 / 2 / 3 (or clear); the cell footer shows a `[Group1-A]` label
2. Click **▶ Group1 / ▶ Group2 / ▶ Group3** on the toolbar to run across the whole group

| Command | Description |
|---------|-------------|
| `waitall 'string'` | All members wait for the keyword, then continue together (`System.Threading.Barrier`) |
| `sendlnall 'text'` | Each member sends once all have arrived |
| `sendlngroup N 'text'` | Only the specified member sends |

> ⚠️ Group sync commands work **only in Run Group mode**; `▶ Script` and `▶ Run All` reject scripts containing them and show a warning.

---

## ⚡ PDU Power Control

Control PDU outlets over SNMP to power-cycle a DUT from within a script.

```ttl
; connect PDU (device 2 = iPoMan II 1202, IP 192.168.1.21)
pduconnect 2 192.168.1.21

pductrl 2 1 0      ; turn port 1 off
pause 5
pductrl 2 1 1      ; turn port 1 on
wait 'login: '
```

| Command | Syntax | Description |
|---------|--------|-------------|
| `pduconnect` | `pduconnect <device> <ip>` | Connect to a PDU |
| `pductrl` | `pductrl <device> <port> <0\|1>` | Set a port off(0) / on(1) |

---

## 🤖 AI / MCP Integration

> Let AI agents (**Kiro CLI / Claude CLI**) send/receive on a serial port **while you watch it live in the ETTerms GUI**.

**Key design: the GUI owns the COM port; the MCP server never opens it.** A COM port can be opened by only one process at a time, so rather than the MCP server grabbing it, the GUI is the sole owner and runs a local **named pipe server** (`SerialBridgeServer`). The standalone `ETTerms.SerialMcp` (launched by Kiro/Claude CLI) is a thin client: every `write` / `read` is forwarded over the pipe to the GUI, which does the real port I/O. AI's TX is echoed into the terminal tagged `[AI]`, so RX/TX flow through the GUI channel and what you see is exactly what the AI sees.

Full closed loop — all inside the ETTerms GUI:
- **Tab 1:** a Serial session holding e.g. COM3 (shows traffic live; AI's writes tagged `[AI]`)
- **Tab 2:** a PowerShell (ConPTY) tab running `kiro-cli`, which spawns `ETTerms.SerialMcp`, which connects back to the pipe

| Tool | Params | Description |
|------|--------|-------------|
| `serial_list` | — | List serial sessions currently **open in the GUI** (name + baud) |
| `serial_attach` | portName | Bind to a GUI serial session by COM name (required before write/read) |
| `serial_write` | text, appendNewline? | Send text via the GUI (shown live tagged `[AI]`) |
| `serial_read` | waitFor?, timeoutMs? | Drain accumulated RX; optionally wait for a substring / timeout |
| `serial_detach` | — | Unbind (does **not** close the GUI's port) |

### One-click setup (recommended)

In **Settings → AI MCP**, click **Setup** on the Claude Code or Kiro card to register the Serial MCP server into that CLI's user-level config (`~/.claude.json` / `~/.kiro/settings/mcp.json`) with a single button. Your other MCP servers are preserved, and each card shows the CLI command to verify the connection. **Remove** unregisters it.

### Manual setup

```powershell
kiro-cli mcp add --name serial --command dotnet --args "run --project src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj"
```

Or add it to `agent.json` under `mcpServers` (Claude CLI uses an equivalent `mcpServers` config). **Open the target serial connection in the ETTerms GUI first**, then tell the AI: *"list serial sessions, attach to COM3, send `AT` and read the reply."*

> ⚠️ The COM port is owned solely by the GUI. The MCP server attaches over the pipe — it never opens the port itself, so there's no GUI-vs-server conflict.

📖 Step-by-step setup: [docs/serial-mcp-guide.md](docs/serial-mcp-guide.md).

---

## 🔐 Data & Security

- **Standalone, no cloud:** connection metadata is stored in a local SQLite file (`%LocalAppData%\ETTerms\ettermsdb.sqlite`); no telemetry is sent.
- **Passwords never stored in plaintext:** SQLite holds only a `CredentialKey` pointing to **Windows Credential Manager**; actual passwords / key passphrases are accessed via `CredentialVault`.
- **SSH host keys:** the fingerprint is shown for confirmation on first connect (trust-on-first-use), then compared; mismatches warn.
- **Logs contain no secrets:** `AppLogger` and `logopen` record only host / port, never credentials.
- **No `secret/` directory:** a single-user desktop app with no server-side secrets, DB passwords, or compile-time secrets.

---

## 🔧 Troubleshooting

| Issue | Likely cause | Fix |
|-------|--------------|-----|
| Startup reports missing runtime | .NET 8 Desktop Runtime not installed | Install `Microsoft.WindowsDesktop.App 8.0.x`; verify with `dotnet --list-runtimes` |
| Serial connect fails / port busy | COM port held by another app | Close other terminal apps; a COM port can be opened by only one session at a time |
| SSH auth fails | Wrong credentials / key or host-key mismatch | Check auth settings; verify the host-key fingerprint |
| Script stuck on `wait` | Expected string never received | Check connection & baud, verify the `wait` string; set `timeout` or press ■ Stop |
| Group script rejected | Ran a script with Group commands via `▶ Script` | Use **▶ GroupN** instead |

More runbooks in [docs/runbooks/](docs/runbooks/).

---

## 🔨 Building from Source

### Prerequisites

- .NET 8 SDK (or SDK 9/10 + .NET 8 Desktop Runtime)
- Visual Studio 2022 or VS Code + C# extension
- Git

### Build & Run

```powershell
dotnet --list-sdks
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj
```

### Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `SSH.NET` | SSH shell + SFTP |
| `System.IO.Ports` | Serial connections |
| `Microsoft.Data.Sqlite` | Connection metadata storage |
| `SnmpSharpNet` | PDU control (optional) |

### Publish (self-contained=false)

```powershell
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained false
```

---

## 📁 Project Structure

```
ETTerms/
├── README.md                 # English (default)
├── README.zh-TW.md           # Traditional Chinese
├── ARCHITECTURE.md           # Full architecture doc
├── CLAUDE.md                 # Project memory & dev commands
├── ETTerms.slnx
├── docs/
│   ├── ttl-script-reference.md   # TTL command reference
│   └── runbooks/                 # Runbooks / troubleshooting
├── tools/scripts/            # Sample .ttl scripts
├── src/ETTerms/              # Main application (WinForms)
│   ├── App/                  # Window shell: MainForm / ActivityRail / ConnectionSidebar / Workspace
│   ├── Terminal/             # Owner-drawn VT100: TerminalView / AnsiParser / ScreenBuffer / TerminalInput
│   ├── Sessions/             # Connection abstraction: ISessionChannel / SshChannel / SerialChannel / ShellChannel
│   ├── Connections/          # Connection data: Connection / ConnectionStore(SQLite) / CredentialVault
│   ├── Scripting/            # TTL engine: TTLInterpreter / ScriptRunner / GroupSyncContext / Pdu
│   └── Infrastructure/       # AppLogger / AppSettings / McpRegistrar / NativeTheme
└── src/ETTerms.SerialMcp/    # Serial MCP server (stdio) — lets AI agents drive the serial port
```

**Core design:** every connection implements `ISessionChannel` (`Write(byte[])` + `event DataReceived`). `TerminalView` and `TTLInterpreter` only know this abstraction, so SSH / Serial / Shell look identical to upper layers — the key to "one script engine driving multiple connection types." See [ARCHITECTURE.md](ARCHITECTURE.md).

---

## 🤝 Contributing

1. Fork and create a feature branch: `git checkout -b feature/your-feature`
2. Follow [Conventional Commits](https://www.conventionalcommits.org/): `feat(serial): add auto-reconnect`
3. Push and open a Pull Request

**Conventions:** PascalCase types / methods, `_camelCase` private fields, filename = class name; the UI layer depends only on the `ISessionChannel` abstraction; channel I/O runs in the background and all UI updates go back to the UI thread via `Control.Invoke`.

---

## 📜 Version History

### v0.3.1

- **Terminal usability fixes** — added a **dark vertical scrollbar** (drag or click-to-page through long output instead of long mouse-wheel scrolling)
- **Multi-line paste** into Kiro CLI / PSReadLine now pastes as a single block (**bracketed paste**, DEC mode 2004) instead of submitting each line immediately
- Right-click copy now **clears the highlight** so you know it worked

### v0.3.0

- **AI-driven PDU power control (MCP)** — new `ETTerms.PduMcp` server lets AI agents (Kiro CLI / Claude CLI) control an SNMP PDU directly: outlets on/off, status, and power-cycle a DUT during automated tests
- PDU runs over SNMP (UDP, non-exclusive), so the AI talks to it directly — the ETTerms GUI does not need to be running
- Tools: `pdu_connect` / `pdu_list` / `pdu_set_port` / `pdu_get_port` / `pdu_status` / `pdu_power_cycle` / `pdu_disconnect`
- **Settings → AI MCP** now registers both `etterms-serial` and `etterms-pdu` with one click; publishing the GUI auto-bundles both MCP servers

### v0.2.2

- **Terminal stability** — the terminal no longer freezes after minimizing or switching tabs (most noticeable with full-screen TUIs like Kiro CLI in PowerShell); a degenerate 1×1 size is no longer sent to the pseudo-console
- **Shift+Enter** inserts a newline in the shell, so you can type multi-line commands (plain Enter still submits)
- **High-DPI fixes** — text and buttons no longer clipped at 125% / 150% scaling (PerMonitorV2); toolbar / settings / sidebar / About now scale adaptively

### v0.2.1

- **AI MCP one-click setup** — new **Settings → AI MCP** tab registers the Serial MCP server into Claude Code (`~/.claude.json`) or Kiro (`~/.kiro/settings/mcp.json`) with a single button; existing MCP servers are preserved, and each card shows the verify command
- Publish now auto-bundles `ETTerms.SerialMcp` into the app folder, so the registered path always resolves

### v0.2.0

- **Phase 9: Serial MCP server** (`ETTerms.SerialMcp`) — AI agents (Kiro CLI / Claude CLI) send/receive on the serial port while you watch it live in the GUI
- GUI is the sole COM-port owner; the MCP server bridges over a local named pipe (`SerialBridgeServer`) and never opens the port itself
- Tools: `serial_list` / `serial_attach` / `serial_write` / `serial_read` / `serial_detach`; AI's TX echoed into the terminal tagged `[AI]`
- New app icon (window / taskbar / executable); version shown in the title bar

### v0.1.0

- Phases 1–8 complete: window shell, connection sidebar, tiling workspace, Serial / SSH / local Shell (ConPTY) / SFTP
- Owner-drawn VT100 terminal rendering
- TTL scripting engine (ported from MyTeraTerm) + Group sync execution
- PDU power control (SNMP)
- Settings / About

---

## 📄 License

Licensed under the **MIT License** — see [LICENSE](LICENSE).

---

## 🙏 Acknowledgments

- **[KKTerm](https://github.com/ryantsai/KKTerm)** — by ryantsai (MIT) — UI design reference (Activity Rail + tabbed workspace + Saved Connections)
- **MyTeraTerm** — source of the TTL scripting engine and `AppLogger`
- **[SSH.NET](https://github.com/sshnet/SSH.NET)**, **[SnmpSharpNet](http://www.snmpsharpnet.com/)** — open-source connectivity / SNMP libraries
- **Microsoft** — .NET 8, WinForms, ConPTY, Credential Manager
