# TTL Script Reference — ETTerms

> ETTerms 的 TTL（Tera Term Language）腳本引擎移植自 MyTeraTerm，改為驅動原生
> `ISessionChannel`（SSH / Serial 皆可）。在 **Scripts 檢視**載入 `.ttl` 腳本，對
> **目前 active session** 執行（先在 Terminal 檢視開好連線）。

執行於背景執行緒，可隨時按 **Stop** 中止；`wait` / `pause` 期間皆可取消。

---

## 語法規則

- 一行一個指令；前後空白會被去除。
- 註解：`;` 之後到行尾視為註解。
- 字串引號 `'...'` 或 `"..."` 皆可，送出時引號會被去除。
- 變數以 `名稱 = 值` 指派；名稱須符合 `^[a-zA-Z_][a-zA-Z0-9_]*$`。
- 內建變數 `result`：`wait` 命中為 `1`、逾時為 `0`。
- 換行：`sendln` 自動附加 `\r\n`。

---

## 指令對照表

| 指令 | 語法 | 說明 |
|------|------|------|
| `send` | `send '文字'` | 送出文字（不加換行）到 active session。 |
| `sendln` | `sendln '文字'` | 送出文字並附加 `\r\n`。 |
| `wait` | `wait '字串'` | **一直等到**接收緩衝出現指定字串才往下；預設無限等待（可按 Stop 取消）。命中後 `result=1` 並清掉緩衝中該字串（含）之前的內容。 |
| `pause` | `pause 秒數` | 暫停指定秒數（可被 Stop 中止）。 |
| `timeout` | `timeout = 秒數` | 設定 `wait` 的逾時秒數。預設 `0`＝無限等待；若設為 `N>0`，`wait` 超過 N 秒仍未命中會**中止整個腳本並報錯**（不會略過 `wait` 往下做）。 |
| `flushrecv` | `flushrecv` | 清空接收緩衝區。 |
| `logopen` | `logopen '檔名'` | 開啟 log 檔（覆寫）；`send` 與 `logwrite` 會寫入。 |
| `logwrite` | `logwrite '文字'` | 寫一行到 log 檔。 |
| `logclose` | `logclose` | 關閉 log 檔（腳本結束時自動關閉）。 |
| `messagebox` | `messagebox '訊息'` | 跳出訊息對話框。 |
| `if` / `elseif` / `else` / `endif` | 見下 | 條件分支（可巢狀）。 |
| `while` / `endwhile` | 見下 | 迴圈（可巢狀，可被 Stop 中止）。 |
| `名稱 = 值` | `idx = 0` | 變數指派，支援 `+ - * /` 整數運算與字串。 |

> **注意：** PDU 控制指令（`pductrl` / `pduconnect`）屬 Phase 7，本階段尚未提供。

---

## Group 同步指令

以下指令**只能在 Run Group 模式**下使用（toolbar 的 `▶ Group1` / `▶ Group2` / `▶ Group3`）。
若在 `▶ Script`（分頁個別執行）或 `▶ Run All` 使用，會跳 Warning 並拒絕執行。

Group 內的成員依加入順序編為 **A, B, C, D...**，顯示在 cell footer（如 `[Group1-A]`）。

| 指令 | 語法 | 說明 |
|------|------|------|
| `waitall` | `waitall '字串'` | 各成員各自 `wait` 到指定字串出現後，再等其他成員也完成，全員到齊才繼續下一步。 |
| `sendlnall` | `sendlnall '文字'` | 等所有成員到達此行後，每人各自對自己的 channel `sendln` 同一段文字。 |
| `sendlngroup` | `sendlngroup A '文字'` | 只有指定 member（A/B/C...）會 `sendln`，其他成員跳過。用於 Group 內各設備需送不同指令的情境。 |

### Group 範例：同步升級多台交換機

```ttl
; Group1 有 A=SW1, B=SW2, C=SW3
; 每台各自等到 prompt 再同步
waitall '#'

; 各送不同指令
sendlngroup A 'copy tftp://10.0.0.1/sw1.bin flash:'
sendlngroup B 'copy tftp://10.0.0.1/sw2.bin flash:'
sendlngroup C 'copy tftp://10.0.0.1/sw3.bin flash:'

; 等所有人下載完畢
waitall '#'

; 全員一起 reload
sendlnall 'reload'
waitall 'confirm'
sendlnall 'y'
```

### Group 設定方式

1. 在分頁 Tab 上**右鍵** → 選擇 `Group 1` / `Group 2` / `Group 3`（或 `No Group` 取消）
2. 設定後 cell footer 會顯示 `[Group1-A]`、`[Group1-B]` 等標籤
3. 點 toolbar 的 `▶ Group1` 載入 `.ttl` 檔，Group 內所有成員平行執行同一份腳本

---

## 條件運算子

`if` / `elseif` / `while` 條件支援：

| 運算子 | 類型 | 範例 |
|--------|------|------|
| `>=` `<=` `>` `<` | 數值 | `if idx >= 3 then` |
| `==` `!=` | 字串 / 數值 | `if result == 1 then` |
| `=` | 數值或字串相等 | `if result = 0 then` |
| （無運算子） | 非零為真 | `if result then` |

`then` 關鍵字可省略。

---

## 範例：SSH 自動登入 + 收 log

```ttl
; 等提示字元，逐步登入並收集輸出
timeout = 15

wait 'login:'
sendln 'admin'
wait 'Password:'
sendln 'secret'
wait '$'

logopen 'session.log'
sendln 'uname -a'
wait '$'
sendln 'uptime'
wait '$'
logclose

messagebox 'Done'
```

## 範例：while 迴圈下命令

```ttl
idx = 0
while idx < 5
    sendln 'echo loop'
    wait '$'
    idx = idx + 1
endwhile
```

## 範例：if / elseif / else

```ttl
sendln 'whoami'
wait '$'
if result == 1 then
    logwrite 'prompt matched'
elseif result == 0 then
    logwrite 'timed out'
else
    logwrite 'unknown'
endif
```
