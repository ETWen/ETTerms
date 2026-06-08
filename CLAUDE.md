# CLAUDE.md — ETTerms

> 給 Claude 的專案記憶與指令入口。詳細架構見 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 專案簡介

ETTerms 是一個 **C# .NET 8 WinForms** 的原生 Windows 終端機工作台，主打 **SSH** 與 **Serial Port** 兩種連線，並沿用 / 擴充 MyTeraTerm 的 **TTL 腳本引擎**做自動化。UI 參考 KKTerm（Activity Rail + 分頁工作區 + Saved Connections sidebar）。單機、無雲、無登入系統。

**開發策略：GUI 先行** — 先把視窗外殼 + 分頁 + 連線清單做出來，再逐步補 Serial → SSH → VT100 → 腳本引擎 → Settings/About → PDU/Shell/SFTP。

**進度：** Phase 1–5 ✅、Phase 6 ✅（TTL 引擎 + Group 同步，SSH 待驗收）、Phase 7 ✅（Settings/About）、Phase 8 ✅（PDU + Shell/ConPTY + SFTP + Settings 擴充）、Phase 9 ✅（Serial MCP server：**GUI 持有 COM port，MCP 經本機 named pipe 橋接**，AI 收發的資料即時以 `[AI]` 標色顯示在 GUI）。打包待指示。

**v0.3.1：** 終端機體驗修正（皆在 `src/ETTerms/Terminal/`）。**(1) 深色垂直捲軸** — 新增自繪 `DarkScrollBar`（細長、無箭頭、圓角滑塊，配合 KKTerm 深色主題），`TerminalView` 右側 `Dock=Right` 掛上，`ContentWidth` 扣掉捲軸寬避免文字被蓋，`UpdateScrollBar()` 在 `Feed` / 滾輪 / resize 時同步滑塊範圍與位置，滾輪與拖曳互通。**(2) 多行貼上修正** — `AnsiParser` 新增 `BracketedPaste`（DEC mode 2004）；`TerminalView.Paste()` 在對方啟用 bracketed paste（PSReadLine / Kiro CLI 等）時以 `ESC[200~ … ESC[201~` 包夾整段，視為「單次貼上」而非逐行 Enter 立即送出；未啟用時退回原本逐字送出。**(3) 右鍵複製清反白** — 右鍵複製後清掉 `_hasSel` 並重繪，讓使用者知道已複製。

**v0.3.0：** 新增 **`ETTerms.PduMcp`**（stdio MCP server）：讓 AI agent 直接控制 SNMP PDU 插座。與 serial 不同，PDU 走 SNMP(UDP) **非獨佔**，故 PduMcp **直接打 SNMP、不經 GUI 橋接**（內含精簡版 `PduController`，OID 邏輯複製自 GUI 版，log 走 stderr），GUI 不開著也能用。工具：`pdu_connect` / `pdu_list` / `pdu_set_port` / `pdu_get_port` / `pdu_status` / `pdu_power_cycle` / `pdu_disconnect`，回傳統一 `{ok, result/error}` JSON；連線狀態以行程內單例 `PduRegistry`（IP→controller）保存。`McpRegistrar` 改為多 server，**Settings → AI MCP 一鍵同時註冊 `etterms-serial` 與 `etterms-pdu`**；`ETTerms.csproj` 的 publish target 更名 `PublishMcpServers`，GUI publish 會把兩個 MCP 各自帶到 `\ETTerms.SerialMcp\`、`\ETTerms.PduMcp\` 子資料夾。

**v0.2.1：** 新增 GUI **Settings → AI MCP** 分頁（`McpRegistrar`）：對 Claude Code（`~/.claude.json`）與 Kiro（`~/.kiro/settings/mcp.json`）**一鍵 Setup / Remove** 註冊 `etterms-serial` MCP server，read-modify-write 保留檔內其他設定、原子寫回；卡片附 CLI 驗證指令。`ETTerms.csproj` 加 publish target（`AfterTargets=Publish`），GUI publish 會自動把 MCP server 帶到子資料夾，與 `McpRegistrar.ResolveServerExe()` 解析路徑對齊。

**v0.2.0：** Phase 9 完成 — `ETTerms.SerialMcp`（stdio MCP server）+ GUI `SerialBridgeServer`（named pipe `\\.\pipe\etterms-serial`）上線，提供 `serial_list` / `serial_attach` / `serial_write` / `serial_read` / `serial_detach` 五個工具，AI 的 TX 在 GUI 以 `[AI]` 標色即時 echo；視窗 / 工作列 / About 改用 Choco 圖示，標題列顯示版本號。見 [docs/serial-mcp-guide.md](docs/serial-mcp-guide.md)。

**v0.1.2：** 新增 `sprintf2`（TeraTerm 相容 C printf 格式化）；`wait` 改為命中關鍵字後須等裝置安靜（`SettleMs` 預設 300ms）才接受並取「最後一次」出現，排除輸出中途的指令回顯（避免腳本搶跑）。

## 技術棧

- **UI：** C# .NET 8 WinForms（`net8.0-windows`, `UseWindowsForms`, `Nullable=enable`）
- **SSH：** SSH.NET（`Renci.SshNet`）— Shell + SFTP
- **Serial：** `System.IO.Ports`
- **Local Shell：** Windows ConPTY（`CreatePseudoConsole`）— PowerShell / Bash / Cmd
- **終端機渲染：** 自繪 VT100 / ANSI 控制項（owner-drawn）
- **腳本：** `TTLInterpreter`（從 `For_AI/MyTeraTerm` 移植，改驅動 `ISessionChannel`）
- **連線儲存：** SQLite（`Microsoft.Data.Sqlite`）
- **密碼儲存：** Windows Credential Manager（不落地明碼）
- **PDU：** SnmpSharpNet（iPoMan II/III via SNMP）
- **AI / MCP（選用）：** 兩個 stdio MCP server（官方 C# SDK `ModelContextProtocol`）。`ETTerms.SerialMcp`：**不自己開 COM port**，經本機 named pipe 接上 GUI 持有的 serial session，AI 的 TX/RX 同步顯示在 GUI。`ETTerms.PduMcp`（v0.3.0）：**直接打 SNMP** 控制 PDU 插座，非獨佔故不需 GUI 在跑。皆暴露給 Kiro CLI / Claude CLI
- **設定持久化：** JSON → `%LocalAppData%\ETTerms\settings.json`

## 常用指令

```powershell
# 建置 / 執行
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj

# 加套件
dotnet add src\ETTerms package SSH.NET

# 打包（見「Publish / 打包慣例」）—— 兩種版本都產出，輸出到 src\ETTerms\Publish\
# GUI publish 會「自動」把 ETTerms.SerialMcp 與 ETTerms.PduMcp 一併發到各自的子資料夾
# （ETTerms.csproj 的 PublishMcpServers target，AfterTargets=Publish），且 MCP 跟隨 GUI 的 self-contained 設定。
$ver = ([regex]::Match((Get-Content src\ETTerms\ETTerms.csproj -Raw), '<Version>([^<]+)</Version>')).Groups[1].Value

# A. 框架相依版（需目標機已裝 .NET 8 Desktop Runtime）→ ETTerms_v{Version}\
$root = "src\ETTerms\Publish\ETTerms_v$ver"
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained false -o $root
Rename-Item (Join-Path $root "ETTerms.exe") "ETTerms v$ver.exe"

# B. Portable 免安裝版（runtime 內含，免裝、免管理員）→ ETTerms_v{Version}_portable\
$proot = "src\ETTerms\Publish\ETTerms_v${ver}_portable"
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained true -o $proot
Rename-Item (Join-Path $proot "ETTerms.exe") "ETTerms v$ver.exe"

# 註冊 Serial MCP server（給 AI agent 操作 serial）
# 推薦：GUI Settings → AI MCP 分頁，對 Claude Code / Kiro 按 Setup 一鍵註冊（McpRegistrar）。
# 或手動 CLI：
kiro-cli mcp add --name serial --command dotnet --args "run --project src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj"
```

## 開發慣例

- **命名：** PascalCase 類別 / 方法，`_camelCase` 私有欄位；檔名 = 類別名。
- **Publish / 打包：** 輸出到 `src\ETTerms\Publish\`；主 exe 改名為 `ETTerms v{Version}.exe`；`ETTerms.SerialMcp` 與 `ETTerms.PduMcp` 一併發到其下 `ETTerms.SerialMcp\`、`ETTerms.PduMcp\` 子資料夾（且跟隨 GUI 的 self-contained 設定）。**兩種版本都產出**：框架相依 `ETTerms_v{Version}\`（`--self-contained false`，需裝 .NET 8 Desktop Runtime）＋ portable 免安裝 `ETTerms_v{Version}_portable\`（`--self-contained true`，runtime 內含）。不要開 trimming（WinForms 反射）。詳見 [ARCHITECTURE.md](ARCHITECTURE.md#publish--打包慣例)。
- **分層：** UI（`App/`）只認 `ISessionChannel` 抽象，不直接相依 SSH.NET / SerialPort。
- **執行緒：** channel I/O 在背景；所有 UI 更新一律 `Control.Invoke` 回 UI thread。
- **commit：** 走 Conventional Commits（`feat:` / `fix:` / `refactor:` …）。
- **參考專案不改：** `For_AI/KKTerm-main`、`For_AI/MyTeraTerm` 只讀對照，不在 repo 內修改。

## 注意事項 / 禁止事項

- 🚫 **密碼絕不寫進 SQLite / 程式碼 / log**，一律走 Windows Credential Manager。
- 🚫 **不嵌 TeraTerm、不依賴 com0com** —— ETTerms 走全原生（這是與舊版 MyTeraTerm 的關鍵差異）。
- 🚫 不要把 `For_AI/` 內容 commit 進 git。
- ⚠️ Serial COM port 同時只能被一個 session 開啟，開啟前檢查可用性。
- ⚠️ COM port 由 **GUI 唯一持有**；Serial MCP server（`ETTerms.SerialMcp`）**不自己開 port**，而是經本機 named pipe 接上 GUI 已開啟的 serial session 來收發。AI 操作前該 port 必須已在 GUI 開啟（pipe 找不到對應 session 就回錯誤）。
- ⚠️ VT 相容性以常見情境（VT100 / 常見 ANSI）為主，冷門 escape 後補，不阻塞 GUI 進度。
- ⚠️ 本專案**無伺服端祕密 / 無 DB 密碼 / 無 EC2 / 無 VM**，因此不套用 AWS / VirtualBox 部署流程。
- ⚠️ Group 同步指令（`waitall` / `sendlnall` / `sendlngroup`）**只能在 Run Group 模式**使用；`▶ Script` 和 `▶ Run All` 須拒絕含這些指令的腳本。

## 資料夾用途

- **`src/ETTerms/`** — 主應用程式（WinForms 視窗外殼 + 連線 / 終端機 / 腳本引擎）。
- **`src/ETTerms.SerialMcp/`** — ✅ stdio MCP server（給 AI agent 收發 serial）。獨立行程，但**不直接開 COM port**：經本機 named pipe 連到 GUI 的 `SerialBridgeServer`，由 GUI 代為讀寫實體 port；net8.0 console + `ModelContextProtocol` SDK。
- **`src/ETTerms.PduMcp/`** — ✅ stdio MCP server（給 AI agent 控制 SNMP PDU，v0.3.0）。獨立行程，**直接打 SNMP**（內含精簡版 `PduController`），不經 GUI、GUI 不開著也能用；net8.0 console + `ModelContextProtocol` + `SnmpSharpNet`。
- **`For_AI/`** — AI 協作素材與**參考專案**（`KKTerm-main` UI 參考、`MyTeraTerm` Script 參考）。整個資料夾 gitignored，僅供開發對照。
- 本專案**無 `secret/` 資料夾**：沒有伺服端祕密 / DB 密碼 / compile-time secret，連線密碼一律走 Windows Credential Manager。
