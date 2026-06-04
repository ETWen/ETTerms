using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// stdio MCP server：把 ETTerms GUI 持有的 serial session 暴露給 AI（Kiro / Claude CLI）。
// 自己不開 COM port，所有收發經 named pipe 轉給 GUI 的 SerialBridgeServer。
var builder = Host.CreateApplicationBuilder(args);

// stdio 傳輸：stdout 專供 JSON-RPC，log 一律走 stderr，否則會污染協議。
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
