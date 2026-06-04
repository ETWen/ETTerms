# ETTerms

[![English](https://img.shields.io/badge/English-lightgrey.svg)](README.md) [![繁體中文](https://img.shields.io/badge/%E7%B9%81%E9%AB%94%E4%B8%AD%E6%96%87-2ea043.svg)](README.zh-TW.md)

> 原生 Windows 終端機工作台（C# .NET 8 WinForms）—— 一個視窗整合 **SSH**、**Serial Port**、**本機 Shell (ConPTY)** 連線，內建從 MyTeraTerm 移植的 **TTL 腳本引擎**做自動化，並提供選用的 **Serial MCP server**，讓 AI agent（Kiro CLI / Claude CLI）直接操作 serial port。單機、無雲、無登入系統。

![version](https://img.shields.io/badge/version-0.1.0-blue.svg) ![platform](https://img.shields.io/badge/platform-Windows-0078D6.svg?logo=windows&logoColor=white) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg?logo=dotnet&logoColor=white) ![UI](https://img.shields.io/badge/UI-WinForms-5C2D91.svg) ![SSH](https://img.shields.io/badge/SSH-SSH.NET-success.svg) ![Serial](https://img.shields.io/badge/Serial-System.IO.Ports-success.svg) ![status](https://img.shields.io/badge/status-WIP-orange.svg) ![license](https://img.shields.io/badge/license-MIT-green.svg)

---

## 📖 目錄

- [✨ 功能特色](#-功能特色)
- [🖼️ 介面配置](#️-介面配置)
- [💻 系統需求](#-系統需求)
- [📥 安裝](#-安裝)
- [🚀 快速開始](#-快速開始)
- [📚 使用指南](#-使用指南)
- [🤖 TTL 腳本](#-ttl-腳本)
- [👥 Group 同步執行](#-group-同步執行)
- [⚡ PDU 電源控制](#-pdu-電源控制)
- [🤖 AI / MCP 整合](#-ai--mcp-整合)
- [🔐 連線資料與安全](#-連線資料與安全)
- [🔧 疑難排解](#-疑難排解)
- [🔨 從原始碼建置](#-從原始碼建置)
- [📁 專案結構](#-專案結構)
- [🤝 貢獻](#-貢獻)
- [📜 版本紀錄](#-版本紀錄)
- [📄 授權](#-授權)
- [🙏 致謝](#-致謝)

---

## ✨ 功能特色

### 核心功能

- 🖥️ **多協定連線，單一工作台**
  - **SSH**（`SSH.NET`）：password / private key / keyboard-interactive 認證，內建 SFTP
  - **Serial Port**（`System.IO.Ports`）：COM port、baud、data bits、parity、stop bits、handshake 全可調
  - **本機 Shell**（Windows ConPTY）：直接開 PowerShell / Cmd / Bash

- 🪟 **可平鋪 (tiling) 的分頁工作區**
  - 一鍵切換 `1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3` 格狀佈局
  - 每個 pane 可左右 `↔` / 上下 `↕` 手動分割，拖格線按比例縮放
  - 每個 pane 內可再開多條連線分頁（pane 自己的迷你 tab strip）

- 🗂️ **可編輯的連線側欄（Saved Connections）**
  - 自建巢狀資料夾分類、連線數量徽章、全部展開 / 收合
  - 名稱 / 主機即時搜尋過濾，命中分支自動展開
  - 右鍵與工具列：新增 / 改名 / 刪除、拖曳分類
  - 雙擊連線 → 開進 active pane；支援不存檔的快速連線

- 🤖 **TTL 腳本自動化**
  - 沿用並擴充 MyTeraTerm 的 TTL（Tera Term Language）直譯器
  - 對 active session 執行 `.ttl`，即時顯示「檔名 / 行號 / 當前指令」
  - 支援 `send` / `sendln` / `wait` / `if` / `while` / 變數運算 / log 寫檔等
  - 長時間 `wait` / `while` 可隨時 **■ Stop** 中止

- 👥 **Group 同步執行**
  - 把多個分頁設為 Group 1/2/3，對整組同時跑同一份腳本
  - 同步指令：`waitall`（等齊）、`sendlnall`（到齊後各自送）、`sendlngroup`（指定成員送）

- ⚡ **PDU 電源控制（選用）**
  - 透過 SNMP（`SnmpSharpNet`）控制 PDU 插座，測試中遠端電源循環
  - 腳本指令：`pduconnect` / `pductrl`

- 🤖 **AI / MCP 整合（選用，規劃中）**
  - **COM port 由 GUI 持有**；獨立的 stdio **Serial MCP server** 經本機 named pipe 橋接過去，供 **Kiro CLI / Claude CLI** 使用
  - 工具：`serial_list` / `serial_attach` / `serial_write` / `serial_read` / `serial_detach`
  - AI 的 serial 收發會即時顯示在 GUI（標 `[AI]`）——先在 GUI 開好 port，再讓 AI attach

- 📊 **連線 RX 日誌**
  - `logopen` / `logwrite` / `logclose` 將工作階段輸出寫檔

### 終端機渲染

- 🎨 自繪 VT100 / ANSI 控制項（owner-drawn），雙緩衝繪字格
- 🌑 KKTerm 風格深色主題（含 DWM 深色標題列）
- 🔤 可調字型 / 字級 / 配色 / scrollback 行數
- 📋 選取 / 複製 / 貼上

---

## 🖼️ 介面配置

```
┌──────┬─────────────────────┬───────────────────────────────────────┐
│ ▣ T  │ Saved Connections   │  Workspace (可平鋪)                    │
│ ▣ S  │  🔍 search          │  ┌─────────────────┬─────────────────┐ │
│ ▣ ⚙  │  ▾ 📁 Servers (2)   │  │ [tab1][tab2] +  │ [tab1] +        │ │
│      │     ▸ SSH srv-01    │  │                 │                 │ │
│ 圖示 │     ▸ SSH nas       │  │   TerminalView  │   TerminalView  │ │
│ 列   │  ▾ 📁 Boards (2)    │  │   (VT100 自繪)  │   (VT100 自繪)  │ │
│      │     ▸ COM3 @115200  │  │                 │                 │ │
│      │     ▸ COM7 @9600    │  ├─────────────────┴─────────────────┤ │
│      │                     │  │ [Group1-A]   ▶ Script  ■ Stop     │ │
└──────┴─────────────────────┴───────────────────────────────────────┘
  Activity Rail        Sidebar              Tabbed / Tiling Workspace
```

> Activity Rail（左側圖示列）切換 **Terminal / Scripts / Settings** 三大檢視；側欄管理已存連線；工作區以分頁與平鋪 pane 容納多條 session。

---

## 💻 系統需求

| 項目 | 需求 |
|------|------|
| 作業系統 | Windows 10 (1809+) / Windows 11 |
| 執行階段 | .NET 8 Desktop Runtime（`Microsoft.WindowsDesktop.App 8.0.x`） |
| 顯示 | 建議 1600×900 以上 |
| 選用硬體 | USB-to-Serial 轉接器（Serial 連線）、支援 SNMP 的 PDU（電源控制） |

確認執行階段：

```powershell
dotnet --list-runtimes | findstr WindowsDesktop
```

---

## 📥 安裝

目前以原始碼建置為主（尚未提供 binary release）。

```powershell
git clone <repo-url> ETTerms
cd ETTerms
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj
```

完整建置步驟見 [從原始碼建置](#-從原始碼建置)。

---

## 🚀 快速開始

### 建立一條 SSH 連線

1. 啟動 ETTerms，左側 Activity Rail 切到 **Terminal**
2. 側欄按 **＋ 新增連線**，選類型 **SSH**
3. 填入 `Host` / `Port`（預設 22）/ `Username`，選認證方式（密碼 / 私鑰）
4. 雙擊該連線 → 在目前 active pane 開啟分頁

### 建立一條 Serial 連線

1. 側欄 **＋ 新增連線**，類型選 **Serial**
2. 選 `COM port` 與 `BaudRate`（如 `COM3` / `115200`）
3. 雙擊開啟

### 執行 TTL 腳本

1. 連上任一分頁後，在腳本列按 **▶ Script** 選 `.ttl`
2. 左側狀態列即時顯示執行行號與指令
3. 需要時按 **■ Stop** 中止

---

## 📚 使用指南

### Activity Rail（左側圖示列）

切換三大主檢視：**Terminal**（連線工作區）、**Scripts**（腳本編輯與執行）、**Settings**（偏好設定）。

### 連線側欄（Saved Connections）

| 操作 | 方式 |
|------|------|
| 新增資料夾 / 連線 | 工具列按鈕或右鍵選單 |
| 重新命名 / 刪除 | 右鍵選單 |
| 分類 | 拖曳連線或資料夾到目標資料夾（禁止拖入自身子孫） |
| 搜尋 | 上方搜尋框，依名稱 / 主機即時過濾 |
| 開啟連線 | 雙擊 → 開進 active pane |

### 工作區平鋪（Tiling）

- 工具列一鍵套用 grid 佈局：`1×1 / 1×2 / 2×1 / 2×2 / 2×3 / 3×3`
- 每個 pane 右上角：`↔` 左右分割、`↕` 上下分割、`＋` 新分頁、`✕` 關閉
- 關閉某格時兄弟節點自動補位，至少保留一格
- active pane 以強調色外框標示

### 終端機

- 接收 channel bytes → ANSI 解析 → 字格緩衝 → 繪製
- 鍵盤輸入轉 byte 序列送往遠端
- 支援 scrollback、選取 / 複製 / 貼上；字型與配色見 **Settings**

---

## 🤖 TTL 腳本

在任一連線分頁頂部腳本列按 **▶ Script** 載入 `.ttl` 對該連線執行。完整語法與範例見
[docs/ttl-script-reference.md](docs/ttl-script-reference.md)，範例腳本在 `tools/scripts/`。

### 支援的指令

| 指令 | 說明 |
|------|------|
| `send '文字'` | 送出文字（不加換行） |
| `sendln '文字'` | 送出文字並附加 `\r\n` |
| `wait '字串'` | 等到接收緩衝出現該字串才往下（預設無限等待，可按 Stop 取消）；命中後 `result=1` |
| `flushrecv` | 清空接收緩衝區 |
| `pause 秒數` | 暫停 N 秒（可被 Stop 中止） |
| `timeout = 秒數` | 設定 `wait` 逾時秒數；`0`＝無限等待，`N>0` 超時中止並報錯 |
| `if … then` / `elseif … then` / `else` / `endif` | 條件分支（可巢狀） |
| `while …` / `endwhile` | 迴圈（可巢狀，可被 Stop 中止） |
| `名稱 = 值` | 變數指派；支援 `+ - * /` 整數運算與字串；內建變數 `result` |
| `logopen '檔名'` | 開啟 log 檔（覆寫） |
| `logwrite '文字'` | 寫一行到 log |
| `logclose` | 關閉 log（腳本結束自動關閉） |
| `messagebox '訊息'` | 跳出對話框（最上層顯示） |
| `; 註解` | 行內註解（`;` 之後到行尾） |
| `:label` | 標籤行（會被略過） |

**條件運算子**（`if` / `elseif` / `while`）：`>=` `<=` `>` `<` `==` `!=` `=`，或無運算子（非零為真）。

### 範例：自動登入

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

## 👥 Group 同步執行

把多個分頁編成同一 Group，對整組同時跑同一份腳本——適合多台 DUT 同步測試。

1. **右鍵分頁** → 設為 Group 1 / 2 / 3（或取消）；cell footer 顯示 `[Group1-A]` 標籤
2. 工具列按 **▶ Group1 / ▶ Group2 / ▶ Group3** 對整組執行

| 指令 | 說明 |
|------|------|
| `waitall '字串'` | 全員都 wait 到關鍵字後才一起繼續（`System.Threading.Barrier` 同步） |
| `sendlnall '文字'` | 全員到齊後各自 sendln |
| `sendlngroup N '文字'` | 僅指定 member 送出 |

> ⚠️ Group 同步指令**只能在 Run Group 模式**使用；`▶ Script` 與 `▶ Run All` 會拒絕含這些指令的腳本並彈出警告。

---

## ⚡ PDU 電源控制

透過 SNMP 控制 PDU 插座，可在腳本中對 DUT 做電源循環。

```ttl
; 連線 PDU（裝置編號 2 = iPoMan II 1202, IP 192.168.1.21）
pduconnect 2 192.168.1.21

pductrl 2 1 0      ; 關閉 port 1
pause 5
pductrl 2 1 1      ; 開啟 port 1
wait 'login: '
```

| 指令 | 語法 | 說明 |
|------|------|------|
| `pduconnect` | `pduconnect <device> <ip>` | 連線 PDU |
| `pductrl` | `pductrl <device> <port> <0\|1>` | 指定 port 關(0) / 開(1) |

---

## 🤖 AI / MCP 整合

> 🔜 **規劃中（Phase 9）。** 讓 AI agent（**Kiro CLI / Claude CLI**）收發 serial，**而且你能在 ETTerms GUI 即時看到 AI 的每筆收發**。

**關鍵設計：COM port 由 GUI 唯一持有，MCP server 不自己開 port。** 一個 COM port 同時只能被一個行程開啟；因此不讓 MCP server 自己開，而是 **GUI 當 port 的唯一擁有者**，在 GUI 內跑一支本機 **named pipe server**（`SerialBridgeServer`）。獨立的 `ETTerms.SerialMcp`（由 Kiro/Claude CLI 啟動）退化成瘦客戶端：所有 `write` / `read` 都經 pipe 轉發給 GUI，由 GUI 代為讀寫實體 port。AI 的 TX 會以 `[AI]` 標色 echo 進終端機，RX/TX 都流經 GUI channel，**你看到的就是 AI 看到的**。

全閉迴路——全部跑在 ETTerms GUI 內：
- **Tab1：** 一條 Serial 連線持有 COM3（即時顯示流量，AI 的寫入標 `[AI]`）
- **Tab2：** 一個 PowerShell（ConPTY）分頁跑 `kiro-cli`，它啟動 `ETTerms.SerialMcp`，再連回 pipe

| 工具 | 參數 | 說明 |
|------|------|------|
| `serial_list` | — | 列出 **GUI 目前開著的** serial session（名稱 + baud） |
| `serial_attach` | portName | 以 COM 名稱綁定到 GUI 的 serial session（write/read 前必須先 attach） |
| `serial_write` | text, appendNewline? | 經 GUI 送出文字（即時以 `[AI]` 標色顯示） |
| `serial_read` | waitFor?, timeoutMs? | 取出累積的 RX；可等待子字串或逾時 |
| `serial_detach` | — | 解除綁定（**不會**關閉 GUI 的 port） |

在 Kiro CLI 註冊：

```powershell
kiro-cli mcp add --name serial --command dotnet --args "run --project src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj"
```

或寫進 `agent.json` 的 `mcpServers`（Claude CLI 用其對應的 `mcpServers` 設定）。**先在 ETTerms GUI 開好要操作的 serial 連線**，再對 AI 說：*「列出 serial session、接上 COM3、送 `AT` 看回應」*。

> ⚠️ COM port 由 GUI 唯一持有；MCP server 經 pipe attach，自己不開 port，因此不會與 GUI 衝突。

📖 完整註冊與使用步驟：[docs/serial-mcp-guide.md](docs/serial-mcp-guide.md)。

---

## 🔐 連線資料與安全

- **單機、無雲：** 連線 metadata 存本機 SQLite（`%LocalAppData%\ETTerms\ettermsdb.sqlite`），不回傳任何遙測。
- **密碼絕不落地明碼：** SQLite 只存指向 **Windows Credential Manager** 的 `CredentialKey`；實際密碼 / 私鑰 passphrase 由 `CredentialVault` 透過 Credential Manager 存取。
- **SSH host key：** 首次連線顯示指紋供確認（trust-on-first-use），之後比對；不符會警告。
- **日誌不含密碼：** `AppLogger` 與 `logopen` 輸出只記主機 / port，不寫入任何 credential。
- **無 `secret/` 目錄：** 桌面單機 App，無伺服端祕密 / DB 密碼 / compile-time secret。

---

## 🔧 疑難排解

| 問題 | 可能原因 | 解法 |
|------|---------|------|
| 啟動報缺少 runtime | 未裝 .NET 8 Desktop Runtime | 安裝 `Microsoft.WindowsDesktop.App 8.0.x`，以 `dotnet --list-runtimes` 確認 |
| Serial 連線失敗 / port 被佔用 | COM port 被其他程式開啟 | 關閉其他終端機程式；一個 COM port 同時只能被一個 session 開啟 |
| SSH 認證失敗 | 帳密 / 私鑰錯誤或 host key 不符 | 檢查認證設定；確認 host key 指紋 |
| 腳本卡在 `wait` | 未收到預期字串 | 確認連線與 baud、`wait` 字串正確；設 `timeout` 或按 ■ Stop |
| Group 腳本被拒 | 用 `▶ Script` 跑含 Group 指令的腳本 | 改用 **▶ GroupN** 執行 |

更多操作手冊見 [docs/runbooks/](docs/runbooks/)。

---

## 🔨 從原始碼建置

### 前置需求

- .NET 8 SDK（或 SDK 9/10 + .NET 8 Desktop Runtime）
- Visual Studio 2022 或 VS Code + C# 擴充
- Git

### 建置與執行

```powershell
dotnet --list-sdks
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj
```

### 主要 NuGet 套件

| 套件 | 用途 |
|------|------|
| `SSH.NET` | SSH Shell + SFTP |
| `System.IO.Ports` | Serial 連線 |
| `Microsoft.Data.Sqlite` | 連線 metadata 儲存 |
| `SnmpSharpNet` | PDU 控制（選用） |

### 打包（self-contained=false）

```powershell
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained false
```

---

## 📁 專案結構

```
ETTerms/
├── README.md                 # 英文說明（預設）
├── README.zh-TW.md           # 本文件（繁體中文）
├── ARCHITECTURE.md           # 完整架構文件
├── CLAUDE.md                 # 專案記憶與開發指令
├── ETTerms.slnx
├── docs/
│   ├── ttl-script-reference.md   # TTL 指令對照
│   └── runbooks/                 # 操作手冊 / 疑難排解
├── tools/scripts/            # 範例 .ttl 腳本
├── src/ETTerms/              # 主應用程式（WinForms）
│   ├── App/                  # 視窗外殼：MainForm / ActivityRail / ConnectionSidebar / Workspace
│   ├── Terminal/             # 自繪 VT100：TerminalView / AnsiParser / ScreenBuffer / TerminalInput
│   ├── Sessions/             # 連線抽象：ISessionChannel / SshChannel / SerialChannel / ShellChannel
│   ├── Connections/          # 連線資料：Connection / ConnectionStore(SQLite) / CredentialVault
│   ├── Scripting/            # TTL 引擎：TTLInterpreter / ScriptRunner / GroupSyncContext / Pdu
│   └── Infrastructure/       # AppLogger / AppSettings / NativeTheme
└── src/ETTerms.SerialMcp/    # 🔜 Serial MCP server（stdio）—— 讓 AI agent 直接操作 serial port
```

**核心設計：** 所有連線都實作 `ISessionChannel`（`Write(byte[])` + `event DataReceived`）。`TerminalView` 與 `TTLInterpreter` 只認得這個抽象，因此 SSH / Serial / Shell 對上層完全一致——這是「同一套腳本引擎驅動多種連線」的關鍵。詳見 [ARCHITECTURE.md](ARCHITECTURE.md)。

---

## 🤝 貢獻

1. Fork 並建立 feature 分支：`git checkout -b feature/your-feature`
2. 遵循 [Conventional Commits](https://www.conventionalcommits.org/)：`feat(serial): add auto-reconnect`
3. Push 後開 Pull Request

**慣例：** PascalCase 類別 / 方法、`_camelCase` 私有欄位、檔名＝類別名；UI 層只相依 `ISessionChannel` 抽象；channel I/O 在背景，UI 更新一律 `Control.Invoke` 回 UI thread。

---

## 📜 版本紀錄

### v0.1.0（開發中）

- Phase 1–8 完成：視窗外殼、連線側欄、平鋪工作區、Serial / SSH / 本機 Shell (ConPTY) / SFTP
- 自繪 VT100 終端機渲染
- TTL 腳本引擎（移植自 MyTeraTerm）+ Group 同步執行
- PDU 電源控制（SNMP）
- Settings / About
- 打包待規劃

**規劃中**

- Phase 9：Serial MCP server（`ETTerms.SerialMcp`）—— 讓 AI agent（Kiro CLI / Claude CLI）直接操作 serial port

---

## 📄 授權

本專案採 **MIT License**，詳見 [LICENSE](LICENSE)。

---

## 🙏 致謝

- **[KKTerm](https://github.com/)** — UI 設計參考（Activity Rail + 分頁工作區 + Saved Connections）
- **MyTeraTerm** — TTL 腳本引擎與 `AppLogger` 來源
- **[SSH.NET](https://github.com/sshnet/SSH.NET)**、**[SnmpSharpNet](http://www.snmpsharpnet.com/)** — 開源連線 / SNMP 函式庫
- **Microsoft** — .NET 8、WinForms、ConPTY、Credential Manager
