# CLAUDE.md — ETTerms

> 給 Claude 的專案記憶與指令入口。詳細架構見 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 專案簡介

ETTerms 是一個 **C# .NET 8 WinForms** 的原生 Windows 終端機工作台，主打 **SSH** 與 **Serial Port** 兩種連線，並沿用 / 擴充 MyTeraTerm 的 **TTL 腳本引擎**做自動化。UI 參考 KKTerm（Activity Rail + 分頁工作區 + Saved Connections sidebar）。單機、無雲、無登入系統。

**開發策略：GUI 先行** — 先把視窗外殼 + 分頁 + 連線清單做出來，再逐步補 Serial → SSH → VT100 → 腳本引擎 → Settings/About → PDU/Shell/SFTP。

**進度：** Phase 1–5 ✅、Phase 6 ✅（TTL 引擎 + Group 同步，SSH 待驗收）、Phase 7 ✅（Settings/About）、Phase 8 ✅（PDU + Shell/ConPTY + SFTP + Settings 擴充）、Phase 9 🔜（Serial MCP server：**GUI 持有 COM port，MCP 經本機 named pipe 橋接**，AI 收發的資料即時顯示在 GUI）。打包待指示。

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
- **AI / MCP（選用）：** stdio MCP server（`ETTerms.SerialMcp`，官方 C# SDK `ModelContextProtocol`）。**不自己開 COM port**，而是經本機 named pipe 接上 GUI 持有的 serial session，把 serial 收發暴露給 Kiro CLI / Claude CLI；AI 的 TX/RX 同步顯示在 GUI
- **設定持久化：** JSON → `%LocalAppData%\ETTerms\settings.json`

## 常用指令

```powershell
# 建置 / 執行
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj

# 加套件
dotnet add src\ETTerms package SSH.NET

# 打包
dotnet publish src\ETTerms\ETTerms.csproj -c Release -r win-x64 --self-contained false

# 註冊 Serial MCP server（給 AI agent 操作 serial）
kiro-cli mcp add --name serial --command dotnet --args "run --project src\ETTerms.SerialMcp\ETTerms.SerialMcp.csproj"
```

## 開發慣例

- **命名：** PascalCase 類別 / 方法，`_camelCase` 私有欄位；檔名 = 類別名。
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
- **`src/ETTerms.SerialMcp/`** — 🔜 stdio MCP server（給 AI agent 收發 serial）。獨立行程，但**不直接開 COM port**：經本機 named pipe 連到 GUI 的 `SerialBridgeServer`，由 GUI 代為讀寫實體 port；net8.0 console + `ModelContextProtocol` SDK。
- **`For_AI/`** — AI 協作素材與**參考專案**（`KKTerm-main` UI 參考、`MyTeraTerm` Script 參考）。整個資料夾 gitignored，僅供開發對照。
- 本專案**無 `secret/` 資料夾**：沒有伺服端祕密 / DB 密碼 / compile-time secret，連線密碼一律走 Windows Credential Manager。
