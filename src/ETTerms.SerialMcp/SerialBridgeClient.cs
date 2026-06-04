using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ETTerms.SerialMcp;

/// <summary>
/// 連到 ETTerms GUI 的 SerialBridgeServer（named pipe \\.\pipe\etterms-serial）。
/// 轉發 list / attach / write / detach，背景累積 RX 供 <c>serial_read</c> 消費。
/// 單例：整個 MCP server 生命週期共用同一條 pipe，連線狀態跨工具呼叫保留。
/// </summary>
public sealed class SerialBridgeClient
{
    public static readonly SerialBridgeClient Instance = new();
    private const string PipeName = "etterms-serial";

    private readonly SemaphoreSlim _connLock = new(1, 1);
    private readonly SemaphoreSlim _reqLock = new(1, 1);
    private readonly object _wlock = new();
    private readonly object _rxlock = new();
    private readonly StringBuilder _rx = new();

    private StreamWriter? _writer;
    private TaskCompletionSource<JsonElement>? _pending;

    private async Task EnsureConnected()
    {
        await _connLock.WaitAsync();
        try
        {
            if (_writer != null) return;
            var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(3000);
            _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
            _ = Task.Run(() => ReadLoop(new StreamReader(pipe, new UTF8Encoding(false)), pipe));
        }
        finally { _connLock.Release(); }
    }

    private async Task ReadLoop(StreamReader reader, NamedPipeClientStream pipe)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                JsonElement doc;
                try { doc = JsonDocument.Parse(line).RootElement.Clone(); }
                catch { continue; }
                if (doc.TryGetProperty("op", out var op) && op.GetString() == "rx")
                {
                    var d = doc.TryGetProperty("data", out var dd) ? dd.GetString() ?? "" : "";
                    lock (_rxlock) _rx.Append(d);
                }
                else _pending?.TrySetResult(doc);
            }
        }
        catch { }
        finally { lock (_wlock) _writer = null; try { pipe.Dispose(); } catch { } }
    }

    private async Task<string> Request(object req)
    {
        await EnsureConnected();
        await _reqLock.WaitAsync();
        try
        {
            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = tcs;
            lock (_wlock)
            {
                if (_writer == null) throw new IOException("bridge not connected; is the ETTerms GUI running?");
                _writer.WriteLine(JsonSerializer.Serialize(req));
            }
            using var cts = new CancellationTokenSource(5000);
            using var reg = cts.Token.Register(() => tcs.TrySetException(new TimeoutException("no response from ETTerms bridge")));
            return (await tcs.Task).GetRawText();
        }
        finally { _pending = null; _reqLock.Release(); }
    }

    public Task<string> List() => Request(new { op = "list" });
    public Task<string> Attach(string portName) => Request(new { op = "attach", session = portName });
    public Task<string> Write(string text, bool newline) => Request(new { op = "write", data = text, newline });
    public Task<string> Detach() => Request(new { op = "detach" });

    /// <summary>取出累積的 RX；可等待子字串或逾時。回傳後清空 buffer。</summary>
    public async Task<string> Read(string? waitFor, int timeoutMs)
    {
        await EnsureConnected();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs <= 0 ? 1000 : timeoutMs);
        while (true)
        {
            string snap;
            lock (_rxlock) snap = _rx.ToString();
            bool hit = string.IsNullOrEmpty(waitFor) ? snap.Length > 0 : snap.Contains(waitFor);
            if (hit || DateTime.UtcNow >= deadline)
            {
                lock (_rxlock) _rx.Clear();
                return snap;
            }
            await Task.Delay(50);
        }
    }
}
