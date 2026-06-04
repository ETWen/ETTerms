# ETTerms — Architecture

> A native Windows terminal workspace for **SSH** and **Serial Port** connections,
> with a TeraTerm-compatible **TTL scripting engine**.
> UI inspired by **KKTerm** (activity rail + tabbed session workspace + saved-connection sidebar);
> script engine ported and extended from **MyTeraTerm** (`TTLInterpreter`).

---

## Overview

ETTerms 是一個給工程師 / 韌體 / 硬體驗證人員用的**單一視窗終端機工作台**。它把日常會用到的兩種連線——**SSH**（連伺服器 / 嵌入式 Linux）與 **Serial Port**（連 UART / console / 開發板）——收進同一個 WinForms 視窗裡，用分頁（Tab）管理多條連線，左側有可存檔的連線清單（Saved Connections）。

核心差異化價值是**腳本自動化**：沿用並擴充 MyTeraTerm 既有的 **TTL（Tera Term Language）直譯器**，讓使用者既有的 `.ttl` 腳本（`send` / `sendln` / `wait` / `if` / `while` / `logopen` / `sprintf2` / `pductrl`…）可以直接重用，直接驅動原生 SSH / Serial channel，做到登入自動化、批次下命令、log 收集、PDU 電源控制等。

開發策略採「**GUI 先行**」：先把 KKTerm 風格的視窗外殼與分頁工作區做出來（可見、可切換、可關閉），再逐步把 Serial → SSH → VT100 渲染 → 腳本引擎一層層補上。

**目標使用者：** 單機桌面使用者（無多人 / 無登入系統）。所有連線資料存在本機 SQLite，密碼存在 Windows Credential Manager，不上雲、不回傳。

---

## Tech Stack

| Layer | Technology | 備註 |
|-------|-----------|------|
| UI Framework | **C# .NET 8 WinForms**（`net8.0-windows`） | 與 MyTeraTerm 同框架；本機已裝 .NET 8 Desktop Runtime + SDK 9/10（net8 targeting pack 自動還原）|
| SSH | **SSH.NET**（`Renci.SshNet`） | 原生 SSH，支援 password / key / keyboard-interactive |
| Serial | **System.IO.Ports** | 沿用 MyTeraTerm `ComPortBridge` 經驗 |
| Terminal 渲染 | **自繪 VT100 / ANSI 控制項**（owner-drawn `Control`） | 解析 ANSI escape，雙緩衝繪字格 |
| Script 引擎 | **TTLInterpreterLib**（從 MyTeraTerm 移植 + 擴充） | 改為驅動 `ISessionChannel` 而非 com0com bridge |
| 連線儲存 | **SQLite**（`Microsoft.Data.Sqlite`） | 取代 KKTerm 的 SQLite store；存連線 metadata |
| 祕密儲存 | **Windows Credential Manager**（DPAPI / CredMan） | 連線密碼、SSH key passphrase，不落地明碼 |
| PDU 控制（選用） | **SnmpSharpNet** | 沿用 MyTeraTerm PDU 控制（`pductrl` / `pduconnect`） |
| 日誌 | 自製 **AppLogger**（從 MyTeraTerm 移植） | 檔案 + Debug 雙輸出 |
| AI / MCP 整合（選用） | **stdio MCP server**（官方 C# SDK `ModelContextProtocol`） | 獨立行程，但不自己開 port——經本機 named pipe 橋接到 GUI 持有的 serial session，把收發暴露成 AI 可呼叫工具（Kiro CLI / Claude CLI），見 [AI / MCP Integration](#ai--mcp-integrationserial-mcp-server) |
| 打包 | `dotnet publish` + （選用）Inno Setup / MSIX | 單機安裝，current-user |

> **與舊版 MyTeraTerm 的關鍵差異：** 舊版是把真正的 `ttermpro.exe`（TeraTerm）嵌進 Panel，靠 **com0com 虛擬 COM 對**攔截 serial 來跑腳本。ETTerms 改走**全原生**：SSH.NET 做 SSH、`System.IO.Ports` 做 serial、自繪 VT100 控制項做終端機畫面，**不再依賴外部 TeraTerm exe，也不再需要 com0com**。腳本引擎從「驅動 com0com bridge」改成「驅動原生 `ISessionChannel`」。

---

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│  MainForm (WinForms Shell)                                             │
│                                                                        │
│  ┌────────────┐ ┌──────────────────┐ ┌────────────────────────────┐  │
│  │ Activity   │ │ Connection       │ │  Tabbed Workspace          │  │
│  │ Rail       │ │ Sidebar          │ │  (一個 Tab = 一條 Session) │  │
│  │ (左側圖示) │ │ (Saved           │ │                            │  │
│  │            │ │  Connections)    │ │  ┌──────────────────────┐  │  │
│  │ ▣ Terminal │ │  ▸ SSH: srv-01   │ │  │ TerminalView         │  │  │
│  │ ▣ Scripts  │ │  ▸ SSH: nas      │ │  │ (自繪 VT100 控制項)  │  │  │
│  │ ▣ Settings │ │  ▸ COM3 @115200  │ │  │                      │  │  │
│  │            │ │  ▸ COM7 @9600    │ │  │  bytes ↑↓            │  │  │
│  └────────────┘ └──────────────────┘ │  └──────────┬───────────┘  │  │
│                                       └─────────────┼──────────────┘  │
└─────────────────────────────────────────────────────┼─────────────────┘
                                                       │
                          ┌────────────────────────────┼───────────────┐
                          │  ISessionChannel (抽象)     │               │
                          │   ├─ SshChannel  (SSH.NET) ─┘               │
                          │   └─ SerialChannel (System.IO.Ports)        │
                          └──────────────┬──────────────────────────────┘
                                         │ Write(bytes) / DataReceived(bytes)
                          ┌──────────────┴──────────────────────────────┐
                          │  ScriptEngine (TTLInterpreter, ported)       │
                          │   send / sendln / wait / if / while /        │
                          │   logopen / messagebox / pductrl ...         │
                          └──────────────┬──────────────────────────────┘
                                         │ (選用)
                                  ┌──────┴───────┐
                                  │ SNMP PDU     │ (SnmpSharpNet)
                                  └──────────────┘
```

**資料流核心抽象：** 所有連線都實作 `ISessionChannel`（`Write(byte[])` + `event DataReceived`）。`TerminalView` 與 `ScriptEngine` 都只認得這個抽象，因此 SSH 與 Serial 對上層完全一致——這是讓「同一套腳本引擎驅動兩種連線」的關鍵設計。

---

## Project Structure

```
ETTerms/
│
├── CLAUDE.md                       # 專案記憶 & 給 Claude 的指令
├── README.md                       # 快速上手、打包說明
├── ARCHITECTURE.md                 # 本文件
├── .gitignore                      # 含 For_AI/ 與 build 產物規則
├── ETTerms.sln
│
├── docs/
│   ├── architecture.md             # （本文件副本 / 連結）
│   ├── ttl-script-reference.md     # TTL 指令對照表（移植自 MyTeraTerm）
│   ├── decisions/                  # ADR：為何走原生而非嵌 TeraTerm
│   └── runbooks/                   # 操作手冊、常見問題（無實際密碼）
│
├── .claude/
│   ├── settings.json
│   └── skills/
│
├── tools/
│   ├── scripts/                    # 建置 / 打包腳本
│   └── prompts/
│
├── For_AI/                         # 🚫 gitignored — AI 協作素材
│   ├── KKTerm-main/                #   UI 參考專案（Tauri/React 工作台）
│   └── MyTeraTerm/                 #   Script 參考專案（舊版 WinForms）
│
└── src/
    ├── ETTerms/                    # 主應用程式（WinForms）
        │
        ├── Program.cs              # 進入點
        ├── ETTerms.csproj          # net8.0-windows, UseWindowsForms
        │
        ├── App/                    # ── 視窗外殼 (UI Shell) ──
        │   ├── MainForm.cs         #   主視窗：Rail + Sidebar + Workspace（含深色標題列 DWM）
        │   ├── MainForm.Designer.cs
        │   ├── Theme.cs            #   全域深色配色 (KKTerm 風格)
        │   ├── ActivityRail.cs     #   左側圖示列 (Terminal/Scripts/Settings)
        │   ├── ConnectionSidebar.cs#   仿 KKTerm 可編輯資料夾樹（搜尋/CRUD/拖曳分類）
        │   ├── Dialogs/            #   ── 深色對話框 ──
        │   │   ├── DarkDialog.cs       #   對話框基底（深色 + DWM 標題列）
        │   │   ├── TextPromptDialog.cs #   單行輸入（資料夾命名 / 改名）
        │   │   └── ConnectionEditDialog.cs # 連線新增/編輯（名稱+類型+詳細，Phase 2 擴充）
        │   └── Workspace/          #   ── 可平鋪 (tiling) 的工作區 ──
        │       ├── WorkspaceView.cs    #   工具列(grid 預設) + split 樹管理 + active pane 路由
        │       ├── PaneControl.cs      #   單一格子：自繪迷你分頁列 + 內容 + ＋↔↕✕ 動作鈕
        │       └── ProportionalSplit.cs#   按比例縮放的 SplitContainer (巢狀組成任意佈局)
        │
        ├── Terminal/               # ── 終端機渲染 ──
        │   ├── TerminalView.cs     #   自繪 VT100 控制項 (owner-drawn)
        │   ├── AnsiParser.cs       #   ANSI/VT100 escape 解析狀態機
        │   ├── ScreenBuffer.cs     #   字格緩衝 (rows×cols, 屬性/顏色)
        │   └── TerminalInput.cs    #   鍵盤 → byte 序列 (含特殊鍵)
        │
        ├── Sessions/               # ── 連線抽象 ──
        │   ├── ISessionChannel.cs  #   Write(byte[]) + event DataReceived
        │   ├── SshChannel.cs       #   SSH.NET 實作 (ShellStream)
        │   ├── SerialChannel.cs    #   System.IO.Ports 實作
        │   ├── ShellChannel.cs     #   Windows ConPTY 本機 Shell
        │   ├── SessionPage.cs      #   一個分頁 = TerminalView + Channel + 狀態
        │   ├── SessionManager.cs   #   開 / 關 / 列舉所有 active session
        │   ├── SerialBridgeServer.cs# ✅ 本機 named pipe server：把 serial session 的讀寫橋接給 MCP（Phase 9）
        │   └── SerialBridge.cs     # ✅ SerialBridgeEndpoint：單一 serial session 與 pipe 的橋接點
        │
        ├── Connections/            # ── 連線資料 ──
        │   ├── Connection.cs       #   連線 metadata 模型
        │   ├── ConnectionStore.cs  #   SQLite CRUD
        │   └── CredentialVault.cs  #   Windows Credential Manager 封裝
        │
        ├── Scripting/              # ── TTL 腳本引擎（移植 + 擴充）──
        │   ├── TTLInterpreter.cs   #   主直譯器 (port 自 MyTeraTerm)
        │   ├── ScriptRunner.cs     #   非同步執行 + 取消 + 進度事件
        │   ├── GroupSyncContext.cs  #   Group 同步 (Barrier)：waitall / sendlnall / sendlngroup
        │   └── Pdu/
        │       └── PduController.cs#   SnmpSharpNet PDU 控制 (pductrl / pduconnect)
        │
        └── Infrastructure/
            ├── AppLogger.cs        #   日誌 (port 自 MyTeraTerm)
            ├── AppSettings.cs      #   使用者偏好 (JSON, %LocalAppData%\ETTerms\settings.json)
            └── NativeTheme.cs      #   深色標題列 (DWM)
    │
    └── ETTerms.SerialMcp/          # ✅ Serial MCP server（stdio）——不自己開 port，經 named pipe 橋接 GUI
        ├── Program.cs              #   stdio MCP host 進入點
        ├── SerialBridgeClient.cs   #   連 GUI 的 named pipe，轉發 write / 接收 RX
        ├── SerialTools.cs          #   serial_list / attach / write / read / detach 工具（轉發到 pipe）
        └── ETTerms.SerialMcp.csproj#   net8.0 console + ModelContextProtocol SDK
```

> **`For_AI/` 內含兩份參考專案**：`KKTerm-main`（UI 參考，Tauri+React 的 Windows 工作台）與 `MyTeraTerm`（Script 參考，舊版嵌 TeraTerm 的 WinForms）。整個 `For_AI/` 已 gitignore，僅供開發時對照，不進 repo。

> **無 `secret/` 資料夾：** ETTerms 是桌面單機 App，**沒有 DB 密碼 / 連線字串 / API key 之類的伺服端祕密，也無 compile-time secret**。連線密碼一律存 Windows Credential Manager（不落地明碼），因此不需要 `secret/` 集中管理機制。

---

## Data Models

連線 metadata 存在本機 SQLite（`%LocalAppData%\ETTerms\ettermsdb.sqlite`）。**密碼 / passphrase 不存在這裡**，只存一個指向 Windows Credential Manager 的 `CredentialKey`。

```csharp
public enum ConnectionType
{
    Ssh = 0,
    Serial = 1
}

// 主連線模型
public class Connection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";        // 顯示名稱，例：srv-01 / COM3 board
    public ConnectionType Type { get; set; }
    public int SortOrder { get; set; }              // sidebar 排序
    public string? GroupName { get; set; }          // 選用：分組 (folder)
    public DateTime LastUsedUtc { get; set; }

    // SSH 專用（Type == Ssh 時有效）
    public SshSettings? Ssh { get; set; }
    // Serial 專用（Type == Serial 時有效）
    public SerialSettings? Serial { get; set; }

    // 指向 Windows Credential Manager 的 key，例："ETTerms/{Id}"
    // 明碼密碼絕不存進 SQLite
    public string? CredentialKey { get; set; }
}

public class SshSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public SshAuthMethod AuthMethod { get; set; }   // Password / PrivateKey / KeyboardInteractive
    public string? PrivateKeyPath { get; set; }     // key 檔路徑（passphrase 走 CredentialVault）
}

public enum SshAuthMethod { Password = 0, PrivateKey = 1, KeyboardInteractive = 2 }

public class SerialSettings
{
    public string PortName { get; set; } = "COM1";  // COM3, COM7...
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;     // System.IO.Ports.Parity
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public string NewLine { get; set; } = "\r\n";   // 送出換行序列
}

// 終端機偏好（存 AppSettings，非每連線）
public class TerminalProfile
{
    public string FontFamily { get; set; } = "Cascadia Mono";
    public float FontSize { get; set; } = 11f;
    public int Cols { get; set; } = 80;
    public int Rows { get; set; } = 24;
    public string Theme { get; set; } = "dark";      // 配色名稱
    public int ScrollbackLines { get; set; } = 5000;
}
```

**SQLite Schema（單表即可起步）：**

| 欄位 | 型別 | 說明 |
|------|------|------|
| `Id` | TEXT (GUID) | 主鍵 |
| `Name` | TEXT | 顯示名稱 |
| `Type` | INTEGER | 0=Ssh, 1=Serial |
| `SortOrder` | INTEGER | sidebar 排序 |
| `GroupName` | TEXT NULL | 分組 |
| `LastUsedUtc` | TEXT | ISO8601 |
| `SettingsJson` | TEXT | `SshSettings` / `SerialSettings` 序列化 |
| `CredentialKey` | TEXT NULL | Credential Manager 索引鍵 |

---

## Authentication & Authorization

**不適用。** ETTerms 是單機桌面工具，沒有使用者帳號 / 登入 / 角色系統。

唯一相關的「認證」是**對外連線時的 SSH 認證**（password / private key / keyboard-interactive），其憑證透過 **Windows Credential Manager** 儲存與讀取，由 `CredentialVault.cs` 封裝。詳見 [Security Considerations](#security-considerations)。

---

## Key Pages / Features

ETTerms 是單視窗多分頁，沒有「路由」，以下以**功能面板**為單位描述。

### Activity Rail（左側圖示列）
- 切換主檢視：**Terminal**（連線工作區）/ **Scripts**（腳本編輯與執行）/ **Settings**（偏好設定）
- 仿 KKTerm 的 ActivityRail，hover 顯示 tooltip

### Connection Sidebar（Saved Connections，仿 KKTerm 資料夾樹）
- **使用者可自建資料夾**，把連線分類組織成任意層的目錄樹（資料夾可巢狀）
- 每個資料夾顯示**連線數量徽章**、可展開 / 收合（工具列有「全部展開 / 收合」）
- **搜尋框**：依名稱 / 主機即時過濾，命中的分支自動展開
- 圖示區分 SSH / Serial，顯示主機或 COM port + baud
- 操作（右鍵選單 + 工具列按鈕）：新增資料夾 / 新增連線 / 重新命名 / 刪除
- **拖曳分類**：把連線或資料夾拖進別的資料夾（禁止拖進自己的子孫）
- 雙擊連線 → 在 **active pane** 開啟；「快速連線」→ 不存檔的 ad-hoc 連線
- Phase 1 資料存記憶體；**Phase 2 換成 `ConnectionStore`（SQLite）持久化**

### Workspace（可平鋪 tiling 的工作區）
- 工作區可分割成多個 **pane（格子）**，自由排列（仿 KKTerm grid）：
  - **Grid 預設**：工具列一鍵切 `1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3`
  - **手動分割**：每個 pane 右上角 `↔`（左右分割）/ `↕`（上下分割），拖格線（`ProportionalSplit`）按比例調整大小
  - 關閉某格時，兄弟節點自動補位（collapse split）；保留至少一格
- **每個 pane 內可再開多條連線分頁**（pane 自己的迷你 tab strip）
- **active pane** 以強調色外框標示；側欄雙擊連線會開進 active pane
- 一條連線分頁 = `SessionPage`（`TerminalView` + `ISessionChannel`，Phase 3+ 接上）

### Terminal View（自繪 VT100）
- 接收 channel bytes → `AnsiParser` → `ScreenBuffer` → 繪製
- 鍵盤輸入 → `TerminalInput` → byte 序列 → channel
- 支援：scrollback、選取 / 複製、貼上、字型 / 配色（從 `TerminalProfile`）

### Script Editor / Runner（Scripts 檢視）
- 載入 / 編輯 `.ttl` 腳本（語法沿用 MyTeraTerm）
- 對「目前 active session」執行腳本；顯示執行進度（檔名 / 行號 / 當前指令）
- 可取消執行（`Cancel()`）；`logopen` 將輸出寫檔
- `sprintf2`：C `printf` 風格格式化輸出到變數（TeraTerm 相容）
- `wait`：命中關鍵字後會等裝置安靜（`SettleMs`，預設 300ms）再接受並取最後一次出現，避免比對到輸出中途的指令回顯
- 選用：PDU 控制指令（`pductrl` / `pduconnect`）走 SNMP

### Group 同步執行
- 分頁可透過**右鍵 Tab** → 設為 Group 1 / 2 / 3（或取消），cell footer 顯示 `[Group1-A]` 標籤
- Toolbar 的 `▶ Group1` / `▶ Group2` / `▶ Group3` 按鈕對整個 Group 同時跑同一份 `.ttl` 腳本
- Group 模式支援同步指令：`waitall`（全員 wait 到關鍵字再繼續）、`sendlnall`（全員到齊後各自 sendln）、`sendlngroup`（指定 member 才送）
- `▶ Run All` 和分頁 `▶ Script` 會拒絕含 Group 指令的腳本（彈 Warning）
- 同步機制使用 `System.Threading.Barrier`（`GroupSyncContext`），確保成員在同步點等齊

### Settings
- 終端機字型 / 字級 / 配色 / scrollback 行數
- 預設換行序列、編碼
- 視窗位置記憶

---

## Data Flow

**SSH session 端到端：**

```
使用者雙擊 Sidebar 連線
  → SessionManager.Open(connection)
  → CredentialVault.Get(connection.CredentialKey)         讀密碼
  → new SshChannel(SshSettings, credential)
      → SSH.NET SshClient.Connect()
      → ShellStream 建立
  → new SessionPage(TerminalView, channel)  加入 WorkspaceTabs

執行期雙向資料流：
  鍵盤 → TerminalInput → bytes → SshChannel.Write() → ShellStream
  ShellStream → SshChannel.DataReceived(bytes) → TerminalView
      → AnsiParser → ScreenBuffer → Invalidate() → 繪製
```

**腳本驅動流（與互動式共用同一 channel）：**

```
ScriptRunner.RunAsync(scriptText, activeChannel)
  → TTLInterpreter(channel)
      send/sendln  → channel.Write(bytes)
      wait "xxx"   → 監聽 channel.DataReceived，比對到關鍵字後再等裝置「安靜」(~300ms 無新資料) 才接受，
                     並消費到該字串「最後一次」出現處 → 避免命中輸出中途的指令回顯（如 SVOS> help）
      sprintf2     → C printf 風格格式化字串存入變數（與 TeraTerm 相容，設定 result）
      logopen/write→ StreamWriter 寫檔
      pductrl      → PduController（SNMP set）→ PDU
      if/while     → 依 result / 變數做流程控制
  → StatusChanged 事件 → UI 顯示「檔名 第N行 指令」
```

**Serial session：** 與 SSH 相同，只是 `ISessionChannel` 換成 `SerialChannel`（`System.IO.Ports.SerialPort` 的 `DataReceived` / `Write`）。上層 `TerminalView` 與 `TTLInterpreter` 完全不需改動——這正是 `ISessionChannel` 抽象的價值。

---

## AI / MCP Integration（Serial MCP Server）

> 讓 **Kiro CLI / Claude CLI** 等 AI agent 收發 serial，**且使用者能在 ETTerms GUI 即時看到 AI 的每筆收發**。
>
> **關鍵設計：COM port 由 GUI 唯一持有，MCP server 不自己開 port。** 一個 COM port 同一時間只能被一個行程開啟；若讓 MCP server 自己開，GUI 就無法同時開、使用者也看不到。因此改成 **GUI 當 port 的唯一擁有者**，在 GUI 內跑一支本機 **named pipe server**（`SerialBridgeServer`）；獨立的 `ETTerms.SerialMcp`（由 Kiro/Claude CLI 啟動）退化成**瘦客戶端**，所有 `write` / `read` 都經 pipe 轉發給 GUI，由 GUI 代為讀寫實體 port。

### 全閉迴路（推薦用法）

整個迴路可全部跑在 ETTerms GUI 內，使用者一邊看、AI 一邊操作：

```
ETTerms GUI（單一行程，唯一開 COM3 的人）
│
├─ Tab1: Serial 連線 ── SerialChannel 實體持有 COM3
│        TerminalView 即時顯示（AI 的 TX 以 [AI] 標色，RX 照常顯示）
│
├─ SerialBridgeServer（本機 named pipe: \\.\pipe\etterms-serial）
│        ▲  write 轉發 / RX 廣播
│        │
└─ Tab2: PowerShell (ConPTY) 跑 kiro-cli
         └─ kiro 啟動子行程 ETTerms.SerialMcp（stdio / JSON-RPC）
                └─ 不開 COM，連上面的 pipe → 收發都流經 GUI 的 channel
```

**資料流：**
- **AI 送資料：** `serial_write` → pipe → GUI `SerialChannel.Write()` 送出 COM3，**同時把這段 echo 進 Tab1 TerminalView（`[AI]` 標色）** → 使用者看得到 AI 打了什麼。
- **裝置回資料：** COM3 RX → GUI 一邊顯示在 Tab1、一邊經 pipe 廣播 → `serial_read` 取出 → AI 讀到。

因為 RX/TX 物理上都流經 GUI 的 channel，**使用者在 GUI 看到的就是 AI 看到的**，完全同步。

### 暴露的工具

| 工具 | 參數 | 說明 |
|------|------|------|
| `serial_list` | — | 列出 **GUI 目前開著的 serial session**（名稱 + COM port + baud），供 AI 選定要操作哪一個 |
| `serial_attach` | portName \| sessionName | 經 pipe 綁定到 GUI 某個已開啟的 serial session（之後 read/write 都對它） |
| `serial_write` | text, appendNewLine? | 經 pipe 請 GUI 對綁定的 session 送出文字（GUI 同步 echo 到 TerminalView） |
| `serial_read` | waitFor?, timeoutMs? | 取出該 session 累積的 RX；可等待特定字串或逾時 |
| `serial_detach` | — | 解除綁定（**不關閉 GUI 的 port**，GUI 仍持有） |

> 與舊版規劃的差異：不再有「MCP 自己 `serial_open` / `serial_close` 實體 port」；改為 `serial_attach` / `serial_detach` 綁定 / 解除 GUI 既有 session。port 的開關一律在 GUI 操作。

### named pipe 協議（精簡）

- **傳輸：** Windows named pipe（`\\.\pipe\etterms-serial`），純本機、不開網路埠。
- **訊息：** 換行分隔的 JSON，例：`{"op":"write","session":"COM3","data":"...","newline":true}` / `{"op":"read","session":"COM3","waitFor":"OK","timeoutMs":3000}` / `{"op":"list"}`。
- **RX 推送：** GUI 主動把 RX 以 `{"op":"rx","session":"COM3","data":"..."}` 推給已連線的 client，client 端累積成 buffer 供 `serial_read` 消費。
- **找不到 session：** AI `attach` / `write` 一個 GUI 沒開的 port 時，回明確錯誤（要求使用者先在 GUI 開啟）。

### 設計重點

- **技術：** `ETTerms.SerialMcp` 為 .NET 8 console（`net8.0`，無 WinForms）+ 官方 C# MCP SDK（`ModelContextProtocol`），stdio / JSON-RPC；GUI 端的 `SerialBridgeServer` 用 `System.IO.Pipes`。
- **port 擁有權單一化：** 實體 `SerialPort` 只有 GUI 開，根除「兩個行程搶同一 COM」的問題。
- **可視性：** AI 的 TX 在 GUI 以 `[AI]` 標色，與使用者手打的輸入區分；RX 兩邊同源。
- **安全：** 本機、無雲、不碰 credential；pipe 僅限本機行程，只搬 serial bytes。

### 註冊（Kiro CLI）

```powershell
kiro-cli mcp add --name serial --command dotnet `
  --args "run --project src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj"
```

或寫進 agent.json 的 `mcpServers`；Claude CLI 則用其對應的 `mcpServers` 設定。**使用前提：先在 ETTerms GUI 開好要操作的 serial 連線**，AI 才能 `serial_attach` 上去。註冊後即可對 AI 說「列出目前 serial session → 接上 COM3 → 送指令看回應」。

---

## Key Constraints & Business Rules

1. **單機、無雲：** 所有連線 metadata 存本機 SQLite，密碼存 Windows Credential Manager，不回傳任何遙測。
2. **密碼絕不落地明碼：** SQLite 只存 `CredentialKey`，實際密碼 / passphrase 一律走 Credential Manager。
3. **一條連線一個分頁：** 同一連線可開多個分頁（各自獨立 session），但每個分頁綁定一個 `ISessionChannel`。
4. **腳本對 active session 執行：** TTL 腳本只作用在目前選定的分頁 channel，不會跨分頁亂送。例外：**Group 模式**下 `waitall` / `sendlnall` / `sendlngroup` 可跨同 Group 成員同步。
5. **腳本可取消：** 長時間 `wait` / `while` 必須能被使用者中止（`isCancelled` 旗標 + `OperationCanceledException`）。
6. **Serial port 互斥：** 一個 COM port 同時只能被一個 session 開啟；開啟前需檢查可用性。
7. **VT 相容性以常見情境為準：** VT100 / 常見 ANSI 序列優先；冷門 escape 可後補，不阻塞 GUI 進度。
8. **GUI 先行：** Phase 1–2 必須先讓視窗外殼 + 分頁 + 假連線可見可操作，再接真實 channel。
9. **不依賴外部 exe：** 不嵌 TeraTerm、不需 com0com；全原生 .NET 元件。
10. **UI 不可被 channel I/O 阻塞：** channel 讀寫在背景，UI 更新一律 `Invoke` 回 UI thread。

---

## Security Considerations

- **密碼儲存：** 一律使用 **Windows Credential Manager**（透過 `CredentialVault.cs`）。SQLite 內只存索引 `CredentialKey`，無明碼。SSH private key passphrase 同理。
- **SSH host key 驗證：** 首次連線顯示 host key 指紋供使用者確認（trust-on-first-use），記錄已信任的指紋，之後比對；指紋不符要警告。
- **私鑰檔保護：** private key 路徑存設定，但不複製 key 內容進 repo / SQLite。
- **輸入處理：** 終端機輸入直接透傳給遠端，不做 shell 注入解讀（本來就是終端機）；但 UI 載入腳本檔時要防路徑穿越 / 過大檔。
- **日誌不含密碼：** `AppLogger` 與 `logopen` 輸出不可寫入密碼 / passphrase；連線資訊只記主機 / port，不記 credential。
- **無 `secret/` 資料夾：** ETTerms 無伺服端祕密 / DB 密碼 / compile-time secret，連線密碼一律走 Windows Credential Manager，因此不設 `secret/` 集中目錄，也不需要 publish 類腳本。若日後做 Release 程式碼簽章，簽章 `.pfx` 請放在 repo 外並以環境變數 / CI secret 傳入。
- **設定檔權限：** SQLite 與設定檔放在 `%LocalAppData%\ETTerms\`，跟隨使用者帳號 ACL。

---

## Build & Setup Steps

```powershell
# 0. 前置：.NET 8 SDK（或 SDK 9/10 + .NET 8 Desktop Runtime）。確認：
dotnet --list-sdks
dotnet --list-runtimes | findstr WindowsDesktop   # 需有 8.0.x

# 1. 建立方案與專案（Phase 1）
cd F:\10_AI\ETTerms
dotnet new sln -n ETTerms
dotnet new winforms -n ETTerms -o src\ETTerms -f net8.0
dotnet sln add src\ETTerms\ETTerms.csproj

# 2. 安裝 NuGet 套件
cd src\ETTerms
dotnet add package SSH.NET                      # 原生 SSH
dotnet add package System.IO.Ports              # Serial
dotnet add package Microsoft.Data.Sqlite        # 連線 metadata
dotnet add package SnmpSharpNet                 # 選用：PDU 控制
# Windows Credential Manager：用 CredentialManagement 套件或 P/Invoke advapi32

# 3. 設定 csproj（手動確認）
#    <TargetFramework>net8.0-windows</TargetFramework>
#    <UseWindowsForms>true</UseWindowsForms>
#    <Nullable>enable</Nullable>

# 4. 建置與執行
cd F:\10_AI\ETTerms
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj

# 5. 打包（見下方「Publish / 打包慣例」）
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained false `
    -o src\ETTerms\Publish\ETTerms_v<Version>
#    後續可用 Inno Setup / MSIX 做安裝程式
```

---

## Publish / 打包慣例

> 每次發 Release 都依此規則產出，方便辨識版本與一併攜帶 MCP server。

**輸出位置（固定）：** `src\ETTerms\Publish\ETTerms_v{Version}\`
- `{Version}` 取自 `src\ETTerms\ETTerms.csproj` 的 `<Version>`（例：`0.2.0`）。
- 資料夾命名：`ETTerms_v{Version}`（前綴 `v`，例：`ETTerms_v0.2.0`）。
- `Publish/` 已 gitignore（`publish/`），**不入 git**。

**內容結構：**
```
ETTerms_v0.2.0\
├── ETTerms v0.2.0.exe        # 主程式 apphost，改名為「ETTerms v{Version}.exe」
├── ETTerms.dll + 各相依 dll  # SSH.NET / SQLite / SnmpSharpNet / System.IO.Ports …
└── ETTerms.SerialMcp\        # Serial MCP server，獨立發佈到子資料夾（相依 dll 與 GUI 隔離）
    ├── ETTerms.SerialMcp.exe
    └── ETTerms.SerialMcp.dll + 相依
```

**規則：**
1. **GUI 與 MCP 都發佈**：主程式發到 `ETTerms_v{Version}\`，`ETTerms.SerialMcp` 發到其下 **`ETTerms.SerialMcp\` 子資料夾**（兩者相依 dll 不互相覆蓋）。
2. **主 exe 改名**：`dotnet publish` 產生的 `ETTerms.exe` 重新命名為 **`ETTerms v{Version}.exe`**。
   - 可安全改名：.NET apphost 內部記錄要載入的 `ETTerms.dll`，**不靠自身檔名**，改名後仍正常啟動。
3. **框架相依**：`--self-contained false -r win-x64`（目標機需已裝 .NET 8 Desktop Runtime）。

**PowerShell 範例：**
```powershell
$ver    = ([regex]::Match((Get-Content src\ETTerms\ETTerms.csproj -Raw), '<Version>([^<]+)</Version>')).Groups[1].Value
$root   = "src\ETTerms\Publish\ETTerms_v$ver"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }

# 1) GUI
dotnet publish src\ETTerms\ETTerms.csproj         -c Release -r win-x64 --self-contained false -o $root
Rename-Item (Join-Path $root "ETTerms.exe") "ETTerms v$ver.exe"

# 2) Serial MCP server → 子資料夾
dotnet publish src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj -c Release -r win-x64 --self-contained false `
    -o (Join-Path $root "ETTerms.SerialMcp")
```

---

## Development Phases

> 依使用者要求「**前面先把 GUI 做出來，後面再慢慢補功能**」排序：
> Phase 1–2 先把 KKTerm 風格的視窗外殼、分頁、假連線清單做到「看得到、點得動」，
> Phase 3 起才接真實 Serial / SSH channel，最後補腳本引擎與打包。

### Phase 1 — 專案骨架 + UI Shell（工作量：S）✅ 已完成
**目標：** 專案能 `dotnet run` 啟動，KKTerm 風格的主視窗外殼可見：左側 Activity Rail、連線 Sidebar、右側分頁工作區（先放假分頁）。
**包含：**
- [x] `dotnet new winforms` 建立 `src/ETTerms`，設定 `net8.0-windows` / `UseWindowsForms` / `Nullable`
- [x] `MainForm.cs` + `MainForm.Designer.cs`：三欄佈局（Rail / Sidebar / Workspace）
- [x] `ActivityRail.cs`：Terminal / Scripts / Settings 三個圖示按鈕（hover tooltip）
- [x] `ConnectionSidebar.cs`：用假資料填 TreeView（SSH / Serial 圖示）＋資料夾樹 / 搜尋 / CRUD / 拖曳分類（提前實作）
- [x] `Workspace/WorkspaceView.cs`（取代原訂 `WorkspaceTabs.cs`）：可平鋪 tiling 工作區（grid 預設 + 手動分割 + pane 內迷你分頁），提前實作 Future Extension 的分割畫面
- [x] `AppLogger.cs`：從 MyTeraTerm 移植日誌
- [x] 深色主題：`Theme.cs` + `NativeTheme.ApplyDarkTitleBar`（DWM 深色標題列）+ 深色對話框基底
**驗收條件：** ✅ `dotnet build` 通過（0 警告 0 錯誤）；主視窗出現，可在 Rail 切檢視、Sidebar 看到假連線、可開關分頁與分割工作區。

### Phase 2 — 連線資料 + Sidebar 真資料（工作量：M）✅ 已完成
**目標：** 連線清單改吃 SQLite 真資料，可新增 / 編輯 / 刪除連線，密碼存進 Credential Manager。
**包含：**
- [x] `Connection.cs`：`Connection` / `SshSettings` / `SerialSettings` / `ConnectionType` / `SshAuthMethod` 模型
- [x] `ConnectionStore.cs`：SQLite CRUD（`%LocalAppData%\ETTerms\ettermsdb.sqlite` 自動建表，`SettingsJson` 存 Ssh/Serial）
- [x] `CredentialVault.cs`：Windows Credential Manager 讀寫封裝（advapi32 P/Invoke，`CredWrite`/`CredRead`/`CredDelete`）
- [x] 連線編輯對話框：依類型切換 SSH（host/port/user/auth/key/password）與 Serial（COM/baud/databits/parity/stopbits/handshake）兩種表單
- [x] Sidebar 綁定 `ConnectionStore`，支援新增 / 編輯 / 刪除 / 拖曳分組 / `SortOrder` 排序持久化（資料夾路徑存於 `GroupName`）
**驗收條件：** ✅ 新增一條 SSH 與一條 Serial 連線、重啟 App 後仍在；密碼存在 Credential Manager（SQLite 內看不到明碼）。

> **實作備註：** 資料夾以連線的 `GroupName`（`/`-join 路徑）持久化；含連線的資料夾會在重啟後由路徑重建，**空資料夾為 session-only**（不另設 folders 表）。密碼一律經 `CredentialVault` 寫入 Credential Manager，SQLite 僅存 `CredentialKey`（`ETTerms/{Id}`）。

### Phase 3 — Serial Port Session（工作量：M）✅ 已完成
**目標：** 雙擊 Serial 連線可開分頁、收發資料；先用最陽春的文字框驗證資料流。
**包含：**
- [x] `Sessions/ISessionChannel.cs`：`Write(byte[])` + `event Action<byte[]> DataReceived` + `Open()` / `Close()`（: IDisposable）
- [x] `Sessions/SerialChannel.cs`：`System.IO.Ports.SerialPort` 實作（開啟前向 SessionManager 占用 COM port、互斥、錯誤釋放）
- [x] `Sessions/SessionPage.cs` + `Sessions/SessionManager.cs`：分頁內容（綁 channel）+ 全域 session 登錄 / COM 互斥 / 列舉
- [x] 暫用 `RichTextBox`（ReadOnly，靠遠端 echo 顯示）打通「鍵盤→送出、收到→顯示」
- [x] UI 跨執行緒安全：channel 在 handle 建立後才 Open，`DataReceived` 經 `BeginInvoke` 回 UI thread
- [x] 連線資料流改打通：`ConnectionActivated` 改傳完整 `Connection`，`WorkspaceView.BuildPage` 依類型建 `SessionPage`(Serial) 或佔位(SSH)；關分頁 / 關 pane 經 `SessionPage.Dispose` 釋放 COM port
**驗收條件：** ✅ 編譯通過（0 警告 0 錯誤）。接一塊開發板 / com0com loopback，打字送得出去、回傳看得到，關分頁能正確釋放 COM port（需實機 `dotnet run` 驗證硬體收發）。

### Phase 4 — SSH Session（工作量：M）✅ 已完成
**目標：** 雙擊 SSH 連線可登入遠端、互動式 shell 可用。
**包含：**
- [x] `Sessions/SshChannel.cs`：SSH.NET `SshClient` + `ShellStream`（背景執行緒 Connect 避免凍 UI），實作 `ISessionChannel`
- [x] 三種認證：password / private key（passphrase 走 `CredentialVault`）/ keyboard-interactive
- [x] Host key 指紋 TOFU：`Sessions/HostKeyStore.cs`（`known_hosts.txt`）首次記錄 SHA256、之後比對、不符中止並警告
- [x] 連線錯誤（逾時 / 認證失敗 / 斷線）寫入終端機輸出 + `AppLogger`
- [x] PTY resize：反射呼叫 ShellStream 底層 `_channel.SendWindowChangeRequest`（SSH.NET 未公開此 API）
**驗收條件：** ✅ 編譯通過（0/0）。用 password 與 key 兩種方式各登入一台 SSH 主機，能跑 `ls` / `top` 等互動命令（需實機驗證）。

> **套件：** SSH.NET 2024.2.0（含 BouncyCastle.Cryptography 2.4.0）。

### Phase 5 — VT100 終端機控制項（工作量：L）✅ 已完成
**目標：** 用自繪 VT100 控制項取代陽春文字框，正確顯示顏色 / 游標 / 清屏等 ANSI 行為。
**包含：**
- [x] `Terminal/ScreenBuffer.cs`：rows×cols 字格（`Cell{Ch,Fg,Bg,Attr}`，含 Bold/Underline/Inverse）+ scrollback + 滾動區（DECSTBM）+ alt screen
- [x] `Terminal/AnsiParser.cs`：狀態機（Ground/Esc/CSI/OSC）—游標移動、SGR（16 色 / 256 色 / truecolor）、清屏 / 清行、scroll region、insert/delete line/char、alt screen（47/1047/1049）、DECTCEM(?25)/DECAWM(?7)/DECCKM(?1）+ `Palette`
- [x] `Terminal/TerminalView.cs`：owner-drawn 雙緩衝、run 合併繪製、滾輪 scrollback、選取 / 複製（Ctrl+C、右鍵）/ 貼上（Ctrl+V、Shift+Insert、右鍵）
- [x] `Terminal/TerminalInput.cs`：鍵盤 → byte 序列（方向鍵含 appCursor、Fn、Home/End/PgUp/PgDn、Enter/Tab/Backspace/Esc）
- [x] 套用 `Terminal/TerminalProfile`（字型 / 字級 / scrollback 行數）；resize 由 `OnSizeChanged` 算 cols/rows 並 `Resized` 事件通知 channel（PTY size）
**驗收條件：** ✅ 編譯通過（0/0）。在 SSH 跑 `vim` / `htop` / `top` 顏色與版面正常、視窗 resize 通知遠端（需實機驗證）。

### Phase 6 — TTL 腳本引擎移植 + 執行 UI（工作量：L）
**目標：** 移植 MyTeraTerm 的 `TTLInterpreter`，改為驅動 `ISessionChannel`，可對 active session 跑 `.ttl` 腳本。
**包含：**
- [x] `TTLInterpreter.cs`：從 MyTeraTerm 移植，建構子改吃 `ISessionChannel`（取代 `ComPortBridge`）
- [x] 指令覆蓋：`send` / `sendln` / `pause` / `wait` / `timeout` / `flushrecv` / `logopen` / `logwrite` / `logclose` / `messagebox` / `if`-`elseif`-`else`-`endif` / `while`-`endwhile` / 變數指派
- [x] `ScriptRunner.cs`：`async` 執行 + 取消（`Cancel()`）+ `StatusChanged`（檔名 / 行號 / 指令）事件
- [x] Group 同步指令：`waitall` / `sendlnall` / `sendlngroup` + `GroupSyncContext`（Barrier 同步）
- [x] `▶ Run All`（對所有 Serial 個別跑）+ `▶ Group1/2/3`（Group 同步跑）
- [x] `▶ Script` / `▶ Run All` 拒絕含 Group 指令的腳本（彈 Warning）
- [x] `docs/ttl-script-reference.md`：指令對照表（含 Group 指令）
- [ ] SSH session 完整驗收（SSH 自動登入 + 下命令 + `logopen` 收 log）
**驗收條件：** Serial 已驗收通過。SSH 尚待實機驗證：載入一支 `.ttl`（SSH 自動登入 + 下命令 + `logopen` 收 log），對 active session 跑完並產生 log 檔，中途可按停止中止。

### Phase 7 — Settings + About 頁面（工作量：S）✅ 已完成
**目標：** Activity Rail 加入 Settings / About 獨立頁面，提供偏好設定入口與版本紀錄。
**包含：**
- [x] `ActivityRail` 新增 Settings / About 兩個 view（三圖示：▤ / ⚙ / ℹ）
- [x] `MainForm` view 切換邏輯（Terminal / Settings / About 互斥顯示）
- [x] `SettingsView.cs`：偏好設定頁面骨架（Phase 8 擴充內容）
- [x] `AboutView.cs`：左側 App 資訊 + Developer + Tech Stack 卡片，右側 Changelog timeline
- [x] Changelog 資料結構，新版本只需加一筆 `ChangelogEntry`
**驗收條件：** ✅ 點 Activity Rail 可切換三個頁面；About 頁正確顯示版本、作者、changelog。

### Phase 8 — PDU 控制 + Settings 擴充 + Shell + SFTP（工作量：M）✅ 已完成
**目標：** 補上 SNMP PDU 控制、偏好設定持久化、本機 Shell（ConPTY）、SFTP 瀏覽器。
**包含：**
- [x] `Scripting/Pdu/PduController.cs`：SnmpSharpNet 實作，接 `TTLInterpreter` 的 `pductrl` / `pduconnect`
- [x] `Infrastructure/AppSettings.cs`：終端機偏好 / Shell 設定 / 視窗位置記憶（JSON 存 `%LocalAppData%\ETTerms\`）
- [x] Settings 檢視 UI：Terminal 分頁（字型 / 配色 / scrollback / 預設換行 / Shell 設定 + 即時預覽）+ PDU 分頁（連線 / 狀態監控）
- [x] `Sessions/ShellChannel.cs`：Windows ConPTY 本機 Shell（PowerShell / Bash / Cmd），完整 PTY 支援
- [x] Sidebar SFTP 分頁：SSH SFTP 檔案瀏覽器（連線 / 導航 / 目錄列表）
- [x] `docs/runbooks/troubleshooting.md`：常見問題（COM / SSH / TTL / PDU）
- [ ] `dotnet publish` + Inno Setup / MSIX 安裝程式（待使用者指示）
**驗收條件：** ✅ PDU 指令可用；Settings 重啟保留；Local Shell 行為正常（ConPTY）；SFTP 可瀏覽遠端目錄。打包待後續。

### Phase 9 — AI 整合：Serial MCP Server（GUI 橋接模式）（工作量：M）✅ 已完成
**目標：** 讓 Kiro CLI / Claude CLI 等 AI agent 收發 serial，且**使用者能在 GUI 即時看到 AI 的收發**。COM port 由 GUI 唯一持有，MCP 經本機 named pipe 橋接（見 [AI / MCP Integration](#ai--mcp-integrationserial-mcp-server)）。
**包含：**
- [x] GUI：`Sessions/SerialBridgeServer.cs`——本機 named pipe server（`\\.\pipe\etterms-serial`），暴露 list / attach / write / read，並把 serial session 的 RX 廣播給 client；`SerialBridge.cs` 為單一 session 的橋接端點
- [x] GUI：`SessionPage.WriteFromAi` 把 MCP 來源的 TX 以 `[AI]` 標色（`\x1b[35m`）echo，使用者可區分 AI 與手打輸入
- [x] `src/ETTerms.SerialMcp/`：.NET 8 console + 官方 C# MCP SDK（`ModelContextProtocol`），stdio / JSON-RPC
- [x] `SerialBridgeClient.cs`：連 GUI pipe、轉發 write、背景累積 RX
- [x] 工具：`serial_list` / `serial_attach` / `serial_write` / `serial_read`（含 `waitFor` + `timeoutMs`）/ `serial_detach`
- [x] 註冊說明（`kiro-cli mcp add` / agent.json `mcpServers`）寫入 [docs/serial-mcp-guide.md](docs/serial-mcp-guide.md)，含「需先在 GUI 開好 port」前提
**驗收條件：** ✅ GUI 開一條 Serial（COM3）→ 另一分頁 PowerShell 跑 kiro → AI 經 MCP `serial_attach` COM3 → `serial_write` 送指令、`serial_read` 讀回應，**整個過程在 GUI Tab1 即時可見（AI 的 TX 有 `[AI]` 標色）**；全程只有 GUI 開該 port。

---

## Future Extensions

這個版本**不做、但未來可能加**：

- ~~**AI / MCP 整合**~~（✅ 已於 [Phase 9](#development-phases) 實作：Serial MCP Server，讓 AI agent 直接操作 serial；未來可再擴充 SSH / Shell MCP 工具）
- ~~**SFTP 檔案瀏覽**~~（✅ 已於 Phase 8 實作：sidebar SFTP 分頁）
- **Telnet** session 類型（補一個 `TelnetChannel : ISessionChannel`）
- **RDP / VNC** 分頁（KKTerm 用 mstscax.dll；ETTerms 可後期評估）
- **tmux 自動 attach**（SSH 斷線後自動回貼，仿 KKTerm）
- ~~分割畫面 / 多 pane~~（✅ 已於 Phase 1 提前實作：`WorkspaceView` + `PaneControl` + `ProportionalSplit`）
- **拖曳 pane 重新排列**（目前可分割 / 關閉 / 調大小，但還不能把 pane 拖到別處；未來可加 drag-drop 重排）
- **佈局記憶**（記住上次的 grid / split 佈局，重啟還原）
- **全新腳本語言或內嵌 Lua / C# scripting**（目前先沿用 TTL；未來可加第二引擎）
- **連線分組 / 標籤 / 搜尋** 強化
- **跨平台**（WinForms 綁 Windows；若要跨平台需評估 Avalonia / MAUI 重寫 UI 層，但 `Sessions` / `Scripting` 層因抽象良好可重用）
- **macOS / Linux 終端機渲染** 改用 WebView2 + xterm.js（若要與 KKTerm 視覺對齊）

---

## Reference Projects（`For_AI/`）

| 專案 | 角色 | 取用重點 |
|------|------|---------|
| `KKTerm-main` | **UI 參考** | Activity Rail + 分頁工作區 + Saved Connections sidebar 的版面與互動；SQLite 存連線、Credential Manager 存密碼的 local-first 思路 |
| `MyTeraTerm` | **Script 參考** | `lib/TTLInterpreter.cs` 的 TTL 指令實作（直接移植）、`AppLogger.cs` 日誌、PDU/SNMP 控制（`pductrl`/`pduconnect`）；`ComPortBridge.cs` 的 serial 經驗 |

> 注意：KKTerm 是 Tauri/React，**不**直接複製程式碼，只取 UI/UX 與資料架構概念；ETTerms 是純 WinForms。MyTeraTerm 的 `TTLInterpreter` 則可大段移植，主要改建構子從 `ComPortBridge` 換成 `ISessionChannel`。
