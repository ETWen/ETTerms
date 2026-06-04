using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ETTerms.Infrastructure;

namespace ETTerms.Sessions;

/// <summary>
/// 本機 named pipe server：把 GUI 持有的 serial session 橋接給 MCP client（ETTerms.SerialMcp）。
/// 協議＝換行分隔 JSON（list / attach / write / detach），RX 由本端主動以 {"op":"rx"} 推送。
/// 一次服務一個 client（單一 AI agent 已足夠），斷線後自動回到等待下一個連線。
/// </summary>
public sealed class SerialBridgeServer : IDisposable
{
    public const string PipeName = "etterms-serial";

    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public void Start()
    {
        if (_loop != null) return;
        _loop = Task.Run(AcceptLoop);
        AppLogger.Info($"SerialBridge server started on pipe \\\\.\\pipe\\{PipeName}");
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_cts.Token);
                await ServeClient(pipe);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { AppLogger.LogWarning($"SerialBridge accept error: {ex.Message}"); }
        }
    }

    private async Task ServeClient(NamedPipeServerStream pipe)
    {
        using (pipe)
        {
            var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
            var reader = new StreamReader(pipe, new UTF8Encoding(false));
            var wlock = new object();
            void Send(object o) { lock (wlock) { try { writer.WriteLine(JsonSerializer.Serialize(o)); } catch { } } }

            SerialBridgeEndpoint? attached = null;
            Action<byte[]>? rx = null;
            void Detach() { if (attached != null && rx != null) attached.Rx -= rx; attached = null; rx = null; }

            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Req? req;
                    try { req = JsonSerializer.Deserialize<Req>(line); } catch { continue; }
                    if (req == null) continue;

                    switch (req.op)
                    {
                        case "list":
                            Send(new { sessions = SerialBridge.All.Select(e => new { name = e.Name, baud = e.BaudRate }) });
                            break;
                        case "attach":
                            Detach();
                            attached = SerialBridge.Find(req.session ?? "");
                            if (attached == null) { Send(new { ok = false, error = $"no open serial session '{req.session}' in GUI" }); break; }
                            rx = data => Send(new { op = "rx", data = Encoding.UTF8.GetString(data) });
                            attached.Rx += rx;
                            Send(new { ok = true, name = attached.Name });
                            break;
                        case "write":
                            if (attached == null) { Send(new { ok = false, error = "not attached" }); break; }
                            attached.Write(req.data ?? "", req.newline);
                            Send(new { ok = true });
                            break;
                        case "detach":
                            Detach();
                            Send(new { ok = true });
                            break;
                        default:
                            Send(new { ok = false, error = $"unknown op '{req.op}'" });
                            break;
                    }
                }
            }
            catch (Exception ex) { AppLogger.LogWarning($"SerialBridge client error: {ex.Message}"); }
            finally { Detach(); }
        }
    }

    private sealed class Req
    {
        public string? op { get; set; }
        public string? session { get; set; }
        public string? data { get; set; }
        public bool newline { get; set; }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
    }
}
