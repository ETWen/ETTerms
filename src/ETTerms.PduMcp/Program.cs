using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// stdio MCP server：把 SNMP PDU 控制暴露給 AI（Kiro / Claude CLI）。
// 與 ETTerms.SerialMcp 不同，PDU 走 SNMP(UDP) 非獨佔，故直接打 SNMP，不需 GUI 在跑。
var builder = Host.CreateApplicationBuilder(args);

// stdio 傳輸：stdout 專供 JSON-RPC，log 一律走 stderr，否則會污染協議。
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
