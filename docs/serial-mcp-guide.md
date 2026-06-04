# Serial MCP 使用手冊

> 讓 **Kiro CLI / Claude CLI** 等 AI agent 直接收發 serial，**而且你能在 ETTerms GUI 即時看到 AI 的每一筆收發**。

---

## 這是什麼

`ETTerms.SerialMcp` 是一支獨立的 stdio MCP server。重點設計：

- **COM port 由 ETTerms GUI 唯一持有**，MCP server 自己**不開 port**。
- GUI 內跑一支本機 named pipe（`\\.\pipe\etterms-serial`）；MCP server 經這條 pipe 把 `attach / write / read` 轉發給 GUI，由 GUI 代為讀寫實體 port。
- AI 送出的資料會以 `[AI]` 標色 echo 進 GUI 終端機，所以**你看到的就是 AI 看到的**。

```
ETTerms GUI（唯一開 COM32 的人）
├─ Tab1: Serial 連線 COM32（即時顯示，AI 的寫入標 [AI]）
├─ named pipe: \\.\pipe\etterms-serial
└─ Tab2: PowerShell 跑 kiro-cli → 啟動 ETTerms.SerialMcp → 連回 pipe
```

---

## 前置需求

1. 已安裝 .NET 8（`dotnet --list-runtimes` 有 `WindowsDesktop 8.0.x`）。
2. 已編譯出 MCP 執行檔：
   ```powershell
   cd <你的 repo>\ETTerms
   dotnet build ETTerms.slnx
   ```
   產物在：`src\ETTerms.SerialMcp\bin\Debug\net8.0\ETTerms.SerialMcp.exe`
3. 已安裝並可在終端機執行 `kiro-cli`。

---

## 步驟 1：註冊 MCP 到 Kiro

用**編譯好的 exe** 註冊（不要用 `dotnet run`——它會把建置訊息印到 stdout，污染 JSON-RPC）。
建議用 **global** scope，這樣不論從哪個工作目錄啟動 kiro 都抓得到：

```powershell
kiro-cli mcp add --name serial --scope global --force `
  --command "<你的 repo>\ETTerms\src\ETTerms.SerialMcp\bin\Debug\net8.0\ETTerms.SerialMcp.exe"
```

成功會顯示：
```
✓ Added MCP server 'serial' to global config in C:\Users\<you>\.kiro\settings\mcp.json
```

> 設定檔位置：
> - global：`~\.kiro\settings\mcp.json`
> - workspace：`<專案>\.kiro\settings\mcp.json`（只在該目錄啟動 kiro 才生效）

---

## 步驟 2：重啟 kiro session

⚠️ **MCP server 只在 kiro session 啟動時載入。** 若你在註冊前就開著 kiro chat，必須重開：

1. 在 kiro chat 內輸入 `/quit` 回到 PowerShell。
2. 重新執行 `kiro-cli`。
3. 輸入 `/mcp`，應看到：
   ```
   serial   Status: ✓ Initialized
   Tools: serial_list, serial_attach, serial_write, serial_read, serial_detach
   ```

> `/mcp` 顯示 Initialized 只代表 MCP server 起得來，**還沒**連 pipe（連 pipe 是第一次呼叫 serial 工具時才發生）。

---

## 步驟 3：使用

**先在 ETTerms GUI 開好要操作的 serial 連線**（雙擊側欄連線開分頁），AI 才能 attach 上去。

然後直接對 AI 講白話即可，例如：

> 列出目前的 serial session，接上 COM32，送 `root` 然後讀回應。

AI 會依序呼叫 `serial_list` → `serial_attach` → `serial_write` → `serial_read`，
而你在 GUI 的 COM32 分頁會即時看到紫色 `[AI] root` 與裝置回應。

### ⚠️ 重要：session 名稱 = COM port 名稱

GUI 裡連線的**顯示名稱**（例如 `BP1`）與橋接用的**session 名稱不同**。
`serial_list` 回傳的是 **COM port 名稱**（例如 `COM32`），attach 時要用它：

| GUI 顯示 | serial_list 回傳 / attach 用 |
|---------|------------------------------|
| BP1     | `COM32` |
| BP2     | `COM17` |

---

## 工具清單

| 工具 | 參數 | 說明 |
|------|------|------|
| `serial_list` | — | 列出 GUI 目前開著的 serial session（名稱 + baud） |
| `serial_attach` | `portName`（如 `COM32`） | 綁定到某個 GUI session；write/read 前必須先 attach |
| `serial_write` | `text`, `appendNewline`（預設 true） | 送出文字（GUI 以 `[AI]` 標色顯示） |
| `serial_read` | `waitFor`?（等待子字串）, `timeoutMs`?（預設 3000） | 取出累積的 RX，回傳後清空 buffer |
| `serial_detach` | — | 解除綁定（**不會**關閉 GUI 的 port） |

---

## 疑難排解

| 症狀 | 原因 | 解法 |
|------|------|------|
| `/mcp` 顯示 `0 configured` | 還沒註冊，或 kiro session 早於註冊就開了 | 執行步驟 1，再 `/quit` 重開 kiro |
| `/mcp` 註冊了但仍看不到 | 用了 workspace scope，但 kiro 的工作目錄不同 | 改用 `--scope global --force` 重註冊 |
| 工具回 `bridge not connected; is the ETTerms GUI running?` | ETTerms GUI 沒開 | 先開 GUI（GUI 啟動時才會起 pipe server） |
| 工具回 `no open serial session '...' in GUI` | 該 COM port 在 GUI 沒開，或名稱打錯 | 先在 GUI 開該連線；用 `serial_list` 確認正確的 COM 名稱 |
| pipe 行為怪異 / 收不到 | 同時有別的程式佔用 pipe | pipe 一次只服務一個 client，關掉其他連 `etterms-serial` 的程式 |
| 註冊後 stdout 有奇怪輸出 | 用了 `dotnet run` 當 command | 改用編譯好的 `.exe` 路徑 |

---

## 移除

```powershell
kiro-cli mcp remove --name serial
```

---

## 進階：用 agent.json 設定（替代 mcp add）

也可直接寫進 agent 設定的 `mcpServers`（global：`~\.kiro\settings\mcp.json`）：

```json
{
  "mcpServers": {
    "serial": {
      "command": "<你的 repo>\\ETTerms\\src\\ETTerms.SerialMcp\\bin\\Debug\\net8.0\\ETTerms.SerialMcp.exe"
    }
  }
}
```
