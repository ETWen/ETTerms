# ETTerms

原生 Windows 終端機工作台（C# .NET 8 WinForms），支援 **SSH** 與 **Serial Port** 連線，
並內建從 MyTeraTerm 移植的 **TTL 腳本引擎**做自動化。單機、無雲、無登入系統。

詳細架構見 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 建置 / 執行

```powershell
dotnet build
dotnet run --project src\ETTerms\ETTerms.csproj
```

## TTL 腳本

在任一連線分頁頂部的腳本列按 **▶ Script** 載入 `.ttl` 對該連線執行；左側狀態列即時顯示
執行到的行號與指令，**■ Stop** 可中止。完整語法與範例見
[docs/ttl-script-reference.md](docs/ttl-script-reference.md)，範例腳本在 `tools/scripts/`。

### 目前支援的指令

| 指令 | 說明 |
|------|------|
| `send '文字'` | 送出文字（不加換行） |
| `sendln '文字'` | 送出文字並附加 `\r\n` |
| `wait '字串'` | 一直等到接收緩衝出現該字串才往下（預設無限等待，可按 Stop 取消）；命中後 `result=1` |
| `flushrecv` | 清空接收緩衝區 |
| `pause 秒數` | 暫停 N 秒（可被 Stop 中止） |
| `timeout = 秒數` | 設定 `wait` 逾時秒數；`0`＝無限等待，`N>0` 超時會中止腳本並報錯 |
| `if … then` / `elseif … then` / `else` / `endif` | 條件分支（可巢狀） |
| `while …` / `endwhile` | 迴圈（可巢狀，可被 Stop 中止） |
| `名稱 = 值` | 變數指派；支援 `+ - * /` 整數運算與字串；內建變數 `result` |
| `logopen '檔名'` | 開啟 log 檔（覆寫） |
| `logwrite '文字'` | 寫一行到 log |
| `logclose` | 關閉 log（腳本結束自動關閉） |
| `messagebox '訊息'` | 跳出對話框（會顯示在最上層） |
| `; 註解` | 行內註解（`;` 之後到行尾） |
| `:label` | 標籤行（會被略過） |

**條件運算子**（`if` / `elseif` / `while`）：`>=` `<=` `>` `<` `==` `!=` `=`，或無運算子（非零為真）。

> `pductrl` / `pduconnect`（SNMP PDU 控制）屬 Phase 7，目前尚未支援。
