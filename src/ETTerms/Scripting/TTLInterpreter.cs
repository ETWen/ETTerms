using System.Text;
using System.Text.RegularExpressions;
using ETTerms.Scripting.Pdu;
using ETTerms.Sessions;

namespace ETTerms.Scripting;

/// <summary>
/// TTL (Tera Term Language) 直譯器，移植自 MyTeraTerm，改驅動 <see cref="ISessionChannel"/>
/// （取代舊版的 ComPortBridge）。送出走 <c>channel.Write</c>，接收監聽 <c>channel.DataReceived</c>。
/// 支援：send / sendln / pause / wait / timeout / flushrecv / logopen / logwrite / logclose /
/// messagebox / if-elseif-else-endif / while-endwhile / 變數指派。
/// 阻塞執行（wait/pause 用 Thread.Sleep），由 <see cref="ScriptRunner"/> 放到背景執行緒驅動。
/// </summary>
public sealed class TTLInterpreter : IDisposable
{
    private readonly ISessionChannel _channel;
    private readonly Encoding _enc = new UTF8Encoding(false);
    private readonly Dictionary<string, object> _vars = new();
    private readonly StringBuilder _recv = new();
    private readonly CancellationTokenSource _cts = new();

    private GroupSyncContext? _groupSync;
    private string _groupMemberLabel = "";
    private readonly Dictionary<int, PduController> _pdus = new();
    private StreamWriter? _logWriter;
    private int _timeout;   // 0 = 無限等待（wait 會一直等到關鍵字出現或被取消）
    private int _result;
    private string[] _lines = Array.Empty<string>();
    private int _line;
    private string _scriptFile = "";
    private volatile bool _cancelled;
    private bool _disposed;

    /// <summary>(檔名, 行號, 當前指令) 進度更新。</summary>
    public event Action<string, int, string>? StatusChanged;

    /// <summary>輸出訊息（log / 提示 / 錯誤），供 UI 顯示。</summary>
    public event Action<string>? Output;

    public TTLInterpreter(ISessionChannel channel, GroupSyncContext? groupSync = null, string memberLabel = "")
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _groupSync = groupSync;
        _groupMemberLabel = memberLabel;
        _channel.DataReceived += OnData;
    }

    private void OnData(byte[] data)
    {
        lock (_recv) _recv.Append(_enc.GetString(data));
    }

    #region Script Execution

    public void ExecuteScriptContent(string scriptContent, string fileName = "")
    {
        _scriptFile = string.IsNullOrEmpty(fileName) ? "Inline Script" : fileName;
        _cancelled = false;
        lock (_recv) _recv.Clear();

        _lines = Preprocess(scriptContent);
        _line = 0;

        while (_line < _lines.Length)
        {
            ThrowIfCancelled();
            StatusChanged?.Invoke(_scriptFile, _line + 1, _lines[_line]);
            ExecuteLine(_lines[_line]);
            _line++;
        }

        StatusChanged?.Invoke(_scriptFile, _lines.Length, "Completed");
        LogClose();
    }

    private static string[] Preprocess(string content)
    {
        var lines = new List<string>();
        foreach (string raw in content.Split('\n'))
        {
            string t = raw.Trim();
            int comment = t.IndexOf(';');
            if (comment >= 0) t = t.Substring(0, comment).Trim();
            if (!string.IsNullOrWhiteSpace(t)) lines.Add(t);
        }
        return lines.ToArray();
    }

    private void ThrowIfCancelled()
    {
        if (_cancelled) throw new OperationCanceledException("Script execution was cancelled by user");
    }

    #endregion

    #region Line Execution

    private void ExecuteLine(string line)
    {
        string original = line;
        bool isAssignment = original.Contains('=')
            && !original.Contains("==") && !original.Contains("!=")
            && !original.Contains(">=") && !original.Contains("<=")
            && !original.StartsWith("if ") && !original.StartsWith("elseif ");

        string command = GetCommand(line).ToLower();
        if (!isAssignment && command != "while" && command != "if")
            line = ReplaceVariables(line);

        if (line.StartsWith(":")) return;   // label

        string args = GetArgs(line);

        switch (command)
        {
            case "send": Send(args, false); break;
            case "sendln": Send(args, true); break;
            case "pause": Pause(args); break;
            case "wait": Wait(args); break;
            case "timeout": SetTimeout(args); break;
            case "flushrecv": FlushReceive(); break;
            case "logopen": LogOpen(args); break;
            case "logwrite": LogWrite(args); break;
            case "logclose": LogClose(); break;
            case "messagebox": ShowMessageBox(args); break;
            case "waitall": ExecuteWaitAll(args); break;
            case "sendlnall": ExecuteSendlnAll(args); break;
            case "sendlngroup": ExecuteSendlnGroup(args); break;
            case "pduconnect": ExecutePduConnect(args); break;
            case "pductrl": ExecutePduCtrl(args); break;
            case "while": ExecuteWhile(original); break;
            case "if": ExecuteIf(original); break;
            case "endwhile":
            case "elseif":
            case "else":
            case "endif":
                break;  // handled by ExecuteWhile / ExecuteIf
            case "":
                if (isAssignment) ExecuteAssignment(original);
                break;
            default:
                if (isAssignment) ExecuteAssignment(original);
                else Output?.Invoke($"[ttl] Unknown command: {command}");
                break;
        }
    }

    #endregion

    #region Control Flow - If

    private void ExecuteIf(string line)
    {
        var match = Regex.Match(line, @"if\s+(.+?)\s+then", RegexOptions.IgnoreCase);
        if (!match.Success) match = Regex.Match(line, @"if\s+(.+)", RegexOptions.IgnoreCase);
        if (!match.Success) throw new Exception("Invalid if statement");

        string condition = match.Groups[1].Value.Trim();
        int ifStart = _line;
        int endif = FindBlockEnd(ifStart, "if ", "endif");
        if (endif < 0) throw new Exception("if statement without matching endif");

        var elseifLines = new List<int>();
        int elseLine = -1;
        for (int i = ifStart + 1; i < endif; i++)
        {
            string t = _lines[i].Trim().ToLower();
            if (t.StartsWith("elseif")) elseifLines.Add(i);
            else if (t.StartsWith("else")) { elseLine = i; break; }
        }

        bool met = false;
        int start = -1, end = -1;
        if (EvaluateCondition(ReplaceVariables(condition)))
        {
            met = true; start = ifStart + 1;
            end = elseifLines.Count > 0 ? elseifLines[0] : (elseLine >= 0 ? elseLine : endif);
        }
        else
        {
            for (int i = 0; i < elseifLines.Count && !met; i++)
            {
                var em = Regex.Match(_lines[elseifLines[i]], @"elseif\s+(.+?)\s+then", RegexOptions.IgnoreCase);
                if (!em.Success) em = Regex.Match(_lines[elseifLines[i]], @"elseif\s+(.+)", RegexOptions.IgnoreCase);
                if (em.Success && EvaluateCondition(ReplaceVariables(em.Groups[1].Value.Trim())))
                {
                    met = true; start = elseifLines[i] + 1;
                    end = (i + 1 < elseifLines.Count) ? elseifLines[i + 1] : (elseLine >= 0 ? elseLine : endif);
                }
            }
            if (!met && elseLine >= 0) { met = true; start = elseLine + 1; end = endif; }
        }

        if (met && start >= 0 && end > start)
        {
            _line = start;
            while (_line < end)
            {
                ThrowIfCancelled();
                StatusChanged?.Invoke(_scriptFile, _line + 1, _lines[_line]);
                ExecuteLine(_lines[_line]);
                _line++;
            }
        }
        _line = endif;
    }

    #endregion

    #region Control Flow - While

    private void ExecuteWhile(string line)
    {
        var match = Regex.Match(line, @"while\s+(.+)", RegexOptions.IgnoreCase);
        if (!match.Success) throw new Exception("Invalid while statement");

        string condition = match.Groups[1].Value.Trim();
        int whileStart = _line;
        int endwhile = FindBlockEnd(whileStart, "while", "endwhile");
        if (endwhile < 0) throw new Exception("while statement without matching endwhile");

        while (true)
        {
            ThrowIfCancelled();
            if (!EvaluateCondition(ReplaceVariables(condition))) break;

            _line = whileStart + 1;
            while (_line < endwhile)
            {
                ThrowIfCancelled();
                StatusChanged?.Invoke(_scriptFile, _line + 1, _lines[_line]);
                ExecuteLine(_lines[_line]);
                _line++;
            }
        }
        _line = endwhile;
    }

    /// <summary>找出與 <paramref name="open"/> 配對的 <paramref name="close"/>（支援巢狀）。</summary>
    private int FindBlockEnd(int startIndex, string open, string close)
    {
        int depth = 0;
        for (int i = startIndex; i < _lines.Length; i++)
        {
            string t = _lines[i].Trim().ToLower();
            if (t.StartsWith(open)) depth++;
            else if (t.StartsWith(close)) { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    #endregion

    #region Condition Evaluation

    private bool EvaluateCondition(string condition)
    {
        condition = condition.Trim();

        foreach (var (op, isNumeric) in new[] { (">=", true), ("<=", true), ("==", false), ("!=", false), (">", true), ("<", true) })
        {
            int at = condition.IndexOf(op, StringComparison.Ordinal);
            if (at < 0) continue;
            string l = condition.Substring(0, at);
            string r = condition.Substring(at + op.Length);
            if (isNumeric)
            {
                int li = ParseIntDirect(l), ri = ParseIntDirect(r);
                return op switch { ">=" => li >= ri, "<=" => li <= ri, ">" => li > ri, _ => li < ri };
            }
            string ls = l.Trim().Trim('\'', '"'), rs = r.Trim().Trim('\'', '"');
            return op == "==" ? ls == rs : ls != rs;
        }

        // single '=' as equality
        int eq = condition.IndexOf('=');
        if (eq >= 0)
        {
            string l = condition.Substring(0, eq).Trim(), r = condition.Substring(eq + 1).Trim();
            if (int.TryParse(l, out int li) && int.TryParse(r, out int ri)) return li == ri;
            return l.Trim('\'', '"') == r.Trim('\'', '"');
        }

        return ParseIntDirect(condition) != 0;
    }

    #endregion

    #region Variable Assignment

    private void ExecuteAssignment(string line)
    {
        int eq = line.IndexOf('=');
        if (eq < 0) return;

        string name = line.Substring(0, eq).Trim();
        string expr = line.Substring(eq + 1).Trim();

        if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            Output?.Invoke($"[ttl] Invalid variable name '{name}'");
            return;
        }

        _vars[name] = EvaluateExpression(ReplaceVariables(expr));
    }

    private object EvaluateExpression(string expr)
    {
        expr = expr.Trim();

        foreach (char op in new[] { '+', '-', '*', '/' })
        {
            if (op == '-' && expr.StartsWith("-")) continue;
            int at = expr.IndexOf(op);
            if (at <= 0) continue;
            int l = ParseIntDirect(expr.Substring(0, at));
            int r = ParseIntDirect(expr.Substring(at + 1));
            return op switch { '+' => l + r, '-' => l - r, '*' => l * r, _ => r != 0 ? l / r : 0 };
        }

        if (expr.StartsWith("\"") && expr.EndsWith("\"") && expr.Length >= 2)
            return expr.Substring(1, expr.Length - 2);
        if (int.TryParse(expr, out int v)) return v;
        return expr;
    }

    #endregion

    #region Basic Commands

    private void Send(string args, bool newline)
    {
        string text = args.Trim('\'', '"');
        _channel.Write(_enc.GetBytes(text + (newline ? "\r\n" : "")));
        _logWriter?.WriteLine($">> {text}");
        Output?.Invoke($">> {text}");
    }

    private void Wait(string text)
    {
        text = ReplaceVariables(text.Trim().Trim('"', '\''));
        if (string.IsNullOrEmpty(text)) return;   // 沒有關鍵字可等，直接略過
        Output?.Invoke($"[wait] '{text}'");
        _logWriter?.WriteLine($"[Wait] {text}");

        int elapsed = 0;
        while (true)
        {
            ThrowIfCancelled();
            lock (_recv)
            {
                string buf = _recv.ToString();
                int idx = buf.IndexOf(text, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    _result = 1;
                    _recv.Clear();
                    _recv.Append(buf.Substring(idx + text.Length));
                    return;
                }
            }
            // 只有在明確設定 timeout(>0) 且超時才中止腳本；否則一直等到關鍵字出現或被取消。
            if (_timeout > 0 && elapsed >= _timeout)
            {
                _result = 0;
                _logWriter?.WriteLine($"[Wait] timeout: {text}");
                throw new TimeoutException($"wait timeout ({_timeout}ms): '{text}'");
            }
            Thread.Sleep(100);
            elapsed += 100;
        }
    }

    private void FlushReceive()
    {
        lock (_recv) _recv.Clear();
    }

    private void Pause(string args)
    {
        int total = ParseInt(args) * 1000, elapsed = 0;
        while (elapsed < total)
        {
            ThrowIfCancelled();
            int slice = Math.Min(100, total - elapsed);
            Thread.Sleep(slice);
            elapsed += slice;
        }
    }

    private void SetTimeout(string args)
    {
        var m = Regex.Match(args, @"=\s*(\d+)");
        _timeout = (m.Success ? ParseInt(m.Groups[1].Value) : ParseInt(args)) * 1000;
    }

    private void LogOpen(string args)
    {
        var m = Regex.Match(args, @"['""]([^'""]+)['""]");
        string file = m.Success ? m.Groups[1].Value : args.Trim().Trim('\'', '"');
        if (string.IsNullOrWhiteSpace(file)) return;
        _logWriter = new StreamWriter(file, false);
        Output?.Invoke($"[log] opened: {file}");
    }

    private void LogWrite(string args)
    {
        if (_logWriter == null) return;
        string text = args.Trim('\'', '"');
        _logWriter.WriteLine(text);
        _logWriter.Flush();
    }

    private void LogClose()
    {
        if (_logWriter == null) return;
        _logWriter.Flush();
        _logWriter.Dispose();
        _logWriter = null;
    }

    private void ShowMessageBox(string args)
    {
        string message = ReplaceVariables(args.Trim('\'', '"'));
        Output?.Invoke($"[messagebox] {message}");
        var main = System.Windows.Forms.Application.OpenForms.Count > 0
            ? System.Windows.Forms.Application.OpenForms[0] : null;
        if (main != null && main.InvokeRequired)
            main.Invoke(() => System.Windows.Forms.MessageBox.Show(main, message, "TTL Script",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information));
        else
            System.Windows.Forms.MessageBox.Show(main, message, "TTL Script",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
    }

    private void ExecuteWaitAll(string args)
    {
        if (_groupSync == null)
            throw new InvalidOperationException("'waitall' can only be used in Group execution mode.");
        // 先各自等到關鍵字出現
        string text = ReplaceVariables(args.Trim().Trim('"', '\''));
        if (!string.IsNullOrEmpty(text))
            Wait(text);
        // 再等所有成員都完成 wait
        Output?.Invoke("[waitall] waiting for all group members...");
        _groupSync.WaitAll(_cts.Token);
        Output?.Invoke("[waitall] all members synchronized");
    }

    private void ExecuteSendlnAll(string args)
    {
        if (_groupSync == null)
            throw new InvalidOperationException("'sendlnall' can only be used in Group execution mode.");
        Output?.Invoke("[sendlnall] waiting for all group members...");
        _groupSync.WaitAll(_cts.Token);
        Send(args, true);
    }

    private void ExecuteSendlnGroup(string args)
    {
        if (_groupSync == null)
            throw new InvalidOperationException("'sendlngroup' can only be used in Group execution mode.");
        // 格式: sendlngroup A "show version"
        var m = Regex.Match(args, @"^(\w+)\s+(.+)$");
        if (!m.Success) { Output?.Invoke("[sendlngroup] invalid syntax, expected: sendlngroup A \"command\""); return; }
        string target = m.Groups[1].Value.ToUpper();
        string cmd = m.Groups[2].Value.Trim('\'', '"');
        if (string.Equals(target, _groupMemberLabel, StringComparison.OrdinalIgnoreCase))
            Send(cmd, true);
    }

    // ── PDU Commands ──

    private void ExecutePduConnect(string args)
    {
        // pduconnect <device> <ip>
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) { Output?.Invoke("[pduconnect] syntax: pduconnect <device> <ip>"); _result = 0; return; }
        int device = ParseIntDirect(parts[0]);
        string ip = parts[1].Trim('\'', '"');
        var pdu = new PduController(ip);
        if (pdu.CheckConnection())
        {
            _pdus[device] = pdu;
            _result = 1;
            Output?.Invoke($"[pduconnect] connected to device {device} at {ip}");
        }
        else
        {
            pdu.Dispose();
            _result = 0;
            Output?.Invoke($"[pduconnect] failed to connect to {ip}");
        }
    }

    private void ExecutePduCtrl(string args)
    {
        // pductrl <device> <port> <action: 1=on, 0=off>
        args = ReplaceVariables(args);
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) { Output?.Invoke("[pductrl] syntax: pductrl <device> <port> <0|1>"); _result = 0; return; }
        int device = ParseIntDirect(parts[0]);
        int port = ParseIntDirect(parts[1]);
        int action = ParseIntDirect(parts[2]);

        if (!_pdus.TryGetValue(device, out var pdu))
        {
            Output?.Invoke($"[pductrl] device {device} not connected. Use pduconnect first.");
            _result = 0; return;
        }

        bool ok = action == 1 ? pdu.SetPortOn(port) : pdu.SetPortOff(port);
        _result = ok ? 1 : 0;
        string act = action == 1 ? "ON" : "OFF";
        Output?.Invoke($"[pductrl] device {device} port {port} {act} → {(ok ? "OK" : "FAILED")}");
        _logWriter?.WriteLine($"[pductrl] device={device} port={port} action={act} result={_result}");
    }

    #endregion

    #region Helpers

    private static string GetCommand(string line)
    {
        int sp = line.IndexOf(' ');
        return sp > 0 ? line.Substring(0, sp).Trim() : line.Trim();
    }

    private static string GetArgs(string line)
    {
        int sp = line.IndexOf(' ');
        return sp > 0 ? line.Substring(sp + 1).Trim() : "";
    }

    private string ReplaceVariables(string text)
    {
        foreach (var kvp in _vars)
            text = Regex.Replace(text, @"\b" + Regex.Escape(kvp.Key) + @"\b", kvp.Value?.ToString() ?? "");
        return Regex.Replace(text, @"\bresult\b", _result.ToString());
    }

    private int ParseInt(string value) => ParseIntDirect(ReplaceVariables(value.Trim()));

    private static int ParseIntDirect(string value) =>
        int.TryParse(value.Trim(), out int r) ? r : 0;

    #endregion

    public void Cancel() { _cancelled = true; _cts.Cancel(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancelled = true;
        _cts.Cancel();
        _cts.Dispose();
        LogClose();
        foreach (var pdu in _pdus.Values) pdu.Dispose();
        _pdus.Clear();
        _channel.DataReceived -= OnData;
    }
}
