using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ETTerms.SerialMcp;

/// <summary>
/// 暴露給 AI 的 serial 工具。COM port 由 ETTerms GUI 持有，這些工具經 named pipe 橋接過去；
/// AI 的寫入會即時顯示在 GUI 終端機（標 [AI]）。使用前須先在 GUI 開好該 serial 連線。
/// </summary>
[McpServerToolType]
public static class SerialTools
{
    [McpServerTool, Description("List serial sessions currently open in the ETTerms GUI (name + baud). The GUI owns the COM port.")]
    public static Task<string> serial_list() => SerialBridgeClient.Instance.List();

    [McpServerTool, Description("Attach to a serial session already open in the ETTerms GUI by COM port name (e.g. COM3). Required before write/read. Does not open the port itself.")]
    public static Task<string> serial_attach(
        [Description("COM port name of an already-open GUI session, e.g. COM3")] string portName)
        => SerialBridgeClient.Instance.Attach(portName);

    [McpServerTool, Description("Send text to the attached serial session. The GUI displays it live tagged [AI].")]
    public static Task<string> serial_write(
        [Description("Text to send")] string text,
        [Description("Append the session newline (acts like pressing Enter)")] bool appendNewline = true)
        => SerialBridgeClient.Instance.Write(text, appendNewline);

    [McpServerTool, Description("Read output accumulated from the attached serial session. Optionally wait for a substring up to timeoutMs. Returns and clears the buffer.")]
    public static Task<string> serial_read(
        [Description("Substring to wait for; null/empty returns whatever has arrived")] string? waitFor = null,
        [Description("Max time to wait in milliseconds")] int timeoutMs = 3000)
        => SerialBridgeClient.Instance.Read(waitFor, timeoutMs);

    [McpServerTool, Description("Detach from the serial session. Does NOT close the GUI's port (the GUI keeps owning it).")]
    public static Task<string> serial_detach() => SerialBridgeClient.Instance.Detach();
}
